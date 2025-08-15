namespace SpotifyCLI.Tests.Services

open Expecto
open FsCheck
open System.Net.Http
open System.Threading.Tasks
open SpotifyCLI.Domain
open SpotifyCLI.Services
open SpotifyCLI.Tests.TestUtilities

/// Comprehensive tests for HttpService functionality
module HttpServiceTests =
    
    /// Test helpers and utilities
    module TestHelpers =
        
        /// Create test HTTP request
        let createTestRequest (url: string) (method: HttpMethod) (body: string option) (headers: Map<string, string>) = {
            Url = HttpUrl url
            Method = method
            Headers = headers
            Body = body
            TimeoutMs = 30000
        }
        
        /// Create test HTTP response
        let createTestResponse (statusCode: int) (content: string) (headers: Map<string, string>) = {
            StatusCode = statusCode
            Content = content
            Headers = headers
            IsSuccess = statusCode >= 200 && statusCode < 300
        }
        
        /// Mock HTTP client that returns predefined responses
        type MockHttpClient(responses: Map<string * string, Result<HttpResponse, HttpError>>) =
            interface IHttpClient with
                member _.SendAsync(request: HttpRequest) =
                    let url = TypeExtraction.getHttpUrl request.Url
                    let methodString = request.Method.ToString().ToUpperInvariant()
                    let key = (url, methodString)
                    match responses.TryFind(key) with
                    | Some response -> Task.FromResult(response)
                    | None -> Task.FromResult(Error(NotFound))
        
        /// Create HTTP service with mock client
        let createMockHttpService (responses: Map<string * string, Result<HttpResponse, HttpError>>) =
            let mockClient = MockHttpClient(responses) :> IHttpClient
            HttpService.create mockClient
    
    /// Tests for HTTP request creation and execution
    module HttpRequestTests =
        
        [<Tests>]
        let tests = testList "HttpRequest" [
            
            test "should create valid GET request" {
                let url = "https://api.spotify.com/v1/me"
                let headers = Map.ofList [("Authorization", "Bearer token123")]
                let request = TestHelpers.createTestRequest url HttpMethod.Get None headers
                
                let urlValue = TypeExtraction.getHttpUrl request.Url
                Expect.equal urlValue url "Should have correct URL"
                Expect.equal request.Method HttpMethod.Get "Should have GET method"
                Expect.isNone request.Body "Should have no body"
                Expect.equal request.Headers headers "Should have correct headers"
            }
            
            test "should create valid POST request with body" {
                let url = "https://api.spotify.com/v1/playlists"
                let body = """{"name": "Test Playlist"}"""
                let headers = Map.ofList [
                    ("Authorization", "Bearer token123")
                    ("Content-Type", "application/json")
                ]
                let request = TestHelpers.createTestRequest url HttpMethod.Post (Some body) headers
                
                Expect.equal request.Method HttpMethod.Post "Should have POST method"
                Expect.equal request.Body (Some body) "Should have correct body"
                Expect.equal request.Headers headers "Should have correct headers"
            }
        ]
    
    /// Tests for HTTP response handling
    module HttpResponseTests =
        
        [<Tests>]
        let tests = testList "HttpResponse" [
            
            test "should identify successful response" {
                let response = TestHelpers.createTestResponse 200 """{"status": "ok"}""" Map.empty
                
                Expect.isTrue response.IsSuccess "Should identify 200 as success"
                Expect.equal response.StatusCode 200 "Should have correct status code"
                Expect.equal response.Content """{"status": "ok"}""" "Should have correct content"
            }
            
            test "should identify error response" {
                let response = TestHelpers.createTestResponse 404 "Not Found" Map.empty
                
                Expect.isFalse response.IsSuccess "Should identify 404 as error"
                Expect.equal response.StatusCode 404 "Should have correct status code"
                Expect.equal response.Content "Not Found" "Should have correct content"
            }
            
            test "should handle response headers" {
                let headers = Map.ofList [
                    ("Content-Type", "application/json")
                    ("X-Rate-Limit-Remaining", "100")
                ]
                let response = TestHelpers.createTestResponse 200 "test" headers
                
                Expect.equal response.Headers headers "Should preserve response headers"
            }
        ]
    
    /// Tests for HttpService GET operations
    module HttpServiceGetTests =
        
        [<Tests>]
        let tests = testList "HttpService GET" [
            
            test "should successfully execute GET request" {
                let url = "https://api.spotify.com/v1/me"
                let expectedResponse = TestHelpers.createTestResponse 200 """{"id": "user123"}""" Map.empty
                let responses = Map.ofList [
                    ((url, HttpMethod.Get), Ok expectedResponse)
                ]
                let service = TestHelpers.createMockHttpService responses
                
                let result = service.Get (HttpUrl url) Map.empty
                
                match result with
                | Ok response ->
                    Expect.equal response.StatusCode 200 "Should have success status"
                    Expect.equal response.Content """{"id": "user123"}""" "Should have correct content"
                | Error error -> failtestf $"Expected success but got error: %A" error
            }
            
            test "should handle GET request with headers" {
                let url = "https://api.spotify.com/v1/me"
                let headers = Map.ofList [("Authorization", "Bearer token123")]
                let expectedResponse = TestHelpers.createTestResponse 200 """{"id": "user123"}""" Map.empty
                let responses = Map.ofList [
                    ((url, HttpMethod.Get), Ok expectedResponse)
                ]
                let service = TestHelpers.createMockHttpService responses
                
                let result = service.Get (HttpUrl url) headers
                
                match result with
                | Ok response -> Expect.isTrue response.IsSuccess "Should succeed with headers"
                | Error error -> failtestf $"Expected success but got error: %A" error
            }
            
            test "should handle GET request errors" {
                let url = "https://api.spotify.com/v1/nonexistent"
                let responses = Map.ofList [
                    ((url, HttpMethod.Get), Error NotFound)
                ]
                let service = TestHelpers.createMockHttpService responses
                
                let result = service.Get (HttpUrl url) Map.empty
                
                match result with
                | Ok _ -> failtest "Expected error for non-existent resource"
                | Error NotFound -> () // Expected
                | Error error -> failtestf $"Expected NotFound but got: %A" error
            }
            
            test "should handle unauthorized GET request" {
                let url = "https://api.spotify.com/v1/me"
                let responses = Map.ofList [
                    ((url, HttpMethod.Get), Error Unauthorized)
                ]
                let service = TestHelpers.createMockHttpService responses
                
                let result = service.Get (HttpUrl url) Map.empty
                
                match result with
                | Ok _ -> failtest "Expected error for unauthorized request"
                | Error Unauthorized -> () // Expected
                | Error error -> failtestf $"Expected Unauthorized but got: %A" error
            }
        ]
    
    /// Tests for HttpService POST operations
    module HttpServicePostTests =
        
        [<Tests>]
        let tests = testList "HttpService POST" [
            
            test "should successfully execute POST request" {
                let url = "https://api.spotify.com/v1/playlists"
                let body = """{"name": "Test Playlist"}"""
                let expectedResponse = TestHelpers.createTestResponse 201 """{"id": "playlist123"}""" Map.empty
                let responses = Map.ofList [
                    ((url, HttpMethod.Post), Ok expectedResponse)
                ]
                let service = TestHelpers.createMockHttpService responses
                
                let result = service.Post (HttpUrl url) body Map.empty
                
                match result with
                | Ok response ->
                    Expect.equal response.StatusCode 201 "Should have created status"
                    Expect.equal response.Content """{"id": "playlist123"}""" "Should have correct content"
                | Error error -> failtestf $"Expected success but got error: %A" error
            }
            
            test "should handle POST request with headers" {
                let url = "https://api.spotify.com/v1/playlists"
                let body = """{"name": "Test Playlist"}"""
                let headers = Map.ofList [
                    ("Authorization", "Bearer token123")
                    ("Content-Type", "application/json")
                ]
                let expectedResponse = TestHelpers.createTestResponse 201 """{"id": "playlist123"}""" Map.empty
                let responses = Map.ofList [
                    ((url, HttpMethod.Post), Ok expectedResponse)
                ]
                let service = TestHelpers.createMockHttpService responses
                
                let result = service.Post (HttpUrl url) body headers
                
                match result with
                | Ok response -> Expect.isTrue response.IsSuccess "Should succeed with headers"
                | Error error -> failtestf $"Expected success but got error: %A" error
            }
            
            test "should handle POST request with bad request error" {
                let url = "https://api.spotify.com/v1/playlists"
                let body = """{"invalid": "json"}"""
                let responses = Map.ofList [
                    ((url, HttpMethod.Post), Error (BadRequest "Invalid request body"))
                ]
                let service = TestHelpers.createMockHttpService responses
                
                let result = service.Post (HttpUrl url) body Map.empty
                
                match result with
                | Ok _ -> failtest "Expected error for bad request"
                | Error (BadRequest _) -> () // Expected
                | Error error -> failtestf $"Expected BadRequest but got: %A" error
            }
            
            test "should handle POST request server error" {
                let url = "https://api.spotify.com/v1/playlists"
                let body = """{"name": "Test"}"""
                let responses = Map.ofList [
                    ((url, HttpMethod.Post), Error (ServerError(500, "Internal Server Error")))
                ]
                let service = TestHelpers.createMockHttpService responses
                
                let result = service.Post (HttpUrl url) body Map.empty
                
                match result with
                | Ok _ -> failtest "Expected error for server error"
                | Error (ServerError (500, "Internal Server Error")) -> () // Expected
                | Error error -> failtestf $"Expected ServerError but got: %A" error
            }
        ]
    
    /// Tests for HttpServiceHelpers functionality
    module HttpServiceHelpersTests =
        
        [<Tests>]
        let tests = testList "HttpServiceHelpers" [
            
            test "createAuthorizationHeader should create Bearer header" {
                let token = AccessToken "test_token_123"
                
                let headers = HttpServiceHelpers.createAuthorizationHeader token
                
                match headers.TryFind("Authorization") with
                | Some authHeader -> Expect.equal authHeader "Bearer test_token_123" "Should create correct Bearer header"
                | None -> failtest "Should create Authorization header"
            }
            
            test "createJsonContentTypeHeader should create JSON content type" {
                let headers = HttpServiceHelpers.createJsonContentTypeHeader()
                
                match headers.TryFind("Content-Type") with
                | Some contentType -> Expect.equal contentType "application/json" "Should create JSON content type"
                | None -> failtest "Should create Content-Type header"
            }
            
            test "combineHeaders should merge multiple header maps" {
                let authHeaders = Map.ofList [("Authorization", "Bearer token")]
                let contentHeaders = Map.ofList [("Content-Type", "application/json")]
                let customHeaders = Map.ofList [("X-Custom", "value")]
                
                let combined = HttpServiceHelpers.combineHeaders [authHeaders; contentHeaders; customHeaders]
                
                Expect.equal combined.Count 3 "Should have all headers"
                Expect.equal combined.["Authorization"] "Bearer token" "Should have auth header"
                Expect.equal combined.["Content-Type"] "application/json" "Should have content type"
                Expect.equal combined.["X-Custom"] "value" "Should have custom header"
            }
            
            test "combineHeaders should handle overlapping keys with later values winning" {
                let headers1 = Map.ofList [("Key", "value1")]
                let headers2 = Map.ofList [("Key", "value2")]
                
                let combined = HttpServiceHelpers.combineHeaders [headers1; headers2]
                
                Expect.equal combined.["Key"] "value2" "Later header value should win"
            }
            
            test "createUrlWithQuery should create URL without query parameters" {
                let baseUrl = "https://api.spotify.com/v1/me"
                let queryParams = []
                
                let result = HttpServiceHelpers.createUrlWithQuery baseUrl queryParams
                
                match result with
                | Ok (HttpUrl url) -> Expect.equal url baseUrl "Should return base URL unchanged"
                | Error error -> failtestf $"Expected success but got error: {error}"
            }
            
            test "createUrlWithQuery should create URL with query parameters" {
                let baseUrl = "https://api.spotify.com/v1/playlists"
                let queryParams = [
                    ("limit", "20")
                    ("offset", "0")
                    ("market", "US")
                ]
                
                let result = HttpServiceHelpers.createUrlWithQuery baseUrl queryParams
                
                match result with
                | Ok (HttpUrl url) ->
                    Expect.stringContains url "https://api.spotify.com/v1/playlists?" "Should contain base URL with query separator"
                    Expect.stringContains url "limit=20" "Should contain limit parameter"
                    Expect.stringContains url "offset=0" "Should contain offset parameter"
                    Expect.stringContains url "market=US" "Should contain market parameter"
                | Error error -> failtestf $"Expected success but got error: {error}"
            }
            
            test "createUrlWithQuery should URL encode query parameters" {
                let baseUrl = "https://api.spotify.com/v1/search"
                let queryParams = [
                    ("q", "artist:\"The Beatles\"")
                    ("type", "track,album")
                ]
                
                let result = HttpServiceHelpers.createUrlWithQuery baseUrl queryParams
                
                match result with
                | Ok (HttpUrl url) ->
                    Expect.stringContains url "q=artist%3A%22The%20Beatles%22" "Should URL encode query values"
                    Expect.stringContains url "type=track%2Calbum" "Should URL encode comma"
                | Error error -> failtestf $"Expected success but got error: {error}"
            }
            
            test "extractJsonFromResponse should extract JSON from successful response" {
                let response = TestHelpers.createTestResponse 200 """{"id": "test123"}""" Map.empty
                
                let result = HttpServiceHelpers.extractJsonFromResponse response
                
                match result with
                | Ok json -> Expect.equal json """{"id": "test123"}""" "Should extract JSON content"
                | Error error -> failtestf $"Expected success but got error: %A" error
            }
            
            test "extractJsonFromResponse should fail for empty response content" {
                let response = TestHelpers.createTestResponse 200 "" Map.empty
                
                let result = HttpServiceHelpers.extractJsonFromResponse response
                
                match result with
                | Ok _ -> failtest "Expected error for empty content"
                | Error (JsonParseError ("", "Response content is empty")) -> () // Expected
                | Error error -> failtestf $"Expected JsonParseError but got: %A" error
            }
            
            test "extractJsonFromResponse should fail for error response" {
                let response = TestHelpers.createTestResponse 500 "Internal Server Error" Map.empty
                
                let result = HttpServiceHelpers.extractJsonFromResponse response
                
                match result with
                | Ok _ -> failtest "Expected error for server error response"
                | Error (ServerError (500, "Internal Server Error")) -> () // Expected
                | Error error -> failtestf $"Expected ServerError but got: %A" error
            }
            
            test "extractRateLimitDelay should extract retry delay from headers" {
                let headers = Map.ofList [("Retry-After", "60")]
                let response = TestHelpers.createTestResponse 429 "Rate limited" headers
                
                let delay = HttpServiceHelpers.extractRateLimitDelay response
                
                match delay with
                | Some 60 -> () // Expected
                | Some other -> failtestf $"Expected 60 seconds but got: {other}"
                | None -> failtest "Expected retry delay to be extracted"
            }
            
            test "extractRateLimitDelay should return None when header missing" {
                let response = TestHelpers.createTestResponse 429 "Rate limited" Map.empty
                
                let delay = HttpServiceHelpers.extractRateLimitDelay response
                
                match delay with
                | None -> () // Expected
                | Some value -> failtestf $"Expected None but got: {value}"
            }
            
            test "extractRateLimitDelay should return None for invalid header value" {
                let headers = Map.ofList [("Retry-After", "invalid")]
                let response = TestHelpers.createTestResponse 429 "Rate limited" headers
                
                let delay = HttpServiceHelpers.extractRateLimitDelay response
                
                match delay with
                | None -> () // Expected
                | Some value -> failtestf $"Expected None but got: {value}"
            }
        ]
    
    /// Tests for error status code mapping
    module ErrorStatusCodeTests =
        
        [<Tests>]
        let tests = testList "Error Status Code Mapping" [
            
            test "should map 400 to BadRequest" {
                let responses = Map.ofList [
                    (("https://test.com", HttpMethod.Get), Error (BadRequest "Bad request"))
                ]
                let service = TestHelpers.createMockHttpService responses
                
                let result = service.Get (HttpUrl "https://test.com") Map.empty
                
                match result with
                | Error (BadRequest "Bad request") -> () // Expected
                | _ -> failtest "Should map 400 to BadRequest"
            }
            
            test "should map 401 to Unauthorized" {
                let responses = Map.ofList [
                    (("https://test.com", HttpMethod.Get), Error Unauthorized)
                ]
                let service = TestHelpers.createMockHttpService responses
                
                let result = service.Get (HttpUrl "https://test.com") Map.empty
                
                match result with
                | Error Unauthorized -> () // Expected
                | _ -> failtest "Should map 401 to Unauthorized"
            }
            
            test "should map 403 to Forbidden" {
                let responses = Map.ofList [
                    (("https://test.com", HttpMethod.Get), Error (Forbidden "Forbidden"))
                ]
                let service = TestHelpers.createMockHttpService responses
                
                let result = service.Get (HttpUrl "https://test.com") Map.empty
                
                match result with
                | Error (Forbidden "Forbidden") -> () // Expected
                | _ -> failtest "Should map 403 to Forbidden"
            }
            
            test "should map 404 to NotFound" {
                let responses = Map.ofList [
                    (("https://test.com", HttpMethod.Get), Error NotFound)
                ]
                let service = TestHelpers.createMockHttpService responses
                
                let result = service.Get (HttpUrl "https://test.com") Map.empty
                
                match result with
                | Error NotFound -> () // Expected
                | _ -> failtest "Should map 404 to NotFound"
            }
            
            test "should map 429 to RateLimited" {
                let responses = Map.ofList [
                    (("https://test.com", HttpMethod.Get), Error (RateLimited None))
                ]
                let service = TestHelpers.createMockHttpService responses
                
                let result = service.Get (HttpUrl "https://test.com") Map.empty
                
                match result with
                | Error (RateLimited None) -> () // Expected
                | _ -> failtest "Should map 429 to RateLimited"
            }
            
            test "should map 500+ to ServerError" {
                let responses = Map.ofList [
                    (("https://test.com", HttpMethod.Get), Error (ServerError(500, "Internal Server Error")))
                ]
                let service = TestHelpers.createMockHttpService responses
                
                let result = service.Get (HttpUrl "https://test.com") Map.empty
                
                match result with
                | Error (ServerError (500, "Internal Server Error")) -> () // Expected
                | _ -> failtest "Should map 500 to ServerError"
            }
        ]
    
    /// Tests for async operations
    module AsyncOperationTests =
        
        [<Tests>]
        let tests = testList "Async Operations" [
            
            test "GetAsync should work correctly" {
                let url = "https://api.spotify.com/v1/me"
                let expectedResponse = TestHelpers.createTestResponse 200 """{"id": "user123"}""" Map.empty
                let responses = Map.ofList [
                    ((url, HttpMethod.Get), Ok expectedResponse)
                ]
                let service = TestHelpers.createMockHttpService responses
                
                let asyncResult = service.GetAsync (HttpUrl url) Map.empty
                let result = Async.AwaitTask asyncResult |> Async.RunSynchronously
                
                match result with
                | Ok response ->
                    Expect.equal response.StatusCode 200 "Should have success status"
                    Expect.equal response.Content """{"id": "user123"}""" "Should have correct content"
                | Error error -> failtestf $"Expected success but got error: %A" error
            }
            
            test "PostAsync should work correctly" {
                let url = "https://api.spotify.com/v1/playlists"
                let body = """{"name": "Test Playlist"}"""
                let expectedResponse = TestHelpers.createTestResponse 201 """{"id": "playlist123"}""" Map.empty
                let responses = Map.ofList [
                    ((url, HttpMethod.Post), Ok expectedResponse)
                ]
                let service = TestHelpers.createMockHttpService responses
                
                let asyncResult = service.PostAsync (HttpUrl url) body Map.empty
                let result = Async.AwaitTask asyncResult |> Async.RunSynchronously
                
                match result with
                | Ok response ->
                    Expect.equal response.StatusCode 201 "Should have created status"
                    Expect.equal response.Content """{"id": "playlist123"}""" "Should have correct content"
                | Error error -> failtestf $"Expected success but got error: %A" error
            }
        ]
    
    /// Combined test suite
    [<Tests>]
    let allTests = testList "HttpServiceTests" [
        HttpRequestTests.tests
        HttpResponseTests.tests
        HttpServiceGetTests.tests
        HttpServicePostTests.tests
        HttpServiceHelpersTests.tests
        ErrorStatusCodeTests.tests
        AsyncOperationTests.tests
    ]