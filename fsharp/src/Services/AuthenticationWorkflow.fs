namespace SpotifyCLI.Services

open System
open System.Threading
open System.Threading.Tasks
open SpotifyCLI.Domain

/// Authentication workflow configuration
[<Struct>]
type AuthWorkflowConfig = {
    SpotifyClientId: string
    CallbackPort: PortNumber
    TimeoutMinutes: int
    IncludePlaylistAccess: bool
}

/// Authentication workflow result
[<Struct>]
type AuthWorkflowResult = {
    TokenStorage: TokenStorage
    UserMessage: string
    ConfigPath: string
}

/// Internal workflow state for tracking PKCE and server instances
[<Struct>]
type AuthWorkflowState = {
    CodeVerifier: CodeVerifier
    CodeChallenge: CodeChallenge
    State: string
    CallbackServerRunning: bool
}


/// All services needed for authentication workflow
[<Struct>]
type AuthWorkflowServices = {
    BrowserService: IBrowserService
    CallbackServerService: ICallbackServerService
    OAuthService: IOAuthService
    ConfigService: IConfigService
    CryptoService: ICryptoService
}

/// Authentication workflow service interface
type IAuthenticationWorkflowService =
    abstract StartAuthenticationFlow: AuthWorkflowConfig -> Task<Result<AuthWorkflowResult, AuthWorkflowError>>
    abstract RefreshTokenIfNeeded: TokenStorage -> SpotifyOAuthConfig -> Task<Result<TokenStorage, AuthWorkflowError>>
    abstract ValidateExistingToken: unit -> Result<TokenStorage, AuthWorkflowError>

/// Authentication workflow service implementation
type AuthenticationWorkflowService(services: AuthWorkflowServices) =
    
    /// Create Spotify OAuth configuration from workflow config
    let createOAuthConfig (config: AuthWorkflowConfig) : Result<SpotifyOAuthConfig, AuthWorkflowError> =
        let scopes = OAuthServiceHelpers.getRecommendedScopes config.IncludePlaylistAccess
        
        OAuthServiceHelpers.createSpotifyOAuthConfig config.SpotifyClientId config.CallbackPort
        |> Result.mapError (fun err -> ErrorConversion.oauthErrorToAuthWorkflowError err)
        |> Result.map (fun oauthConfig -> { oauthConfig with Scopes = scopes })
    
    /// Generate PKCE parameters and secure state for OAuth flow
    let generateAuthWorkflowState () : Result<AuthWorkflowState, AuthWorkflowError> =
        // Generate PKCE verifier and challenge
        services.CryptoService.GenerateCodeVerifier()
        |> Result.mapError (fun err -> ErrorConversion.cryptoErrorToAuthWorkflowError err)
        |> Result.bind (fun codeVerifier ->
            services.CryptoService.GenerateCodeChallenge(codeVerifier)
            |> Result.mapError (fun err -> ErrorConversion.cryptoErrorToAuthWorkflowError err)
            |> Result.bind (fun codeChallenge ->
                // Generate secure state parameter
                OAuthServiceHelpers.createTimestampedState services.CryptoService
                |> Result.mapError (fun err -> ErrorConversion.cryptoErrorToAuthWorkflowError err)
                |> Result.map (fun state ->
                    {
                        CodeVerifier = codeVerifier
                        CodeChallenge = codeChallenge
                        State = state
                        CallbackServerRunning = false
                    })))
    
    /// Start callback server and get the port
    let startCallbackServer (port: PortNumber) : Result<PortNumber, AuthWorkflowError> =
        let config = CallbackServerHelpers.createDefaultConfig port
        services.CallbackServerService.StartCallbackServer(config)
        |> Result.mapError (fun err -> ErrorConversion.callbackServerErrorToAuthWorkflowError err)
        |> Result.map (fun _ -> port)
    
    /// Generate authorization URL and launch browser using workflow state
    let launchAuthorizationFlow (oauthConfig: SpotifyOAuthConfig) (workflowState: AuthWorkflowState) : Result<HttpUrl, AuthWorkflowError> =
        services.OAuthService.GenerateAuthorizationUrl oauthConfig workflowState.State
        |> Result.mapError (fun err -> ErrorConversion.oauthErrorToAuthWorkflowError err)
        |> Result.bind (fun authUrl ->
            BrowserServiceHelpers.launchBrowserWithFallback services.BrowserService authUrl
            |> Result.mapError (fun err -> ErrorConversion.browserErrorToAuthWorkflowError err)
            |> Result.map (fun _ -> authUrl))
    
    /// Wait for OAuth callback with timeout
    let waitForCallback (timeoutMinutes: int) : Task<Result<CallbackResult, AuthWorkflowError>> =
        task {
            try
                use cts = new CancellationTokenSource(TimeSpan.FromMinutes(float timeoutMinutes))
                let! callbackResult = services.CallbackServerService.WaitForCallback(cts.Token)
                return callbackResult |> Result.mapError (fun err -> ErrorConversion.callbackServerErrorToAuthWorkflowError err)
            with
            | :? OperationCanceledException ->
                return Error(CallbackTimeoutError timeoutMinutes)
            | ex ->
                return Error(UnexpectedWorkflowError ex.Message)
        }
    
    /// Validate callback result and extract authorization code using workflow state
    let processCallback (workflowState: AuthWorkflowState) (callbackResult: CallbackResult) : Result<AuthorizationCode, AuthWorkflowError> =
        // Validate state parameter
        OAuthServiceHelpers.validateState workflowState.State callbackResult.State
        |> Result.mapError (fun err -> 
            match err with
            | StateValidationFailed(expected, received) -> StateValidationError(expected, received)
            | other -> ErrorConversion.oauthErrorToAuthWorkflowError other)
        |> Result.bind (fun _ ->
            // Extract authorization code
            CallbackServerHelpers.extractAuthorizationCode callbackResult
            |> Result.mapError (fun err -> ConfigurationError($"Callback processing failed: {err}")))
    
    /// Exchange authorization code for tokens using stored PKCE verifier
    let exchangeCodeForTokens (oauthConfig: SpotifyOAuthConfig) (workflowState: AuthWorkflowState) (authCode: AuthorizationCode) : Result<TokenStorage, AuthWorkflowError> =
        // Use the stored PKCE verifier (proper OAuth security)
        services.OAuthService.ExchangeCodeForTokens oauthConfig authCode workflowState.CodeVerifier
        |> Result.mapError (fun err -> ErrorConversion.oauthErrorToAuthWorkflowError err)
    
    /// Save tokens to configuration storage
    let saveTokens (tokenStorage: TokenStorage) : Result<string, AuthWorkflowError> =
        services.ConfigService.EnsureConfigDirectory()
        |> Result.mapError (fun err -> ErrorConversion.configErrorToAuthWorkflowError err)
        |> Result.bind (fun _ ->
            services.ConfigService.WriteTokens(tokenStorage)
            |> Result.mapError (fun err -> ErrorConversion.configErrorToAuthWorkflowError err)
            |> Result.map (fun _ -> services.ConfigService.GetConfigPath()))
    
    /// Stop the callback server and clean up resources
    let stopCallbackServer () : Result<unit, AuthWorkflowError> =
        services.CallbackServerService.StopCallbackServer()
        |> Result.mapError (fun err -> ErrorConversion.callbackServerErrorToAuthWorkflowError err)
    
    /// Complete authentication workflow
    let completeAuthenticationWorkflow (config: AuthWorkflowConfig) : Task<Result<AuthWorkflowResult, AuthWorkflowError>> =
        task {
            try
                // Step 1: Create OAuth configuration
                match createOAuthConfig config with
                | Error err -> return Error err
                | Ok oauthConfig ->
                
                // Step 2: Generate PKCE parameters and secure state
                match generateAuthWorkflowState() with
                | Error err -> return Error err
                | Ok workflowState ->
                
                // Step 3: Start callback server
                match startCallbackServer config.CallbackPort with
                | Error err -> return Error err
                | Ok actualPort ->
                
                // Step 4: Launch browser with authorization URL
                Console.WriteLine("🔐 Starting Spotify OAuth authentication...")
                Console.WriteLine()
                
                match launchAuthorizationFlow oauthConfig workflowState with
                | Error err ->
                    let _ = stopCallbackServer() // Best effort cleanup
                    return Error err
                | Ok authUrl ->
                
                Console.WriteLine("📱 Please complete the authorization in your browser")
                Console.WriteLine($"   Authorization URL: {TypeExtraction.getHttpUrl authUrl}")
                Console.WriteLine($"   Callback server running on port: {TypeExtraction.getPortNumber actualPort}")
                Console.WriteLine()
                Console.WriteLine("⏳ Waiting for authorization callback...")
                
                // Step 5: Wait for callback
                let! callbackResult = waitForCallback config.TimeoutMinutes
                match callbackResult with
                | Error err ->
                    let _ = stopCallbackServer() // Best effort cleanup
                    return Error err
                | Ok callback ->
                
                // Step 6: Process callback and extract authorization code
                match processCallback workflowState callback with
                | Error err ->
                    let _ = stopCallbackServer() // Best effort cleanup
                    return Error err
                | Ok authCode ->
                
                Console.WriteLine("✅ Authorization callback received")
                Console.WriteLine("🔄 Exchanging authorization code for access tokens...")
                
                // Step 7: Exchange code for tokens using stored PKCE verifier
                match exchangeCodeForTokens oauthConfig workflowState authCode with
                | Error err ->
                    let _ = stopCallbackServer() // Best effort cleanup
                    return Error err
                | Ok tokenStorage ->
                
                // Step 8: Save tokens
                match saveTokens tokenStorage with
                | Error err ->
                    let _ = stopCallbackServer() // Best effort cleanup
                    return Error err
                | Ok configPath ->
                
                // Step 9: Clean up callback server
                match stopCallbackServer() with
                | Error err -> return Error err
                | Ok _ ->
                
                // Success!
                let result = {
                    TokenStorage = tokenStorage
                    UserMessage = "🎉 Successfully authenticated with Spotify!"
                    ConfigPath = configPath
                }
                
                return Ok result
            with
            | ex -> 
                let _ = stopCallbackServer() // Best effort cleanup
                return Error(UnexpectedWorkflowError ex.Message)
        }
    
    interface IAuthenticationWorkflowService with
        
        member _.StartAuthenticationFlow(config: AuthWorkflowConfig) =
            completeAuthenticationWorkflow config
        
        member _.RefreshTokenIfNeeded(tokenStorage: TokenStorage) (oauthConfig: SpotifyOAuthConfig) =
            task {
                if OAuthServiceHelpers.isTokenNearExpiry tokenStorage then
                    Console.WriteLine("🔄 Access token is near expiry, refreshing...")
                    
                    let refreshResult = services.OAuthService.RefreshAccessToken oauthConfig tokenStorage.RefreshToken
                    match refreshResult with
                    | Ok newTokenStorage ->
                        // Save the new tokens
                        match saveTokens newTokenStorage with
                        | Ok _ ->
                            Console.WriteLine("✅ Access token refreshed successfully")
                            return Ok newTokenStorage
                        | Error err ->
                            return Error err
                    | Error oauthErr ->
                        return Error(ErrorConversion.oauthErrorToAuthWorkflowError oauthErr)
                else
                    return Ok tokenStorage
            }
        
        member _.ValidateExistingToken() =
            services.ConfigService.ReadTokens()
            |> Result.mapError (fun err -> ErrorConversion.configErrorToAuthWorkflowError err)
            |> Result.bind (fun tokenStorage ->
                if DomainValidation.isTokenExpired tokenStorage then
                    Error(ErrorConversion.configErrorToAuthWorkflowError(ConfigError.InvalidFormat "Token has expired"))
                else
                    Ok tokenStorage)

/// Factory functions for creating authentication workflow service
module AuthenticationWorkflowService =
    
    let create (services: AuthWorkflowServices) : IAuthenticationWorkflowService =
        AuthenticationWorkflowService(services) :> IAuthenticationWorkflowService

/// Authentication workflow utilities and helpers
module AuthenticationWorkflowHelpers =
    
    /// Create default authentication workflow configuration
    let createDefaultConfig (clientId: string) : AuthWorkflowConfig = {
        SpotifyClientId = clientId
        CallbackPort = PortNumber 3000
        TimeoutMinutes = 5
        IncludePlaylistAccess = true
    }
    
    /// Format authentication workflow error for user display (use domain error formatting)
    let formatAuthWorkflowError = ErrorFormatting.formatAuthWorkflowError
    
    /// Check if authentication is required (no valid tokens found)
    let isAuthenticationRequired (workflowService: IAuthenticationWorkflowService) : bool =
        match workflowService.ValidateExistingToken() with
        | Ok _ -> false
        | Error _ -> true
    
    /// Create all required services for authentication workflow
    let createAuthWorkflowServices (configService: IConfigService) (httpService: IHttpService) (cryptoService: ICryptoService) : AuthWorkflowServices = {
        BrowserService = BrowserService.create()
        CallbackServerService = CallbackServerService.create()
        OAuthService = OAuthService.create httpService cryptoService
        ConfigService = configService
        CryptoService = cryptoService
    }
    
    /// Run complete authentication flow with user-friendly output
    let runAuthenticationFlow (clientId: string) (workflowService: IAuthenticationWorkflowService) : Task<Result<AuthWorkflowResult, AuthWorkflowError>> =
        task {
            let config = createDefaultConfig clientId
            
            Console.WriteLine("🎵 Spotify CLI Authentication")
            Console.WriteLine("═══════════════════════════════")
            Console.WriteLine()
            Console.WriteLine("This will open your browser to authenticate with Spotify.")
            Console.WriteLine("Please complete the authorization process and return to this terminal.")
            Console.WriteLine()
            
            let! result = workflowService.StartAuthenticationFlow(config)
            
            match result with
            | Ok authResult ->
                Console.WriteLine()
                Console.WriteLine(authResult.UserMessage)
                Console.WriteLine($"📁 Tokens saved to: {authResult.ConfigPath}")
                Console.WriteLine()
                Console.WriteLine("You can now use other spotify-cli commands!")
                return Ok authResult
            | Error err ->
                Console.WriteLine()
                Console.WriteLine($"❌ Authentication failed: {formatAuthWorkflowError err}")
                Console.WriteLine()
                Console.WriteLine("Please try again or check your network connection.")
                return Error err
        }
    
    /// Get authentication instructions for manual setup
    let getManualAuthInstructions (clientId: string) (port: PortNumber) : string list =
        let portNum = TypeExtraction.getPortNumber port
        [
            "Manual Authentication Setup:"
            "1. Open your web browser"
            "2. Go to: https://accounts.spotify.com/authorize"
            "3. Add these query parameters:"
            $"   - client_id={clientId}"
            "   - response_type=code"
            $"   - redirect_uri=http://127.0.0.1:{portNum}/callback"
            "   - scope=user-read-private user-read-email playlist-read-private"
            "4. Complete the Spotify authorization"
            "5. Copy the authorization code from the callback URL"
            "6. Return to this terminal with the code"
        ]
    
    /// Validate client ID format (basic validation)
    let validateClientId (clientId: string) : Result<string, string> =
        if String.IsNullOrWhiteSpace(clientId) then
            Error "Client ID cannot be empty"
        elif clientId.Length < 10 then
            Error "Client ID appears to be too short (should be from Spotify Developer Dashboard)"
        else
            Ok clientId