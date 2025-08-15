namespace SpotifyCLI.Tests.Services

open Expecto
open SpotifyCLI.Domain
open SpotifyCLI.Services
open SpotifyCLI.Tests.TestUtilities

/// Comprehensive tests for CryptoService functionality
module CryptoServiceTests =
    
    /// Test helpers and utilities
    module TestHelpers =
        
        /// Generate test crypto service
        let createCryptoService () = CryptoService.create()
        
        /// Check if string is URL-safe base64
        let isUrlSafeBase64 (str: string) =
            str |> Seq.forall (fun c ->
                (c >= 'A' && c <= 'Z') || 
                (c >= 'a' && c <= 'z') || 
                (c >= '0' && c <= '9') ||
                c = '-' || c = '_')
        
        /// Check if string contains no padding characters
        let hasNoPadding (str: string) =
            not (str.Contains('='))
    
    /// Tests for GenerateRandomString functionality
    module GenerateRandomStringTests =
        
        [<Tests>]
        let tests = testList "GenerateRandomString Tests" [
            testCase "should generate string when given positive length" (fun () ->
                let service = TestHelpers.createCryptoService()
                let result = service.GenerateRandomString(16)
                
                match result with
                | Ok randomString -> 
                    Expect.isTrue (randomString.Length > 0) "Should generate non-empty string"
                    Expect.isTrue (TestHelpers.isUrlSafeBase64 randomString) "Should be URL-safe base64"
                    Expect.isTrue (TestHelpers.hasNoPadding randomString) "Should have no padding"
                | Error error -> failtestf "Expected success but got error: %A" error)
        
            testCase "should fail when given zero length" (fun () ->
                let service = TestHelpers.createCryptoService()
                let result = service.GenerateRandomString(0)
                
                match result with
                | Ok _ -> failtestf "Expected error for zero length"
                | Error (RandomGenerationFailed _) -> () // Expected
                | Error error -> failtestf "Expected RandomGenerationFailed but got: %A" error)
        
            testCase "should fail when given negative length" (fun () ->
                let service = TestHelpers.createCryptoService()
                let result = service.GenerateRandomString(-1)
                
                match result with
                | Ok _ -> failtestf "Expected error for negative length"
                | Error (RandomGenerationFailed _) -> () // Expected
                | Error error -> failtestf "Expected RandomGenerationFailed but got: %A" error)
        
            testCase "should generate different strings on multiple calls" (fun () ->
                let length = 16 // Fixed length for this test
                let service = TestHelpers.createCryptoService()
                
                let result1 = service.GenerateRandomString(length)
                let result2 = service.GenerateRandomString(length)
                
                match result1, result2 with
                | Ok str1, Ok str2 -> Expect.notEqual str1 str2 "Generated strings should be different"
                | _ -> failtestf "Both calls should succeed")
        ]
    
    // Temporarily commented out while completing conversion
    (*
    /// Tests for GenerateCodeVerifier functionality
    module GenerateCodeVerifierTests =
        
        [<Tests>]
        let tests = testList "GenerateCodeVerifier Tests" [
            testCase "should generate valid PKCE code verifier" (fun () ->
                let service = TestHelpers.createCryptoService()
                let result = service.GenerateCodeVerifier()
                
                match result with
                | Ok (CodeVerifier verifier) ->
                    Expect.isTrue (verifier.Length >= 43) "Verifier should be at least 43 chars"
                    Expect.isTrue (verifier.Length <= 128) "Verifier should be at most 128 chars"
                    Expect.isTrue (TestHelpers.isUrlSafeBase64 verifier) "Verifier should be URL-safe base64"
                    Expect.isTrue (CryptoServiceHelpers.isValidCodeVerifier verifier) "Verifier should be valid"
                | Error error -> failtestf "Expected success but got error: %A" error)
        
        testCase "should generate different verifiers on multiple calls" (fun () ->
            let service = TestHelpers.createCryptoService()
            
            let result1 = service.GenerateCodeVerifier()
            let result2 = service.GenerateCodeVerifier()
            
            match result1, result2 with
            | Ok (CodeVerifier v1), Ok (CodeVerifier v2) ->
                Expect.notEqual v1 v2 "Verifiers should be different"
            | _ -> failtestf "Both calls should succeed")
        
        testProperty "should always generate valid verifiers" (fun () ->
            let service = TestHelpers.createCryptoService()
            let result = service.GenerateCodeVerifier()
            
            match result with
            | Ok (CodeVerifier verifier) -> CryptoServiceHelpers.isValidCodeVerifier verifier
            | Error _ -> false)
        ]
    *)
    
    // Rest of the modules temporarily commented out while completing conversion
    (*
    /// Tests for GenerateCodeChallenge functionality
    module GenerateCodeChallengeTests =
        
        let ``should generate valid code challenge from verifier`` () =
            let service = TestHelpers.createCryptoService()
            let verifier = CodeVerifier "test-verifier-1234567890123456789012345"
            let result = service.GenerateCodeChallenge(verifier)
            
            match result with
            | Ok (CodeChallenge challenge) ->
                Expect.isTrue (challenge.Length > 0) "Challenge should not be empty"
                Expect.isTrue (TestHelpers.isUrlSafeBase64 challenge) "Challenge should be URL-safe base64"
                Expect.isTrue (TestHelpers.hasNoPadding challenge) "Challenge should have no padding"
            | Error error -> failtestf "Expected success but got error: %A" error
        
        let ``should generate same challenge for same verifier`` () =
            let service = TestHelpers.createCryptoService()
            let verifier = CodeVerifier "test-verifier-1234567890123456789012345"
            
            let result1 = service.GenerateCodeChallenge(verifier)
            let result2 = service.GenerateCodeChallenge(verifier)
            
            match result1, result2 with
            | Ok (CodeChallenge c1), Ok (CodeChallenge c2) ->
                Expect.equal c1 c2 "Same verifier should produce same challenge"
            | _ -> failtestf "Both calls should succeed"
        
        let ``should generate different challenges for different verifiers`` () =
            let service = TestHelpers.createCryptoService()
            let verifier1 = CodeVerifier "test-verifier-1111111111111111111111111"
            let verifier2 = CodeVerifier "test-verifier-2222222222222222222222222"
            
            let result1 = service.GenerateCodeChallenge(verifier1)
            let result2 = service.GenerateCodeChallenge(verifier2)
            
            match result1, result2 with
            | Ok (CodeChallenge c1), Ok (CodeChallenge c2) ->
                Expect.notEqual c1 c2 "Different verifiers should produce different challenges"
            | _ -> failtestf "Both calls should succeed"
    
    /// Tests for GenerateState functionality
    module GenerateStateTests =
        
        let ``should generate valid OAuth state parameter`` () =
            let service = TestHelpers.createCryptoService()
            let result = service.GenerateState()
            
            match result with
            | Ok state ->
                Expect.isTrue (state.Length >= 16) "State should be at least 16 chars"
                Expect.isTrue (state.Length <= 128) "State should be at most 128 chars"
                Expect.isTrue (TestHelpers.isUrlSafeBase64 state) "State should be URL-safe base64"
                Expect.isTrue (CryptoServiceHelpers.isValidState state) "State should be valid"
            | Error error -> failtestf "Expected success but got error: %A" error
        
        let ``should generate different states on multiple calls`` () =
            let service = TestHelpers.createCryptoService()
            
            let result1 = service.GenerateState()
            let result2 = service.GenerateState()
            
            match result1, result2 with
            | Ok s1, Ok s2 ->
                Expect.notEqual s1 s2 "States should be different"
            | _ -> failtestf "Both calls should succeed"
        
        let ``should always generate valid states`` () =
            let service = TestHelpers.createCryptoService()
            let result = service.GenerateState()
            
            match result with
            | Ok state -> CryptoServiceHelpers.isValidState state
            | Error _ -> false
    
    /// Tests for CryptoServiceHelpers functionality
    module CryptoServiceHelpersTests =
        
        let ``generatePKCEPair should create valid verifier and challenge pair`` () =
            let service = TestHelpers.createCryptoService()
            let result = CryptoServiceHelpers.generatePKCEPair service
            
            match result with
            | Ok (CodeVerifier verifier, CodeChallenge challenge) ->
                Expect.isTrue (CryptoServiceHelpers.isValidCodeVerifier verifier) "Verifier should be valid"
                Expect.isTrue (challenge.Length > 0) "Challenge should not be empty"
                Expect.isTrue (TestHelpers.isUrlSafeBase64 challenge) "Challenge should be URL-safe base64"
            | Error error -> failtestf "Expected success but got error: %A" error
        
        let ``isValidCodeVerifier should validate correct format`` () =
            let validVerifier = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_"
            let invalidVerifier1 = "short" // Too short
            let invalidVerifier2 = "valid-but-contains-invalid-chars!" // Invalid characters
            let invalidVerifier3 = String.replicate 130 "A" // Too long
            
            Expect.isTrue (CryptoServiceHelpers.isValidCodeVerifier validVerifier) "Valid verifier should pass validation"
            Expect.isFalse (CryptoServiceHelpers.isValidCodeVerifier invalidVerifier1) "Too short verifier should fail"
            Expect.isFalse (CryptoServiceHelpers.isValidCodeVerifier invalidVerifier2) "Invalid chars verifier should fail"
            Expect.isFalse (CryptoServiceHelpers.isValidCodeVerifier invalidVerifier3) "Too long verifier should fail"
        
        let ``isValidState should validate correct format`` () =
            let validState = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrst"
            let invalidState1 = "short" // Too short
            let invalidState2 = "valid-but-contains-invalid-chars!" // Invalid characters
            let invalidState3 = String.replicate 130 "A" // Too long
            
            Expect.isTrue (CryptoServiceHelpers.isValidState validState) "Valid state should pass validation"
            Expect.isFalse (CryptoServiceHelpers.isValidState invalidState1) "Too short state should fail"
            Expect.isFalse (CryptoServiceHelpers.isValidState invalidState2) "Invalid chars state should fail"
            Expect.isFalse (CryptoServiceHelpers.isValidState invalidState3) "Too long state should fail"
        
        let ``generateNonce should create valid nonce`` () =
            let service = TestHelpers.createCryptoService()
            let result = CryptoServiceHelpers.generateNonce service
            
            match result with
            | Ok nonce ->
                Expect.isTrue (nonce.Length > 0) "Nonce should not be empty"
                Expect.isTrue (TestHelpers.isUrlSafeBase64 nonce) "Nonce should be URL-safe base64"
            | Error error -> failtestf "Expected success but got error: %A" error
        
        let ``generateTemporaryPassword should validate length constraints`` () =
            let service = TestHelpers.createCryptoService()
            
            // Valid length
            let validResult = CryptoServiceHelpers.generateTemporaryPassword service 12
            match validResult with
            | Ok password -> Expect.isTrue (password.Length > 0) "Password should not be empty"
            | Error error -> failtestf "Expected success for valid length but got: %A" error
            
            // Too short
            let shortResult = CryptoServiceHelpers.generateTemporaryPassword service 7
            match shortResult with
            | Ok _ -> failtestf "Should fail for too short password"
            | Error (RandomGenerationFailed _) -> () // Expected
            | Error error -> failtestf "Expected RandomGenerationFailed but got: %A" error
            
            // Too long
            let longResult = CryptoServiceHelpers.generateTemporaryPassword service 129
            match longResult with
            | Ok _ -> failtestf "Should fail for too long password"
            | Error (RandomGenerationFailed _) -> () // Expected
            | Error error -> failtestf "Expected RandomGenerationFailed but got: %A" error
    
    /// Tests for mock crypto service behavior
    module MockCryptoServiceTests =
        
        let ``should return fixed values when configured`` () =
            let fixedValues = Map.ofList [
                ("randomString", "fixed-random-string")
                ("codeVerifier", "fixed-code-verifier-1234567890123456789")
                ("codeChallenge", "fixed-code-challenge-123456789")
                ("state", "fixed-state-1234567890")
            ]
            let mockService = Mocks.MockCryptoService(fixedValues) :> ICryptoService
            
            let randomResult = mockService.GenerateRandomString(16)
            let verifierResult = mockService.GenerateCodeVerifier()
            let challengeResult = mockService.GenerateCodeChallenge(CodeVerifier "test")
            let stateResult = mockService.GenerateState()
            
            Assertions.assertResultOk "fixed-random-string" randomResult
            Assertions.assertResultOk (CodeVerifier "fixed-code-verifier-1234567890123456789") verifierResult
            Assertions.assertResultOk (CodeChallenge "fixed-code-challenge-123456789") challengeResult
            Assertions.assertResultOk "fixed-state-1234567890" stateResult
        
        let ``should return default values when not configured`` () =
            let mockService = Mocks.MockCryptoService(Map.empty) :> ICryptoService
            
            let randomResult = mockService.GenerateRandomString(5)
            let verifierResult = mockService.GenerateCodeVerifier()
            let challengeResult = mockService.GenerateCodeChallenge(CodeVerifier "test")
            let stateResult = mockService.GenerateState()
            
            match randomResult, verifierResult, challengeResult, stateResult with
            | Ok random, Ok (CodeVerifier verifier), Ok (CodeChallenge challenge), Ok state ->
                Expect.equal "AAAAA" random "Should return default random string"
                Expect.equal "test-code-verifier-1234567890123456789012" verifier "Should return default verifier"
                Expect.equal "test-code-challenge-1234567890" challenge "Should return default challenge"
                Expect.equal "test-state-12345678901234567890" state "Should return default state"
            | _ -> failtestf "All mock operations should succeed"
    *)
