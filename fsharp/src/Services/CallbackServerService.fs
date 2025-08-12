namespace SpotifyCLI.Services

open System
open System.IO
open System.Net
open System.Text
open System.Threading
open System.Threading.Tasks
open System.Web
open FsToolkit.ErrorHandling
open SpotifyCLI.Domain

/// OAuth callback result from authorization server
[<Struct>]
type CallbackResult = {
    AuthorizationCode: AuthorizationCode option
    State: string option
    Error: string option
    ErrorDescription: string option
}

/// Callback server configuration
[<Struct>]
type CallbackServerConfig = {
    Port: PortNumber
    TimeoutMs: int
    SuccessHtml: string
    ErrorHtml: string
}


/// Callback server service interface for dependency injection
type ICallbackServerService =
    abstract StartCallbackServer: CallbackServerConfig -> Result<unit, CallbackServerError>
    abstract WaitForCallback: CancellationToken -> Task<Result<CallbackResult, CallbackServerError>>
    abstract StopCallbackServer: unit -> Result<unit, CallbackServerError>
    abstract IsServerRunning: unit -> bool

/// HTTP listener-based callback server implementation
type HttpCallbackServerService() =
    
    let mutable httpListener: HttpListener option = None
    let mutable callbackResult: CallbackResult option = None
    let mutable isRunning = false
    
    /// Create default success HTML response
    let createSuccessHtml () = """
<!DOCTYPE html>
<html>
<head>
    <title>Spotify CLI - Authorization Successful</title>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; 
               text-align: center; padding: 50px; background: #1db954; color: white; }
        .container { max-width: 600px; margin: 0 auto; }
        .icon { font-size: 64px; margin-bottom: 20px; }
        h1 { margin-bottom: 10px; }
        p { font-size: 18px; margin: 20px 0; }
        .close-instruction { font-size: 14px; color: #b3b3b3; margin-top: 40px; }
    </style>
</head>
<body>
    <div class="container">
        <div class="icon">🎵</div>
        <h1>Authorization Successful!</h1>
        <p>You have successfully authorized the Spotify CLI application.</p>
        <p>You can now close this tab and return to your terminal.</p>
        <div class="close-instruction">This window can be safely closed.</div>
    </div>
</body>
</html>"""
    
    /// Create default error HTML response
    let createErrorHtml (error: string) (description: string option) = $"""
<!DOCTYPE html>
<html>
<head>
    <title>Spotify CLI - Authorization Failed</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; 
               text-align: center; padding: 50px; background: #e22134; color: white; }}
        .container {{ max-width: 600px; margin: 0 auto; }}
        .icon {{ font-size: 64px; margin-bottom: 20px; }}
        h1 {{ margin-bottom: 10px; }}
        p {{ font-size: 18px; margin: 20px 0; }}
        .error-details {{ background: rgba(0,0,0,0.2); padding: 20px; border-radius: 5px; margin: 20px 0; }}
        .close-instruction {{ font-size: 14px; color: #fcc; margin-top: 40px; }}
    </style>
</head>
<body>
    <div class="container">
        <div class="icon">❌</div>
        <h1>Authorization Failed</h1>
        <p>There was an error authorizing the Spotify CLI application.</p>
        <div class="error-details">
            <strong>Error:</strong> {error}<br>
            {match description with Some desc -> $"<strong>Description:</strong> {desc}" | None -> ""}
        </div>
        <p>Please close this tab and try again from your terminal.</p>
        <div class="close-instruction">This window can be safely closed.</div>
    </div>
</body>
</html>"""
    
    /// Parse callback URL query parameters
    let parseCallbackQuery (queryString: string) : CallbackResult =
        try
            let query = HttpUtility.ParseQueryString(queryString)
            {
                AuthorizationCode = 
                    match query.["code"] with
                    | null | "" -> None
                    | code -> Some(AuthorizationCode code)
                State = 
                    match query.["state"] with
                    | null | "" -> None
                    | state -> Some state
                Error = 
                    match query.["error"] with
                    | null | "" -> None
                    | error -> Some error
                ErrorDescription = 
                    match query.["error_description"] with
                    | null | "" -> None
                    | desc -> Some desc
            }
        with
        | ex -> 
            {
                AuthorizationCode = None
                State = None
                Error = Some "parse_error"
                ErrorDescription = Some ex.Message
            }
    
    /// Handle incoming HTTP callback request
    let handleCallbackRequest (context: HttpListenerContext) (config: CallbackServerConfig) : CallbackResult =
        let request = context.Request
        let response = context.Response
        
        try
            // Parse the callback result
            let result = parseCallbackQuery request.Url.Query
            
            // Prepare response HTML
            let responseHtml = 
                match result.Error with
                | Some error -> createErrorHtml error result.ErrorDescription
                | None when result.AuthorizationCode.IsSome -> createSuccessHtml()
                | None -> createErrorHtml "missing_code" (Some "Authorization code not received")
            
            // Send HTTP response
            let responseBytes = Encoding.UTF8.GetBytes(responseHtml)
            response.ContentType <- "text/html; charset=utf-8"
            response.ContentLength64 <- int64 responseBytes.Length
            response.StatusCode <- 200
            
            use output = response.OutputStream
            output.Write(responseBytes, 0, responseBytes.Length)
            output.Close()
            
            result
        with
        | ex ->
            // Handle error in request processing
            try
                let errorHtml = createErrorHtml "server_error" (Some ex.Message)
                let errorBytes = Encoding.UTF8.GetBytes(errorHtml)
                response.ContentType <- "text/html; charset=utf-8"
                response.ContentLength64 <- int64 errorBytes.Length
                response.StatusCode <- 500
                
                use output = response.OutputStream
                output.Write(errorBytes, 0, errorBytes.Length)
                output.Close()
            with
            | _ -> () // Ignore response errors
            
            {
                AuthorizationCode = None
                State = None
                Error = Some "server_error"
                ErrorDescription = Some ex.Message
            }
    
    interface ICallbackServerService with
        
        member _.StartCallbackServer(config: CallbackServerConfig) =
            try
                if isRunning then
                    Ok() // Already running
                else
                    let listener = new HttpListener()
                    let port = TypeExtraction.getPortNumber config.Port
                    let prefix = $"http://localhost:{port}/"
                    
                    listener.Prefixes.Add(prefix)
                    listener.Start()
                    
                    httpListener <- Some listener
                    isRunning <- true
                    Ok()
            with
            | :? HttpListenerException as ex when ex.ErrorCode = 32 ->
                Error(PortAlreadyInUse(TypeExtraction.getPortNumber config.Port))
            | :? HttpListenerException as ex ->
                Error(ServerStartFailed(TypeExtraction.getPortNumber config.Port, ex.Message))
            | ex ->
                Error(ServerStartFailed(TypeExtraction.getPortNumber config.Port, ex.Message))
        
        member _.WaitForCallback(cancellationToken: CancellationToken) =
            taskResult {
                match httpListener with
                | None -> 
                    return! Error(ServerStartFailed(0, "Server not started"))
                | Some listener ->
                    try
                        // Wait for incoming request with cancellation support
                        let! context = listener.GetContextAsync()
                        
                        // Create default config for response HTML
                        let config = {
                            Port = PortNumber 3000
                            TimeoutMs = 30000
                            SuccessHtml = createSuccessHtml()
                            ErrorHtml = createErrorHtml "unknown_error" None
                        }
                        
                        let result = handleCallbackRequest context config
                        callbackResult <- Some result
                        
                        return result
                    with
                    | :? OperationCanceledException ->
                        return! Error(CallbackTimeout 30000)
                    | ex ->
                        return! Error(InvalidCallback ex.Message)
            }
        
        member _.StopCallbackServer() =
            try
                match httpListener with
                | Some listener ->
                    listener.Stop()
                    listener.Close()
                    httpListener <- None
                    isRunning <- false
                    Ok()
                | None ->
                    Ok() // Already stopped
            with
            | ex ->
                Error(ServerShutdownFailed ex.Message)
        
        member _.IsServerRunning() = isRunning

/// Factory functions for creating callback server service
module CallbackServerService =
    
    let create () : ICallbackServerService =
        HttpCallbackServerService() :> ICallbackServerService

/// Callback server utilities and helpers
module CallbackServerHelpers =
    
    /// Create default callback server configuration
    let createDefaultConfig (port: PortNumber) : CallbackServerConfig = {
        Port = port
        TimeoutMs = 60000 // 1 minute timeout
        SuccessHtml = """
<!DOCTYPE html>
<html><head><title>Success</title></head>
<body><h1>✅ Authorization Successful</h1><p>You can close this tab.</p></body>
</html>"""
        ErrorHtml = """
<!DOCTYPE html>
<html><head><title>Error</title></head>
<body><h1>❌ Authorization Failed</h1><p>Please try again.</p></body>
</html>"""
    }
    
    /// Format callback server error for user display
    let formatCallbackServerError = function
        | ServerStartFailed(port, reason) -> 
            $"Failed to start callback server on port {port}: {reason}"
        | CallbackTimeout timeoutMs -> 
            $"Callback timeout after {timeoutMs}ms. Please try the authorization again."
        | InvalidCallback reason -> 
            $"Invalid callback received: {reason}"
        | ServerShutdownFailed reason -> 
            $"Failed to shutdown callback server: {reason}"
        | PortAlreadyInUse port -> 
            $"Port {port} is already in use. Please try a different port or close other applications."
    
    /// Validate callback result and extract authorization code
    let extractAuthorizationCode (result: CallbackResult) : Result<AuthorizationCode, string> =
        match result.Error with
        | Some error -> 
            let description = result.ErrorDescription |> Option.defaultValue "No error description provided"
            Error($"OAuth error: {error} - {description}")
        | None ->
            match result.AuthorizationCode with
            | Some code -> Ok code
            | None -> Error("Authorization code not received in callback")
    
    /// Validate state parameter against expected value
    let validateState (expectedState: string) (result: CallbackResult) : Result<unit, string> =
        match result.State with
        | Some receivedState when receivedState = expectedState -> Ok()
        | Some receivedState -> Error($"State parameter mismatch. Expected: {expectedState}, Received: {receivedState}")
        | None -> Error("State parameter not received in callback")
    
    /// Check if port is available for callback server
    let isPortAvailable (port: PortNumber) : bool =
        try
            use listener = new HttpListener()
            let portNum = TypeExtraction.getPortNumber port
            listener.Prefixes.Add($"http://localhost:{portNum}/")
            listener.Start()
            listener.Stop()
            true
        with
        | _ -> false
    
    /// Find next available port starting from given port
    let findAvailablePort (startingPort: PortNumber) (maxAttempts: int) : PortNumber option =
        let startPort = TypeExtraction.getPortNumber startingPort
        
        let rec findPort currentPort attempts =
            if attempts <= 0 then
                None
            else
                match ConstrainedTypes.createPortNumber currentPort with
                | Ok port when isPortAvailable port -> Some port
                | Ok _ -> findPort (currentPort + 1) (attempts - 1)
                | Error _ -> None // Port number out of valid range
        
        findPort startPort maxAttempts
    
    /// Create callback server with automatic port selection
    let createCallbackServerWithAvailablePort (preferredPort: PortNumber) : Result<ICallbackServerService * PortNumber, CallbackServerError> =
        let server = CallbackServerService.create()
        
        match findAvailablePort preferredPort 10 with
        | Some availablePort ->
            let config = createDefaultConfig availablePort
            match server.StartCallbackServer(config) with
            | Ok() -> Ok(server, availablePort)
            | Error err -> Error err
        | None ->
            let port = TypeExtraction.getPortNumber preferredPort
            Error(PortAlreadyInUse port)