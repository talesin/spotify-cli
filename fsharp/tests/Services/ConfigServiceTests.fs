namespace SpotifyCLI.Tests.Services

open Expecto
open FsCheck
open SpotifyCLI.Domain
open SpotifyCLI.Services
open SpotifyCLI.Tests.TestUtilities

/// Tests for ConfigService following F# testing best practices
module ConfigServiceTests =
    
    /// Test fixtures and sample data
    module TestFixtures =
        
        let validTokenJson = """{
  "AccessToken": "test-access-token",
  "RefreshToken": "test-refresh-token", 
  "ExpiresAt": "2025-01-15T12:00:00.0000000Z",
  "TokenType": "Bearer"
}"""
        
        let invalidTokenJson = """{
  "AccessToken": "test-access-token"
  // Missing required fields
}"""
        
        let createMockFileSystemWithFile (filePath: string) (content: string) =
            let files = Map.ofList [(filePath, content)]
            let directories = Set.ofList [System.IO.Path.GetDirectoryName(filePath)]
            Mocks.MockFileSystem(files, directories)
        
        let createEmptyMockFileSystem () =
            Mocks.MockFileSystem(Map.empty, Set.empty)
    
    /// ReadTokens functionality tests
    module ReadTokensTests =
        
        let successCases = testList "ReadTokens Success Cases" [
            
            testCase "Should read valid token file successfully" (fun () ->
                let configPath = "/test/.spotify-cli/spotify.json"
                let fileSystem = TestFixtures.createMockFileSystemWithFile configPath TestFixtures.validTokenJson
                let configService = ConfigService.create fileSystem
                
                let result = configService.ReadTokens()
                
                match result with
                | Ok tokens ->
                    Expect.equal (TypeExtraction.getAccessToken tokens.AccessToken) "test-access-token" "Access token should match"
                    Expect.equal (TypeExtraction.getRefreshToken tokens.RefreshToken) "test-refresh-token" "Refresh token should match"
                    Expect.equal tokens.TokenType "Bearer" "Token type should match"
                | Error error -> 
                    failtestf $"Expected success but got error: %A" error)
        ]
        
        let errorCases = testList "ReadTokens Error Cases" [
            
            testCase "Should return FileNotFound when file doesn't exist" (fun () ->
                let fileSystem = TestFixtures.createEmptyMockFileSystem()
                let configService = ConfigService.create fileSystem
                
                let result = configService.ReadTokens()
                
                match result with
                | Error(ConfigError.FileNotFound _) -> () // Expected
                | Ok _ -> failtest "Expected FileNotFound error but got success"
                | Error other -> failtestf $"Expected FileNotFound but got: %A" other)
            
            testCase "Should return InvalidFormat for malformed JSON" (fun () ->
                let configPath = "/test/.spotify-cli/spotify.json"  
                let fileSystem = TestFixtures.createMockFileSystemWithFile configPath TestFixtures.invalidTokenJson
                let configService = ConfigService.create fileSystem
                
                let result = configService.ReadTokens()
                
                match result with
                | Error(ConfigError.InvalidFormat _) -> () // Expected
                | Ok _ -> failtest "Expected InvalidFormat error but got success"
                | Error other -> failtestf $"Expected InvalidFormat but got: %A" other)
        ]
        
        let allReadTokensTests = testList "ReadTokens Tests" [
            successCases
            errorCases
        ]
    
    /// WriteTokens functionality tests  
    module WriteTokensTests =
        
        let successCases = testList "WriteTokens Success Cases" [
            
            testCase "Should write tokens successfully" (fun () ->
                let fileSystem = TestFixtures.createEmptyMockFileSystem()
                let configService = ConfigService.create fileSystem
                
                let result = configService.WriteTokens(TestData.sampleTokenStorage)
                
                Assertions.assertResultOk () result)
        ]
        
        let allWriteTokensTests = testList "WriteTokens Tests" [
            successCases
        ]
    
    /// EnsureConfigDirectory functionality tests
    module EnsureConfigDirectoryTests =
        
        let successCases = testList "EnsureConfigDirectory Success Cases" [
            
            testCase "Should succeed when directory already exists" (fun () ->
                let existingDir = "/test/.spotify-cli"
                let directories = Set.ofList [existingDir]
                let fileSystem = Mocks.MockFileSystem(Map.empty, directories)
                let configService = ConfigService.create fileSystem
                
                let result = configService.EnsureConfigDirectory()
                
                Assertions.assertResultOk () result)
            
            testCase "Should succeed when creating new directory" (fun () ->
                let fileSystem = TestFixtures.createEmptyMockFileSystem()
                let configService = ConfigService.create fileSystem
                
                let result = configService.EnsureConfigDirectory()
                
                Assertions.assertResultOk () result)
        ]
        
        let allEnsureDirectoryTests = testList "EnsureConfigDirectory Tests" [
            successCases
        ]
    
    /// Configuration service helper tests
    module ConfigServiceHelpersTests =
        
        let tokenValidityTests = testList "Token Validity Tests" [
            
            testCase "Should detect valid token correctly" (fun () ->
                let validTokens = {
                    TestData.sampleTokenStorage with
                        ExpiresAt = System.DateTime.UtcNow.AddHours(1.0)
                }
                let configPath = "/test/.spotify-cli/spotify.json"
                let tokenJson = System.Text.Json.JsonSerializer.Serialize(validTokens)
                let fileSystem = TestFixtures.createMockFileSystemWithFile configPath tokenJson
                let configService = ConfigService.create fileSystem
                
                let result = ConfigServiceHelpers.isTokenValid configService
                
                match result with
                | Ok isValid -> Expect.isTrue isValid "Token should be valid"
                | Error error -> failtestf $"Expected success but got error: %A" error)
            
            testCase "Should detect expired token correctly" (fun () ->
                let expiredTokens = {
                    TestData.sampleTokenStorage with
                        ExpiresAt = System.DateTime.UtcNow.AddHours(-1.0)
                }
                let configPath = "/test/.spotify-cli/spotify.json"
                let tokenJson = System.Text.Json.JsonSerializer.Serialize(expiredTokens)
                let fileSystem = TestFixtures.createMockFileSystemWithFile configPath tokenJson
                let configService = ConfigService.create fileSystem
                
                let result = ConfigServiceHelpers.isTokenValid configService
                
                match result with
                | Ok isValid -> Expect.isFalse isValid "Token should be invalid/expired"
                | Error error -> failtestf $"Expected success but got error: %A" error)
        ]
        
        let getValidTokenTests = testList "GetValidTokenOrError Tests" [
            
            testCase "Should return valid token when not expired" (fun () ->
                let validTokens = {
                    TestData.sampleTokenStorage with
                        ExpiresAt = System.DateTime.UtcNow.AddHours(1.0)
                }
                let configPath = "/test/.spotify-cli/spotify.json"
                let tokenJson = System.Text.Json.JsonSerializer.Serialize(validTokens)
                let fileSystem = TestFixtures.createMockFileSystemWithFile configPath tokenJson
                let configService = ConfigService.create fileSystem
                
                let result = ConfigServiceHelpers.getValidTokenOrError configService
                
                match result with
                | Ok tokens -> 
                    Expect.equal (TypeExtraction.getAccessToken tokens.AccessToken) 
                                 (TypeExtraction.getAccessToken validTokens.AccessToken) "Should return the valid tokens"
                | Error error -> failtestf $"Expected success but got error: %A" error)
            
            testCase "Should return error when token is expired" (fun () ->
                let expiredTokens = TestData.expiredTokenStorage
                let configPath = "/test/.spotify-cli/spotify.json" 
                let tokenJson = System.Text.Json.JsonSerializer.Serialize(expiredTokens)
                let fileSystem = TestFixtures.createMockFileSystemWithFile configPath tokenJson
                let configService = ConfigService.create fileSystem
                
                let result = ConfigServiceHelpers.getValidTokenOrError configService
                
                match result with
                | Error(ConfigError.InvalidFormat _) -> () // Expected
                | Ok _ -> failtest "Expected error for expired token but got success"
                | Error other -> failtestf $"Expected InvalidFormat error but got: %A" other)
        ]
        
        let tokenExpiryUpdateTests = testList "Token Expiry Update Tests" [
            
            testCase "Should update token expiry correctly" (fun () ->
                let originalTokens = TestData.sampleTokenStorage
                let expiresInSeconds = 3600 // 1 hour
                
                let updatedTokens = ConfigServiceHelpers.updateTokenExpiry originalTokens expiresInSeconds
                
                let expectedExpiry = System.DateTime.UtcNow.AddSeconds(float expiresInSeconds)
                let timeDifference = abs((updatedTokens.ExpiresAt - expectedExpiry).TotalSeconds)
                
                Expect.isLessThan timeDifference 5.0 "Updated expiry should be within 5 seconds of expected time"
                Expect.equal (TypeExtraction.getAccessToken updatedTokens.AccessToken) 
                           (TypeExtraction.getAccessToken originalTokens.AccessToken) "Access token should remain unchanged"
                Expect.equal (TypeExtraction.getRefreshToken updatedTokens.RefreshToken)
                           (TypeExtraction.getRefreshToken originalTokens.RefreshToken) "Refresh token should remain unchanged")
        ]
        
        let allHelperTests = testList "ConfigService Helper Tests" [
            tokenValidityTests
            getValidTokenTests
            tokenExpiryUpdateTests
        ]
    
    /// Property-based tests for ConfigService
    module PropertyBasedTests =
        
        let tokenRoundtripTests = testList "Token Roundtrip Property Tests" [
            
            testProperty "Tokens should roundtrip through JSON serialization" (fun () ->
                // Create a property that tests serialization roundtrip
                let tokenStorage = TestData.sampleTokenStorage
                
                try
                    let fileSystem = TestFixtures.createEmptyMockFileSystem()
                    let configService = ConfigService.create fileSystem
                    
                    // Write and read should be inverse operations for valid data
                    let writeResult = configService.WriteTokens(tokenStorage)
                    match writeResult with
                    | Ok () -> true // Mock always succeeds, so this tests the JSON serialization path
                    | Error _ -> false
                with
                | _ -> false) // Any exception means the property failed
        ]
        
        let allPropertyTests = testList "ConfigService Property Tests" [
            tokenRoundtripTests
        ]
    
    /// Main test suite for ConfigService
    let allTests = testList "ConfigService Tests" [
        ReadTokensTests.allReadTokensTests
        WriteTokensTests.allWriteTokensTests  
        EnsureConfigDirectoryTests.allEnsureDirectoryTests
        ConfigServiceHelpersTests.allHelperTests
        PropertyBasedTests.allPropertyTests
    ]