namespace SpotifyCLI.Tests.Domain

open Expecto
open FsCheck
open SpotifyCLI.Domain
open SpotifyCLI.Tests.TestUtilities

/// Tests for constrained domain types following F# coding guide testing patterns
module TypesTests =
    
    /// String50 constraint tests
    module String50Tests =
        
        let validString50Tests = testList "String50 Valid Cases" [
            
            testCase "Single character string should be valid" (fun () ->
                let result = ConstrainedTypes.createString50 "A"
                Assertions.assertResultOk (String50 "A") result)
            
            testCase "Exactly 50 character string should be valid" (fun () ->
                let input = String.replicate 50 "A"
                let result = ConstrainedTypes.createString50 input
                Assertions.assertResultOk (String50 input) result)
            
            testProperty "Valid strings (1-50 chars) should create String50" (fun () ->
                Prop.forAll 
                    (Arb.fromGen Generators.validString50)
                    (fun validStr ->
                        let result = ConstrainedTypes.createString50 validStr
                        match result with
                        | Ok (String50 s) -> s = validStr
                        | Error _ -> false))
        ]
        
        let invalidString50Tests = testList "String50 Invalid Cases" [
            
            testCase "Empty string should be invalid" (fun () ->
                let result = ConstrainedTypes.createString50 ""
                Assertions.assertResultError result)
            
            testCase "Null string should be invalid" (fun () ->
                let result = ConstrainedTypes.createString50 null
                Assertions.assertResultError result)
            
            testCase "51 character string should be invalid" (fun () ->
                let input = String.replicate 51 "A"
                let result = ConstrainedTypes.createString50 input
                Assertions.assertResultError result)
            
            testProperty "Strings longer than 50 chars should be invalid" (fun (NonEmptyString longStr) ->
                if longStr.Length > 50 then
                    let result = ConstrainedTypes.createString50 longStr
                    match result with
                    | Error _ -> true
                    | Ok _ -> false
                else
                    true) // Skip strings that are valid length
        ]
        
        let allString50Tests = testList "String50 Tests" [
            validString50Tests
            invalidString50Tests
        ]
    
    /// PlaylistName constraint tests
    module PlaylistNameTests =
        
        let validPlaylistNameTests = testList "PlaylistName Valid Cases" [
            
            testCase "Valid playlist name should work" (fun () ->
                let result = ConstrainedTypes.createPlaylistName "My Awesome Playlist"
                Assertions.assertResultOk (PlaylistName "My Awesome Playlist") result)
            
            testCase "Empty string should create default name" (fun () ->
                let result = ConstrainedTypes.createPlaylistName ""
                Assertions.assertResultOk (PlaylistName "Untitled Playlist") result)
            
            testCase "Null string should create default name" (fun () ->
                let result = ConstrainedTypes.createPlaylistName null
                Assertions.assertResultOk (PlaylistName "Untitled Playlist") result)
            
            testCase "Exactly 100 character name should be valid" (fun () ->
                let input = String.replicate 100 "A"
                let result = ConstrainedTypes.createPlaylistName input
                Assertions.assertResultOk (PlaylistName input) result)
        ]
        
        let invalidPlaylistNameTests = testList "PlaylistName Invalid Cases" [
            
            testCase "101 character name should be invalid" (fun () ->
                let input = String.replicate 101 "A"
                let result = ConstrainedTypes.createPlaylistName input
                Assertions.assertResultError result)
        ]
        
        let allPlaylistNameTests = testList "PlaylistName Tests" [
            validPlaylistNameTests
            invalidPlaylistNameTests
        ]
    
    /// EmailAddress validation tests
    module EmailAddressTests =
        
        let validEmailTests = testList "EmailAddress Valid Cases" [
            
            testCase "Simple valid email should work" (fun () ->
                let result = ConstrainedTypes.createEmailAddress "test@example.com"
                Assertions.assertResultOk (EmailAddress "test@example.com") result)
            
            testCase "Email with subdomain should work" (fun () ->
                let result = ConstrainedTypes.createEmailAddress "user@mail.example.com"
                Assertions.assertResultOk (EmailAddress "user@mail.example.com") result)
            
            testCase "Email with numbers should work" (fun () ->
                let result = ConstrainedTypes.createEmailAddress "user123@example123.com"
                Assertions.assertResultOk (EmailAddress "user123@example123.com") result)
            
            testProperty "Generated valid emails should be accepted" (fun () ->
                Prop.forAll
                    (Arb.fromGen Generators.validEmail)
                    (fun validEmail ->
                        let result = ConstrainedTypes.createEmailAddress validEmail
                        match result with
                        | Ok (EmailAddress e) -> e = validEmail
                        | Error _ -> false))
        ]
        
        let invalidEmailTests = testList "EmailAddress Invalid Cases" [
            
            testCase "Missing @ symbol should be invalid" (fun () ->
                let result = ConstrainedTypes.createEmailAddress "testexample.com"
                Assertions.assertResultError result)
            
            testCase "Missing domain should be invalid" (fun () ->
                let result = ConstrainedTypes.createEmailAddress "test@"
                Assertions.assertResultError result)
            
            testCase "Missing local part should be invalid" (fun () ->
                let result = ConstrainedTypes.createEmailAddress "@example.com"
                Assertions.assertResultError result)
            
            testCase "Empty string should be invalid" (fun () ->
                let result = ConstrainedTypes.createEmailAddress ""
                Assertions.assertResultError result)
            
            testCase "Null should be invalid" (fun () ->
                let result = ConstrainedTypes.createEmailAddress null
                Assertions.assertResultError result)
        ]
        
        let allEmailTests = testList "EmailAddress Tests" [
            validEmailTests
            invalidEmailTests
        ]
    
    /// SpotifyUri validation tests
    module SpotifyUriTests =
        
        let validUriTests = testList "SpotifyUri Valid Cases" [
            
            testCase "User URI should be valid" (fun () ->
                let result = ConstrainedTypes.createSpotifyUri "spotify:user:username"
                Assertions.assertResultOk (SpotifyUri "spotify:user:username") result)
            
            testCase "Track URI should be valid" (fun () ->
                let result = ConstrainedTypes.createSpotifyUri "spotify:track:4iV5W9uYEdYUVa79Axb7Rh"
                Assertions.assertResultOk (SpotifyUri "spotify:track:4iV5W9uYEdYUVa79Axb7Rh") result)
            
            testCase "Playlist URI should be valid" (fun () ->
                let result = ConstrainedTypes.createSpotifyUri "spotify:playlist:37i9dQZF1DXcBWIGoYBM5M"
                Assertions.assertResultOk (SpotifyUri "spotify:playlist:37i9dQZF1DXcBWIGoYBM5M") result)
            
            testProperty "Generated Spotify URIs should be valid" (fun () ->
                Prop.forAll
                    (Arb.fromGen Generators.validSpotifyUri)
                    (fun validUri ->
                        let result = ConstrainedTypes.createSpotifyUri validUri
                        match result with
                        | Ok (SpotifyUri uri) -> uri = validUri
                        | Error _ -> false))
        ]
        
        let invalidUriTests = testList "SpotifyUri Invalid Cases" [
            
            testCase "HTTP URL should be invalid" (fun () ->
                let result = ConstrainedTypes.createSpotifyUri "https://open.spotify.com/track/123"
                Assertions.assertResultError result)
            
            testCase "Empty string should be invalid" (fun () ->
                let result = ConstrainedTypes.createSpotifyUri ""
                Assertions.assertResultError result)
            
            testCase "Random text should be invalid" (fun () ->
                let result = ConstrainedTypes.createSpotifyUri "not-a-uri"
                Assertions.assertResultError result)
        ]
        
        let allSpotifyUriTests = testList "SpotifyUri Tests" [
            validUriTests
            invalidUriTests
        ]
    
    /// TrackCount validation tests
    module TrackCountTests =
        
        let validTrackCountTests = testList "TrackCount Valid Cases" [
            
            testCase "Zero tracks should be valid" (fun () ->
                let result = ConstrainedTypes.createTrackCount 0
                Assertions.assertResultOk (TrackCount 0) result)
            
            testCase "Positive track count should be valid" (fun () ->
                let result = ConstrainedTypes.createTrackCount 42
                Assertions.assertResultOk (TrackCount 42) result)
            
            testCase "Large track count should be valid" (fun () ->
                let result = ConstrainedTypes.createTrackCount 10000
                Assertions.assertResultOk (TrackCount 10000) result)
            
            testProperty "Non-negative integers should create TrackCount" (fun (NonNegativeInt count) ->
                let result = ConstrainedTypes.createTrackCount count
                match result with
                | Ok (TrackCount c) -> c = count
                | Error _ -> false)
        ]
        
        let invalidTrackCountTests = testList "TrackCount Invalid Cases" [
            
            testCase "Negative track count should be invalid" (fun () ->
                let result = ConstrainedTypes.createTrackCount -1
                Assertions.assertResultError result)
            
            testCase "Large negative count should be invalid" (fun () ->
                let result = ConstrainedTypes.createTrackCount -100
                Assertions.assertResultError result)
        ]
        
        let allTrackCountTests = testList "TrackCount Tests" [
            validTrackCountTests
            invalidTrackCountTests
        ]
    
    /// Domain validation tests
    module DomainValidationTests =
        
        let tokenExpirationTests = testList "Token Expiration Tests" [
            
            testCase "Future token should not be expired" (fun () ->
                let futureToken = {
                    TestData.sampleTokenStorage with
                        ExpiresAt = System.DateTime.UtcNow.AddHours(1.0)
                }
                let isExpired = DomainValidation.isTokenExpired futureToken
                Expect.isFalse isExpired "Future token should not be expired")
            
            testCase "Past token should be expired" (fun () ->
                let isExpired = DomainValidation.isTokenExpired TestData.expiredTokenStorage
                Expect.isTrue isExpired "Past token should be expired")
            
            testCase "Token expiring now should be expired" (fun () ->
                let nowToken = {
                    TestData.sampleTokenStorage with
                        ExpiresAt = System.DateTime.UtcNow
                }
                let isExpired = DomainValidation.isTokenExpired nowToken
                Expect.isTrue isExpired "Token expiring now should be expired")
        ]
        
        let userProfileValidationTests = testList "User Profile Validation Tests" [
            
            testCase "Valid profile should pass validation" (fun () ->
                let result = DomainValidation.validateUserProfile TestData.sampleUserProfile
                Assertions.assertResultOk TestData.sampleUserProfile result)
            
            testProperty "Generated valid profiles should pass validation" (fun () ->
                Prop.forAll
                    (Arb.fromGen Generators.validUserProfile)
                    (fun profile ->
                        let result = DomainValidation.validateUserProfile profile
                        match result with
                        | Ok _ -> true
                        | Error _ -> false))
        ]
        
        let allValidationTests = testList "Domain Validation Tests" [
            tokenExpirationTests
            userProfileValidationTests
        ]
    
    /// Type extraction tests
    module TypeExtractionTests =
        
        let extractionTests = testList "Type Extraction Tests" [
            
            testCase "String50 extraction should return original value" (fun () ->
                let original = "test string"
                let string50 = String50 original
                let extracted = TypeExtraction.getString50 string50
                Expect.equal extracted original "Extracted value should match original")
            
            testCase "EmailAddress extraction should return original value" (fun () ->
                let original = "test@example.com"
                let email = EmailAddress original
                let extracted = TypeExtraction.getEmailAddress email
                Expect.equal extracted original "Extracted email should match original")
            
            testCase "TrackCount extraction should return original value" (fun () ->
                let original = 42
                let trackCount = TrackCount original
                let extracted = TypeExtraction.getTrackCount trackCount
                Expect.equal extracted original "Extracted count should match original")
        ]
    
    /// Main test suite
    let allTests = testList "Domain Types Tests" [
        String50Tests.allString50Tests
        PlaylistNameTests.allPlaylistNameTests
        EmailAddressTests.allEmailTests
        SpotifyUriTests.allSpotifyUriTests  
        TrackCountTests.allTrackCountTests
        DomainValidationTests.allValidationTests
        TypeExtractionTests.extractionTests
    ]