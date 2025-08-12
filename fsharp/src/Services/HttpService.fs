namespace SpotifyCLI.Services

open System
open System.Net.Http
open System.Text
open System.Threading.Tasks
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

/// System HTTP client implementation using standard HttpClient
type SystemHttpClient() =
    
    let httpClient = new HttpClient()
    
    /// Map HTTP status codes to domain errors
    let mapStatusCodeToError (statusCode: int) (content: string) =
        match statusCode with
        | 400 -> BadRequest content
        | 401 -> Unauthorized
        | 403 -> Forbidden content
        | 404 -> NotFound
        | 429 -> RateLimited None
        | code when code >= 500 -> ServerError(code, content)
        | _ -> ServerError(statusCode, $"Unexpected status code: {statusCode}")
    
    interface IHttpClient with
        member _.SendAsync(request: HttpRequest) =
            task {
                try
                    let url = TypeExtraction.getHttpUrl request.Url
                    
                    // Create HttpRequestMessage
                    use httpRequestMessage = new HttpRequestMessage(request.Method, url)
                    
                    // Set timeout
                    httpClient.Timeout <- TimeSpan.FromMilliseconds(float request.TimeoutMs)
                    
                    // Add headers
                    for kvp in request.Headers do
                        httpRequestMessage.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value) |> ignore
                    
                    // Add body if present
                    match request.Body with
                    | Some body when request.Method = HttpMethod.Post || request.Method = HttpMethod.Put ->
                        httpRequestMessage.Content <- new StringContent(body, Encoding.UTF8, "application/json")
                    | _ -> ()
                    
                    // Send request
                    let! response = httpClient.SendAsync(httpRequestMessage)
                    let! content = response.Content.ReadAsStringAsync()
                    
                    let responseHeaders = 
                        response.Headers
                        |> Seq.map (fun h -> h.Key, String.concat ", " h.Value)
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
    
    interface IDisposable with
        member _.Dispose() = 
            httpClient.Dispose()

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
        let httpClient = new SystemHttpClient() :> IHttpClient
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