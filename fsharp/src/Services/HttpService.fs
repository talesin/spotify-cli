namespace SpotifyCLI.Services

open System
open System.Net.Http
open System.Text
open System.Threading.Tasks
open FsHttp
open FsHttp.Response
open SpotifyCLI.Domain

/// HTTP request configuration
[<Struct>]
type HttpRequest = {
    Url: HttpUrl
    Method: HttpMethod
    Headers: Map<string, string>
    Body: string option
    TimeoutMs: int
}

/// HTTP response with status and content
[<Struct>]
type HttpResponse = {
    StatusCode: int
    Content: string
    Headers: Map<string, string>
    IsSuccess: bool
}

/// HTTP client abstraction following dependency injection pattern
type IHttpClient =
    abstract SendAsync: HttpRequest -> Task<Result<HttpResponse, HttpError>>

/// HTTP service interface for making HTTP requests with domain error handling
type IHttpService =
    abstract Get: HttpUrl -> Map<string, string> -> Result<HttpResponse, HttpError>
    abstract Post: HttpUrl -> string -> Map<string, string> -> Result<HttpResponse, HttpError>
    abstract GetAsync: HttpUrl -> Map<string, string> -> Task<Result<HttpResponse, HttpError>>
    abstract PostAsync: HttpUrl -> string -> Map<string, string> -> Task<Result<HttpResponse, HttpError>>

/// System HTTP client implementation using FsHttp
type SystemHttpClient() =
    
    let defaultTimeoutMs = 30000
    
    /// Convert HttpMethod to FsHttp method
    let mapHttpMethod = function
        | m when m = HttpMethod.Get -> FsHttp.GlobalConfig.Json.get
        | m when m = HttpMethod.Post -> FsHttp.GlobalConfig.Json.post  
        | m when m = HttpMethod.Put -> FsHttp.GlobalConfig.Json.put
        | m when m = HttpMethod.Delete -> FsHttp.GlobalConfig.Json.delete
        | _ -> FsHttp.GlobalConfig.Json.get // Default to GET
    
    /// Map HTTP status codes to domain errors
    let mapStatusCodeToError (statusCode: int) (content: string) =
        match statusCode with
        | 400 -> BadRequest content
        | 401 -> Unauthorized
        | 403 -> Forbidden content
        | 404 -> NotFound
        | 429 ->
            // Try to extract retry-after header value
            RateLimited None
        | code when code >= 500 -> ServerError(code, content)
        | _ -> ServerError(statusCode, $"Unexpected status code: {statusCode}")
    
    interface IHttpClient with
        member _.SendAsync(request: HttpRequest) =
            task {
                try
                    let url = TypeExtraction.getHttpUrl request.Url
                    
                    // Build FsHttp request
                    let httpRequest = 
                        http {
                            (mapHttpMethod request.Method) url
                            
                            // Add headers
                            for header in request.Headers do
                                header header.Key header.Value
                            
                            // Add body if present
                            match request.Body with
                            | Some body when request.Method = HttpMethod.Post || request.Method = HttpMethod.Put ->
                                body body
                            | _ -> ()
                        }
                    
                    // Execute request with timeout
                    let! response = 
                        httpRequest
                        |> Request.timeout (TimeSpan.FromMilliseconds(float request.TimeoutMs))
                        |> Request.sendAsync
                    
                    let! content = response |> Response.toTextAsync
                    
                    let responseHeaders = 
                        response.Headers
                        |> Seq.map (fun h -> h.Key, String.Join(", ", h.Value))
                        |> Map.ofSeq
                    
                    let httpResponse = {
                        StatusCode = int response.StatusCode
                        Content = content
                        Headers = responseHeaders
                        IsSuccess = response.IsSuccessStatusCode
                    }
                    
                    if response.IsSuccessStatusCode then
                        return Ok httpResponse
                    else
                        let error = mapStatusCodeToError (int response.StatusCode) content
                        return Error error
                        
                with
                | :? TaskCanceledException ->
                    return Error(NetworkTimeout request.TimeoutMs)
                | :? HttpRequestException as ex ->
                    return Error(ServerError(0, ex.Message))
                | ex ->
                    return Error(ServerError(0, $"HTTP request failed: {ex.Message}"))
            }

/// HTTP service implementation using dependency injection pattern
type HttpService(httpClient: IHttpClient) =
    
    let defaultHeaders = Map.empty<string, string>
    let defaultTimeoutMs = 30000
    
    /// Execute HTTP request synchronously
    let executeRequest (request: HttpRequest) =
        httpClient.SendAsync(request)
        |> Async.AwaitTask
        |> Async.RunSynchronously
    
    /// Execute HTTP request asynchronously
    let executeRequestAsync (request: HttpRequest) =
        httpClient.SendAsync(request)
    
    interface IHttpService with
        
        member _.Get(url: HttpUrl) (headers: Map<string, string>) =
            let request = {
                Url = url
                Method = HttpMethod.Get
                Headers = headers
                Body = None
                TimeoutMs = defaultTimeoutMs
            }
            executeRequest request
        
        member _.Post(url: HttpUrl) (body: string) (headers: Map<string, string>) =
            let request = {
                Url = url
                Method = HttpMethod.Post
                Headers = headers
                Body = Some body
                TimeoutMs = defaultTimeoutMs
            }
            executeRequest request
        
        member _.GetAsync(url: HttpUrl) (headers: Map<string, string>) =
            let request = {
                Url = url
                Method = HttpMethod.Get
                Headers = headers
                Body = None
                TimeoutMs = defaultTimeoutMs
            }
            executeRequestAsync request
        
        member _.PostAsync(url: HttpUrl) (body: string) (headers: Map<string, string>) =
            let request = {
                Url = url
                Method = HttpMethod.Post
                Headers = headers
                Body = Some body
                TimeoutMs = defaultTimeoutMs
            }
            executeRequestAsync request

/// Factory functions for creating HTTP service following F# coding guide
module HttpService =
    
    let create (httpClient: IHttpClient) : IHttpService =
        HttpService(httpClient) :> IHttpService
    
    let createWithSystemHttpClient () : IHttpService =
        let httpClient = SystemHttpClient() :> IHttpClient
        create httpClient

/// HTTP service utilities and helpers
module HttpServiceHelpers =
    
    /// Create authorization header for Bearer token
    let createAuthorizationHeader (token: AccessToken) : Map<string, string> =
        let tokenValue = TypeExtraction.getAccessToken token
        Map.ofList [ ("Authorization", $"Bearer {tokenValue}") ]
    
    /// Create content-type header for JSON
    let createJsonContentTypeHeader () : Map<string, string> =
        Map.ofList [ ("Content-Type", "application/json") ]
    
    /// Combine multiple header maps
    let combineHeaders (headerMaps: Map<string, string> list) : Map<string, string> =
        headerMaps
        |> List.fold (fun acc headers ->
            Map.fold (fun acc key value -> Map.add key value acc) acc headers) Map.empty
    
    /// Create URL with query parameters
    let createUrlWithQuery (baseUrl: string) (queryParams: (string * string) list) : Result<HttpUrl, string> =
        let queryString = 
            queryParams
            |> List.map (fun (key, value) -> $"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}")
            |> String.concat "&"
        
        let fullUrl = 
            if List.isEmpty queryParams then baseUrl
            else $"{baseUrl}?{queryString}"
        
        ConstrainedTypes.createHttpUrl fullUrl
    
    /// Extract JSON from HTTP response with error handling
    let extractJsonFromResponse (response: HttpResponse) : Result<string, HttpError> =
        if response.IsSuccess then
            if String.IsNullOrWhiteSpace(response.Content) then
                Error(JsonParseError("", "Response content is empty"))
            else
                Ok response.Content
        else
            Error(ServerError(response.StatusCode, response.Content))
    
    /// Check if response indicates rate limiting and extract retry delay
    let extractRateLimitDelay (response: HttpResponse) : int option =
        response.Headers
        |> Map.tryFind "Retry-After"
        |> Option.bind (fun value ->
            match Int32.TryParse(value) with
            | true, seconds -> Some seconds
            | false, _ -> None)