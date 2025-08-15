namespace SpotifyCLI.Tests

open SpotifyCLI.Domain
open SpotifyCLI.Services

/// Test utilities and shared fixtures
module TestUtilities =
    open FsCheck.FSharp

    let (===) left right = left = right |> Prop.label (sprintf "%A = %A" left right)

    
    /// Test data factories
    module TestData =
        
        /// Create sample token storage
        let sampleTokenStorage : SpotifyCLI.Domain.TokenStorage = {
            AccessToken = AccessToken "sample-access-token"
            RefreshToken = RefreshToken "sample-refresh-token"  
            ExpiresAt = System.DateTime.UtcNow.AddHours(1.0)
            TokenType = "Bearer"
        }
        
        /// Create expired token storage
        let expiredTokenStorage : SpotifyCLI.Domain.TokenStorage = {
            sampleTokenStorage with
                ExpiresAt = System.DateTime.UtcNow.AddHours(-1.0)
        }
        
        /// Create sample user profile
        let sampleUserProfile = {
            DisplayName = ConstrainedTypes.createString50 "Test User" |> Result.toOption
            Email = ConstrainedTypes.createEmailAddress "test@example.com" |> Result.defaultValue (EmailAddress "default@test.com")
            Country = Some "US"
            SpotifyUri = ConstrainedTypes.createSpotifyUri "spotify:user:testuser" |> Result.defaultValue (SpotifyUri "spotify:user:default")
            Id = "testuser123"
            Followers = Some 42
        }
        
        /// Create sample playlist
        let samplePlaylist = {
            Name = ConstrainedTypes.createPlaylistName "My Playlist" |> Result.defaultValue (PlaylistName "Default Playlist")
            TrackCount = TrackCount 25
            Visibility = Public
            Id = "playlist123"
            SpotifyUri = ConstrainedTypes.createSpotifyUri "spotify:playlist:playlist123" |> Result.defaultValue (SpotifyUri "spotify:playlist:default")
            Description = Some "A test playlist"
        }

    /// Mock implementations for testing
    module Mocks =
        
        /// Mock file system for testing
        type MockFileSystem(files: Map<string, string>, directories: Set<string>) =
            interface IFileSystem with
                member _.ReadAllText(path: string) =
                    match files.TryFind(path) with
                    | Some content -> Ok content
                    | None -> Error(PathNotFound path)
                
                member _.WriteAllText(path: string, content: string) =
                    Ok() // Always succeed for mock
                
                member _.DirectoryExists(path: string) =
                    directories.Contains(path)
                
                member _.CreateDirectory(path: string) =
                    Ok() // Always succeed for mock
                
                member _.FileExists(path: string) =
                    files.ContainsKey(path)
        
        /// Mock crypto service for testing
        type MockCryptoService(fixedValues: Map<string, string>) =
            interface ICryptoService with
                member _.GenerateRandomString(length: int) =
                    match fixedValues.TryFind("randomString") with
                    | Some value -> Ok value
                    | None -> Ok (String.replicate length "A")
                
                member _.GenerateCodeVerifier() =
                    match fixedValues.TryFind("codeVerifier") with
                    | Some value -> Ok (CodeVerifier value)
                    | None -> Ok (CodeVerifier "test-code-verifier-1234567890123456789012")
                
                member _.GenerateCodeChallenge(verifier: CodeVerifier) =
                    match fixedValues.TryFind("codeChallenge") with
                    | Some value -> Ok (CodeChallenge value)
                    | None -> Ok (CodeChallenge "test-code-challenge-1234567890")
                
                member _.GenerateState() =
                    match fixedValues.TryFind("state") with
                    | Some value -> Ok value
                    | None -> Ok "test-state-12345678901234567890"
        
        /// Mock HTTP client for testing
        type MockHttpClient(responses: Map<string * string, Result<HttpResponse, HttpError>>) =
            interface IHttpClient with
                member _.SendAsync(request: HttpRequest) =
                    let url = TypeExtraction.getHttpUrl request.Url
                    let methodString = request.Method.ToString().ToUpperInvariant()
                    let key = (url, methodString)
                    match responses.TryFind(key) with
                    | Some response -> System.Threading.Tasks.Task.FromResult(response)
                    | None -> System.Threading.Tasks.Task.FromResult(Error(NotFound))

    /// Assertion helpers
    module Assertions =
        
        /// Assert that Result is Ok with expected value
        let assertResultOk (expected: 'a) (actual: Result<'a, 'b>) =
            match actual with
            | Ok value -> 
                if value = expected then
                    true
                else 
                    failwithf "Expected Ok(%A) but got Ok(%A)" expected value
            | Error error -> failwithf "Expected Ok but got Error: %A" error
        
        /// Assert that Result is Error
        let assertResultError (actual: Result<'a, 'b>) =
            match actual with
            | Ok value -> failwithf "Expected Error but got Ok: %A" value
            | Error _ -> true // Success
        
        /// Assert that Result is Error with specific error type
        let assertResultErrorWith<'a, 'TError when 'TError : equality and 'a : equality> 
            (expectedError: 'TError) 
            (actual: Result<'a, 'TError>) =
            match actual with
            | Ok value -> failwithf "Expected Error but got Ok: %A" value
            | Error error -> 
                if error = expectedError then
                    true
                else
                    failwithf "Expected Error(%A) but got Error(%A)" expectedError error