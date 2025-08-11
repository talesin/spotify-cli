namespace SpotifyCLI.Tests

open Expecto
open FsCheck
open SpotifyCLI.Domain
open SpotifyCLI.Services

/// Test utilities and shared fixtures
module TestUtilities =
    
    /// FsCheck generators for domain types
    module Generators =
        
        /// Generate valid email addresses
        let validEmail = 
            gen {
                let! username = Gen.alphaNumericString |> Gen.suchThat (fun s -> s.Length > 0 && s.Length <= 20)
                let! domain = Gen.alphaNumericString |> Gen.suchThat (fun s -> s.Length > 0 && s.Length <= 20)
                let! tld = Gen.elements ["com"; "net"; "org"; "edu"]
                return $"{username}@{domain}.{tld}"
            }
        
        /// Generate valid Spotify URIs
        let validSpotifyUri =
            gen {
                let! entityType = Gen.elements ["user"; "track"; "album"; "playlist"]
                let! id = Gen.alphaNumericString |> Gen.suchThat (fun s -> s.Length = 22)
                return $"spotify:{entityType}:{id}"
            }
        
        /// Generate valid String50 values
        let validString50 =
            Gen.alphaNumericString 
            |> Gen.suchThat (fun s -> s.Length > 0 && s.Length <= 50)
        
        /// Generate valid track counts
        let validTrackCount =
            Gen.choose(0, 10000)
        
        /// Generate valid port numbers
        let validPortNumber =
            Gen.choose(1, 65535)
        
        /// Generate UserProfile with valid data
        let validUserProfile =
            gen {
                let! email = validEmail
                let! uri = validSpotifyUri
                let! displayName = Gen.option validString50
                let! country = Gen.option (Gen.alphaNumericString |> Gen.suchThat (fun s -> s.Length = 2))
                let! id = Gen.alphaNumericString |> Gen.suchThat (fun s -> s.Length > 0)
                let! followers = Gen.option (Gen.choose(0, 1000000))
                
                return {
                    DisplayName = displayName |> Option.map String50
                    Email = EmailAddress email
                    Country = country
                    SpotifyUri = SpotifyUri uri
                    Id = id
                    Followers = followers
                }
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
        
        /// Mock HTTP client for testing
        type MockHttpClient(responses: Map<string, Result<HttpResponse, HttpError>>) =
            interface IHttpClient with
                member _.SendAsync(request: HttpRequest) =
                    let url = TypeExtraction.getHttpUrl request.Url
                    match responses.TryFind(url) with
                    | Some response -> System.Threading.Tasks.Task.FromResult(response)
                    | None -> System.Threading.Tasks.Task.FromResult(Error(NotFound))
        
        /// Mock crypto service for testing
        type MockCryptoService(fixedValues: Map<string, string>) =
            interface ICryptoService with
                member _.GenerateRandomString(length: int) =
                    match fixedValues.TryFind("randomString") with
                    | Some value -> Ok value
                    | None -> Ok(String.replicate length "A")
                
                member _.GenerateCodeVerifier() =
                    match fixedValues.TryFind("codeVerifier") with
                    | Some value -> Ok(CodeVerifier value)
                    | None -> Ok(CodeVerifier "test-code-verifier-1234567890123456789012")
                
                member _.GenerateCodeChallenge(verifier: CodeVerifier) =
                    match fixedValues.TryFind("codeChallenge") with
                    | Some value -> Ok(CodeChallenge value)
                    | None -> Ok(CodeChallenge "test-code-challenge-1234567890")
                
                member _.GenerateState() =
                    match fixedValues.TryFind("state") with
                    | Some value -> Ok value
                    | None -> Ok "test-state-12345678901234567890"

    /// Test data factories
    module TestData =
        
        /// Create sample token storage
        let sampleTokenStorage = {
            AccessToken = AccessToken "sample-access-token"
            RefreshToken = RefreshToken "sample-refresh-token"
            ExpiresAt = System.DateTime.UtcNow.AddHours(1.0)
            TokenType = "Bearer"
        }
        
        /// Create expired token storage
        let expiredTokenStorage = {
            sampleTokenStorage with
                ExpiresAt = System.DateTime.UtcNow.AddHours(-1.0)
        }
        
        /// Create sample user profile
        let sampleUserProfile = {
            DisplayName = Some(String50 "Test User")
            Email = EmailAddress "test@example.com"
            Country = Some "US"
            SpotifyUri = SpotifyUri "spotify:user:testuser"
            Id = "testuser123"
            Followers = Some 42
        }
        
        /// Create sample playlist
        let samplePlaylist = {
            Name = String50 "My Playlist"
            TrackCount = TrackCount 25
            Visibility = Public
            Id = "playlist123"
            SpotifyUri = SpotifyUri "spotify:playlist:playlist123"
            Description = Some "A test playlist"
        }

    /// Assertion helpers
    module Assertions =
        
        /// Assert that Result is Ok with expected value
        let assertResultOk (expected: 'a) (actual: Result<'a, 'b>) =
            match actual with
            | Ok value -> Expect.equal value expected "Result should be Ok with expected value"
            | Error error -> failtestf $"Expected Ok but got Error: %A" error
        
        /// Assert that Result is Error
        let assertResultError (actual: Result<'a, 'b>) =
            match actual with
            | Ok value -> failtestf $"Expected Error but got Ok: %A" value
            | Error _ -> () // Success
        
        /// Assert that Result is Error with specific error type
        let assertResultErrorWith<'TError when 'TError : equality> 
            (expectedError: 'TError) 
            (actual: Result<'a, 'TError>) =
            match actual with
            | Ok value -> failtestf $"Expected Error but got Ok: %A" value
            | Error error -> Expect.equal error expectedError "Error should match expected error"

/// Property-based testing configuration
module PropertyTestConfig =
    
    /// Standard configuration for property-based tests
    let defaultConfig = { FsCheckConfig.defaultConfig with maxTest = 100; arbitrary = [] }
    
    /// Fast configuration for quick tests
    let fastConfig = { defaultConfig with maxTest = 10 }
    
    /// Thorough configuration for comprehensive tests
    let thoroughConfig = { defaultConfig with maxTest = 1000 }