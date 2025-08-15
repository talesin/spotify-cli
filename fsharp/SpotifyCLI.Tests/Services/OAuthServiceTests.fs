namespace SpotifyCLI.Tests.Services

open Expecto
open FsCheck
open System.Collections.Generic
open System.Text.Json
open SpotifyCLI.Domain
open SpotifyCLI.Services
open SpotifyCLI.Tests.TestUtilities

/// Comprehensive tests for OAuthService functionality
module OAuthServiceTests =
    
    /// Test helpers and utilities
    module TestHelpers =
        
        /// Create test OAuth configuration
        let createTestOAuthConfig () = {
            ClientId = "test_client_id"
            ClientSecret = "test_client_secret"
            RedirectUri = HttpUrl "http://127.0.0.1:8888/callback"
            Scopes = ["user-read-private"; "user-read-email"]
            AuthorizationBaseUrl = HttpUrl "https://accounts.spotify.com/authorize"
            TokenBaseUrl = HttpUrl "https://accounts.spotify.com/api/token"
        }
        
        /// Create invalid OAuth configuration for testing
        let createInvalidOAuthConfig () = {
            ClientId = ""
            ClientSecret = ""
            RedirectUri = HttpUrl "invalid://url"
            Scopes = []
            AuthorizationBaseUrl = HttpUrl "https://accounts.spotify.com/authorize"
            TokenBaseUrl = HttpUrl "https://accounts.spotify.com/api/token"
        }
        
        /// Create mock successful token response JSON
        let createSuccessTokenResponseJson () =
            """{
                "access_token": "test_access_token",
                "token_type": "Bearer",
                "expires_in": 3600,
                "refresh_token": "test_refresh_token",
                "scope": "user-read-private user-read-email"
            }"""
        
        /// Create mock error token response JSON
        let createErrorTokenResponseJson () =
            """{
                "error": "invalid_grant",
                "error_description": "Authorization code expired"
            }"""
        
        /// Create mock OAuth service with predefined responses
        let createMockOAuthService (responses: Map<string, Result<string, HttpError>>) =
            let mockHttpResponses = 
                responses 
                |> Map.toSeq
                |> Seq.map (fun (url, result) -> 
                    let httpResult = result 
                                   |> Result.map (fun json -> { StatusCode = 200; Content = json; Headers = Map.empty; IsSuccess = true })
                                   |> Result.mapError id
                    ((url, "POST"), httpResult))
                |> Map.ofSeq
            let mockHttpService = Mocks.MockHttpClient(mockHttpResponses) :> IHttpClient
            let httpService = HttpService.create mockHttpService
            let cryptoService = CryptoService.create()
            OAuthService.create httpService cryptoService
    
    /// Tests for ValidateConfiguration functionality
    module ValidateConfigurationTests =
        
        [<Tests>]
        let tests = testList "ValidateConfiguration" [
            
            test "should succeed with valid configuration" {
                let service = TestHelpers.createMockOAuthService Map.empty
                let config = TestHelpers.createTestOAuthConfig()
                
                let result = service.ValidateConfiguration(config)
                
                match result with
                | Ok () -> () // Expected
                | Error error -> failtestf "Expected success but got error: %A" error
            }
            
            test "should fail with empty client ID" {
                let service = TestHelpers.createMockOAuthService Map.empty
                let config = { TestHelpers.createTestOAuthConfig() with ClientId = "" }
                
                let result = service.ValidateConfiguration(config)
                
                match result with
                | Ok () -> failtest "Expected error for empty client ID"
                | Error (InvalidConfiguration _) -> () // Expected
                | Error error -> failtestf $"Expected InvalidConfiguration but got: %A" error
            }
            
            test "should fail with empty scopes" {
                let service = TestHelpers.createMockOAuthService Map.empty
                let config = { TestHelpers.createTestOAuthConfig() with Scopes = [] }
                
                let result = service.ValidateConfiguration(config)
                
                match result with
                | Ok () -> failtest "Expected error for empty scopes"
                | Error (InvalidConfiguration _) -> () // Expected
                | Error error -> failtestf $"Expected InvalidConfiguration but got: %A" error
            }
            
            test "should fail with invalid redirect URI protocol" {
                let service = TestHelpers.createMockOAuthService Map.empty
                let config = { TestHelpers.createTestOAuthConfig() with RedirectUri = HttpUrl "ftp://invalid.com" }
                
                let result = service.ValidateConfiguration(config)
                
                match result with
                | Ok () -> failtest "Expected error for invalid redirect URI"
                | Error (InvalidConfiguration _) -> () // Expected
                | Error error -> failtestf $"Expected InvalidConfiguration but got: %A" error
            }
        ]
    
    /// Tests for GenerateAuthorizationUrl functionality
    module GenerateAuthorizationUrlTests =
        
        [<Tests>]
        let tests = testList "GenerateAuthorizationUrl" [
            
            test "should generate valid authorization URL" {
                let service = TestHelpers.createMockOAuthService Map.empty
                let config = TestHelpers.createTestOAuthConfig()
                let state = "test_state_123"
                let codeChallenge = CodeChallenge "test_code_challenge"
                
                let result = service.GenerateAuthorizationUrl config state codeChallenge
                
                match result with
                | Ok (HttpUrl url) ->
                    Expect.stringContains url "https://accounts.spotify.com/authorize" "Should contain base authorization URL"
                    Expect.stringContains url "client_id=test_client_id" "Should contain client ID"
                    Expect.stringContains url "response_type=code" "Should contain response type"
                    Expect.stringContains url "code_challenge_method=S256" "Should contain challenge method"
                    Expect.stringContains url "state=test_state_123" "Should contain state"
                    Expect.stringContains url "scope=user-read-private%20user-read-email" "Should contain encoded scopes"
                | Error error -> failtestf "Expected success but got error: %A" error
            }
            
            test "should fail with invalid configuration" {
                let service = TestHelpers.createMockOAuthService Map.empty
                let config = TestHelpers.createInvalidOAuthConfig()
                let state = "test_state"
                let codeChallenge = CodeChallenge "test_challenge"
                
                let result = service.GenerateAuthorizationUrl config state codeChallenge
                
                match result with
                | Ok _ -> failtest "Expected error for invalid configuration"
                | Error (InvalidConfiguration _) -> () // Expected
                | Error error -> failtestf $"Expected InvalidConfiguration but got: %A" error
            }
        ]
    
    /// Tests for ExchangeCodeForTokens functionality
    module ExchangeCodeForTokensTests =
        
        [<Tests>]
        let tests = testList "ExchangeCodeForTokens" [
            
            test "should successfully exchange code for tokens" {
                let successResponse = TestHelpers.createSuccessTokenResponseJson()
                let responses = Map.ofList [
                    ("https://accounts.spotify.com/api/token", Ok successResponse)
                ]
                let service = TestHelpers.createMockOAuthService responses
                let config = TestHelpers.createTestOAuthConfig()
                let authCode = AuthorizationCode "test_auth_code"
                let codeVerifier = CodeVerifier "test_code_verifier"
                
                let result = service.ExchangeCodeForTokens config authCode codeVerifier
                
                match result with
                | Ok tokenStorage ->
                    let accessToken = TypeExtraction.getAccessToken tokenStorage.AccessToken
                    let refreshToken = TypeExtraction.getRefreshToken tokenStorage.RefreshToken
                    Expect.equal accessToken "test_access_token" "Should have correct access token"
                    Expect.equal refreshToken "test_refresh_token" "Should have correct refresh token"
                    Expect.equal tokenStorage.TokenType "Bearer" "Should have correct token type"
                    Expect.isGreaterThan tokenStorage.ExpiresAt System.DateTime.UtcNow "Should expire in the future"
                | Error error -> failtestf "Expected success but got error: %A" error
            }
            
            test "should handle OAuth error response" {
                let errorResponse = TestHelpers.createErrorTokenResponseJson()
                let responses = Map.ofList [
                    ("https://accounts.spotify.com/api/token", Ok errorResponse)
                ]
                let service = TestHelpers.createMockOAuthService responses
                let config = TestHelpers.createTestOAuthConfig()
                let authCode = AuthorizationCode "invalid_code"
                let codeVerifier = CodeVerifier "test_verifier"
                
                let result = service.ExchangeCodeForTokens config authCode codeVerifier
                
                match result with
                | Ok _ -> failtest "Expected error for OAuth error response"
                | Error (TokenExchangeFailed ("invalid_grant", Some "Authorization code expired")) -> () // Expected
                | Error error -> failtestf $"Expected specific TokenExchangeFailed but got: %A" error
            }
            
            test "should handle HTTP error response" {
                let responses = Map.ofList [
                    ("https://accounts.spotify.com/api/token", Error(ServerError(500, "Internal Server Error")))
                ]
                let service = TestHelpers.createMockOAuthService responses
                let config = TestHelpers.createTestOAuthConfig()
                let authCode = AuthorizationCode "test_code"
                let codeVerifier = CodeVerifier "test_verifier"
                
                let result = service.ExchangeCodeForTokens config authCode codeVerifier
                
                match result with
                | Ok _ -> failtest "Expected error for HTTP error"
                | Error (TokenExchangeFailed (_, Some _)) -> () // Expected
                | Error error -> failtestf $"Expected TokenExchangeFailed but got: %A" error
            }
            
            test "should fail with invalid configuration" {
                let service = TestHelpers.createMockOAuthService Map.empty
                let config = TestHelpers.createInvalidOAuthConfig()
                let authCode = AuthorizationCode "test_code"
                let codeVerifier = CodeVerifier "test_verifier"
                
                let result = service.ExchangeCodeForTokens config authCode codeVerifier
                
                match result with
                | Ok _ -> failtest "Expected error for invalid configuration"
                | Error (InvalidConfiguration _) -> () // Expected
                | Error error -> failtestf $"Expected InvalidConfiguration but got: %A" error
            }
        ]
    
    /// Tests for RefreshAccessToken functionality
    module RefreshAccessTokenTests =
        
        [<Tests>]
        let tests = testList "RefreshAccessToken" [
            
            test "should successfully refresh access token" {
                let refreshResponse = """{
                    "access_token": "new_access_token",
                    "token_type": "Bearer",
                    "expires_in": 3600,
                    "scope": "user-read-private user-read-email"
                }"""
                let responses = Map.ofList [
                    ("https://accounts.spotify.com/api/token", Ok refreshResponse)
                ]
                let service = TestHelpers.createMockOAuthService responses
                let config = TestHelpers.createTestOAuthConfig()
                let refreshToken = RefreshToken "old_refresh_token"
                
                let result = service.RefreshAccessToken config refreshToken
                
                match result with
                | Ok tokenStorage ->
                    let accessToken = TypeExtraction.getAccessToken tokenStorage.AccessToken
                    let preservedRefreshToken = TypeExtraction.getRefreshToken tokenStorage.RefreshToken
                    Expect.equal accessToken "new_access_token" "Should have new access token"
                    Expect.equal preservedRefreshToken "old_refresh_token" "Should preserve original refresh token"
                    Expect.equal tokenStorage.TokenType "Bearer" "Should have correct token type"
                | Error error -> failtestf "Expected success but got error: %A" error
            }
            
            test "should handle new refresh token in response" {
                let refreshResponse = """{
                    "access_token": "new_access_token",
                    "token_type": "Bearer",
                    "expires_in": 3600,
                    "refresh_token": "new_refresh_token",
                    "scope": "user-read-private user-read-email"
                }"""
                let responses = Map.ofList [
                    ("https://accounts.spotify.com/api/token", Ok refreshResponse)
                ]
                let service = TestHelpers.createMockOAuthService responses
                let config = TestHelpers.createTestOAuthConfig()
                let oldRefreshToken = RefreshToken "old_refresh_token"
                
                let result = service.RefreshAccessToken config oldRefreshToken
                
                match result with
                | Ok tokenStorage ->
                    let newRefreshToken = TypeExtraction.getRefreshToken tokenStorage.RefreshToken
                    Expect.equal newRefreshToken "new_refresh_token" "Should use new refresh token from response"
                | Error error -> failtestf "Expected success but got error: %A" error
            }
            
            test "should handle refresh token error" {
                let errorResponse = """{
                    "error": "invalid_grant",
                    "error_description": "Refresh token expired"
                }"""
                let responses = Map.ofList [
                    ("https://accounts.spotify.com/api/token", Ok errorResponse)
                ]
                let service = TestHelpers.createMockOAuthService responses
                let config = TestHelpers.createTestOAuthConfig()
                let refreshToken = RefreshToken "expired_token"
                
                let result = service.RefreshAccessToken config refreshToken
                
                match result with
                | Ok _ -> failtest "Expected error for expired refresh token"
                | Error (TokenExchangeFailed ("invalid_grant", Some "Refresh token expired")) -> () // Expected
                | Error error -> failtestf $"Expected specific TokenExchangeFailed but got: %A" error
            }
        ]
    
    /// Tests for OAuthServiceHelpers functionality
    module OAuthServiceHelpersTests =
        
        [<Tests>]
        let tests = testList "OAuthServiceHelpers" [
            
            test "createSpotifyOAuthConfig should create valid configuration" {
                let clientId = "test_client_id"
                let clientSecret = "test_client_secret"
                let redirectPort = PortNumber 8888
                
                let result = OAuthServiceHelpers.createSpotifyOAuthConfig clientId clientSecret redirectPort
                
                match result with
                | Ok config ->
                    Expect.equal config.ClientId clientId "Should have correct client ID"
                    Expect.equal config.ClientSecret clientSecret "Should have correct client secret"
                    let redirectUri = TypeExtraction.getHttpUrl config.RedirectUri
                    Expect.equal redirectUri "http://127.0.0.1:8888/callback" "Should have correct redirect URI"
                    Expect.isNonEmpty config.Scopes "Should have default scopes"
                | Error error -> failtestf "Expected success but got error: %A" error
            }
            
            test "validateState should succeed with matching states" {
                let expectedState = "test_state_123"
                let receivedState = Some "test_state_123"
                
                let result = OAuthServiceHelpers.validateState expectedState receivedState
                
                match result with
                | Ok () -> () // Expected
                | Error error -> failtestf "Expected success but got error: %A" error
            }
            
            test "validateState should fail with mismatched states" {
                let expectedState = "expected_state"
                let receivedState = Some "different_state"
                
                let result = OAuthServiceHelpers.validateState expectedState receivedState
                
                match result with
                | Ok () -> failtest "Expected error for mismatched states"
                | Error (StateValidationFailed ("expected_state", Some "different_state")) -> () // Expected
                | Error error -> failtestf $"Expected StateValidationFailed but got: %A" error
            }
            
            test "validateState should fail with missing state" {
                let expectedState = "expected_state"
                let receivedState = None
                
                let result = OAuthServiceHelpers.validateState expectedState receivedState
                
                match result with
                | Ok () -> failtest "Expected error for missing state"
                | Error (StateValidationFailed ("expected_state", None)) -> () // Expected
                | Error error -> failtestf $"Expected StateValidationFailed but got: %A" error
            }
            
            test "isTokenNearExpiry should detect near expiry" {
                let nearExpiryToken = {
                    AccessToken = AccessToken "test_token"
                    RefreshToken = RefreshToken "test_refresh"
                    ExpiresAt = System.DateTime.UtcNow.AddMinutes(3.0) // 3 minutes from now
                    TokenType = "Bearer"
                }
                
                let isNearExpiry = OAuthServiceHelpers.isTokenNearExpiry nearExpiryToken
                Expect.isTrue isNearExpiry "Should detect token near expiry"
            }
            
            test "isTokenNearExpiry should not detect far expiry" {
                let farExpiryToken = {
                    AccessToken = AccessToken "test_token"
                    RefreshToken = RefreshToken "test_refresh"
                    ExpiresAt = System.DateTime.UtcNow.AddMinutes(30.0) // 30 minutes from now
                    TokenType = "Bearer"
                }
                
                let isNearExpiry = OAuthServiceHelpers.isTokenNearExpiry farExpiryToken
                Expect.isFalse isNearExpiry "Should not detect token far from expiry"
            }
            
            test "getRecommendedScopes should return base scopes without playlist access" {
                let scopes = OAuthServiceHelpers.getRecommendedScopes false
                
                Expect.contains scopes "user-read-private" "Should contain user-read-private"
                Expect.contains scopes "user-read-email" "Should contain user-read-email"
                Expect.isFalse (List.contains "playlist-read-private" scopes) "Should not contain playlist scopes"
            }
            
            test "getRecommendedScopes should include playlist scopes when requested" {
                let scopes = OAuthServiceHelpers.getRecommendedScopes true
                
                Expect.contains scopes "user-read-private" "Should contain user-read-private"
                Expect.contains scopes "user-read-email" "Should contain user-read-email"
                Expect.contains scopes "playlist-read-private" "Should contain playlist-read-private"
                Expect.contains scopes "playlist-read-collaborative" "Should contain playlist-read-collaborative"
            }
            
            test "extractOAuthError should parse error response JSON" {
                let errorJson = """{
                    "error": "invalid_client",
                    "error_description": "Invalid client credentials"
                }"""
                
                let error = OAuthServiceHelpers.extractOAuthError errorJson
                
                match error with
                | TokenExchangeFailed ("invalid_client", Some "Invalid client credentials") -> () // Expected
                | _ -> failtestf $"Expected specific TokenExchangeFailed but got: %A" error
            }
            
            test "createTimestampedState should create state with timestamp" {
                let cryptoService = CryptoService.create()
                
                let result = OAuthServiceHelpers.createTimestampedState cryptoService
                
                match result with
                | Ok state ->
                    Expect.stringContains state "_" "Should contain timestamp separator"
                    let parts = state.Split('_')
                    Expect.equal parts.Length 2 "Should have two parts"
                | Error error -> failtestf "Expected success but got error: %A" error
            }
            
            test "validateTimestampedState should validate recent state" {
                let cryptoService = CryptoService.create()
                let recentTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                let state = $"random_part_{recentTimestamp}"
                
                let isValid = OAuthServiceHelpers.validateTimestampedState state 10 // 10 minutes max age
                Expect.isTrue isValid "Should validate recent timestamped state"
            }
            
            test "validateTimestampedState should reject old state" {
                let oldTimestamp = System.DateTimeOffset.UtcNow.AddMinutes(-15.0).ToUnixTimeSeconds()
                let state = $"random_part_{oldTimestamp}"
                
                let isValid = OAuthServiceHelpers.validateTimestampedState state 10 // 10 minutes max age
                Expect.isFalse isValid "Should reject old timestamped state"
            }
            
            test "formatOAuthError should format errors correctly" {
                let configError = InvalidConfiguration "Missing client ID"
                let urlError = AuthorizationUrlGenerationFailed "Invalid URL"
                let tokenError = TokenExchangeFailed ("invalid_grant", Some "Code expired")
                let stateError = StateValidationFailed ("expected", Some "received")
                
                let configMessage = OAuthServiceHelpers.formatOAuthError configError
                let urlMessage = OAuthServiceHelpers.formatOAuthError urlError
                let tokenMessage = OAuthServiceHelpers.formatOAuthError tokenError
                let stateMessage = OAuthServiceHelpers.formatOAuthError stateError
                
                Expect.stringContains configMessage "OAuth configuration error" "Should format config error"
                Expect.stringContains urlMessage "Failed to generate authorization URL" "Should format URL error"
                Expect.stringContains tokenMessage "Token exchange failed" "Should format token error"
                Expect.stringContains stateMessage "State validation failed" "Should format state error"
            }
        ]
    
    /// Combined test suite
    [<Tests>]
    let allTests = testList "OAuthServiceTests" [
        ValidateConfigurationTests.tests
        GenerateAuthorizationUrlTests.tests
        ExchangeCodeForTokensTests.tests
        RefreshAccessTokenTests.tests
        OAuthServiceHelpersTests.tests
    ]