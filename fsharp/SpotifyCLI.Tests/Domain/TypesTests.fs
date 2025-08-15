namespace SpotifyCLI.Tests.Domain

open Xunit
open FsCheck
open FsCheck.Xunit
open SpotifyCLI.Domain
open SpotifyCLI.Tests.TestUtilities

/// Tests for constrained domain types following F# coding guide testing patterns
module TypesTests =

    /// Generator for valid String50 values (1-50 characters)
    let validString50 =
        gen {
            let! length = Gen.choose(1, 50)
            let! chars = Gen.listOfLength length (Gen.choose(32, 126) |> Gen.map char)
            return chars |> List.toArray |> System.String
        }


    /// String50 constraint tests
    module String50Tests =

        [<Fact>]
        let ``Single character string should be valid`` () =
            let result = ConstrainedTypes.createString50 "A"
            Assertions.assertResultOk (String50 "A") result

        [<Fact>]
        let ``Exactly 50 character string should be valid`` () =
            let input = String.replicate 50 "A"
            let result = ConstrainedTypes.createString50 input
            Assertions.assertResultOk (String50 input) result

        [<Property>]
        let ``Valid strings (1-50 chars) should create String50`` () =
            Prop.forAll (Arb.fromGen validString50) (fun validStr ->
                let result = ConstrainedTypes.createString50 validStr
                match result with
                | Ok (String50 s) -> s = validStr
                | Error _ -> false)
        
        [<Fact>]
        let ``Empty string should be invalid`` () =
            let result = ConstrainedTypes.createString50 ""
            Assertions.assertResultError result

        [<Fact>]
        let ``Null string should be invalid`` () =
            let result = ConstrainedTypes.createString50 null
            Assertions.assertResultError result

        [<Fact>]
        let ``51 character string should be invalid`` () =
            let input = String.replicate 51 "A"
            let result = ConstrainedTypes.createString50 input
            Assertions.assertResultError result
            
        [<Property>]
        let ``Strings longer than 50 chars should be invalid`` (NonEmptyString longStr) =
            if longStr.Length > 50 then
                let result = ConstrainedTypes.createString50 longStr
                match result with
                | Error _ -> true
                | Ok _ -> false
            else
                true // Skip strings that are valid length
    
    /// PlaylistName constraint tests
    module PlaylistNameTests =
        
        [<Fact>]
        let ``Valid playlist name should work`` () =
            let result = ConstrainedTypes.createPlaylistName "My Awesome Playlist"
            Assertions.assertResultOk (PlaylistName "My Awesome Playlist") result
        
        [<Fact>]
        let ``Empty string should create default name`` () =
            let result = ConstrainedTypes.createPlaylistName ""
            Assertions.assertResultOk (PlaylistName "Untitled Playlist") result
        
        [<Fact>]
        let ``Null string should create default name`` () =
            let result = ConstrainedTypes.createPlaylistName null
            Assertions.assertResultOk (PlaylistName "Untitled Playlist") result
        
        [<Fact>]
        let ``Exactly 100 character name should be valid`` () =
            let input = String.replicate 100 "A"
            let result = ConstrainedTypes.createPlaylistName input
            Assertions.assertResultOk (PlaylistName input) result
        
        [<Fact>]
        let ``101 character name should be invalid`` () =
            let input = String.replicate 101 "A"
            let result = ConstrainedTypes.createPlaylistName input
            Assertions.assertResultError result
    
    /// EmailAddress validation tests
    module EmailAddressTests =
        
        [<Fact>]
        let ``Simple valid email should work`` () =
            let result = ConstrainedTypes.createEmailAddress "test@example.com"
            Assertions.assertResultOk (EmailAddress "test@example.com") result
        
        [<Fact>]
        let ``Email with subdomain should work`` () =
            let result = ConstrainedTypes.createEmailAddress "user@mail.example.com"
            Assertions.assertResultOk (EmailAddress "user@mail.example.com") result
        
        [<Fact>]
        let ``Email with numbers should work`` () =
            let result = ConstrainedTypes.createEmailAddress "user123@example123.com"
            Assertions.assertResultOk (EmailAddress "user123@example123.com") result
        
        // [<Property>]
        // let ``Generated valid emails should be accepted`` () =
        //     Prop.forAll
        //         (Arb.fromGen Generators.validEmail)
        //         (fun validEmail ->
        //             let result = ConstrainedTypes.createEmailAddress validEmail
        //             match result with
        //             | Ok (EmailAddress e) -> e = validEmail
        //             | Error _ -> false)
        
        [<Fact>]
        let ``Missing at symbol should be invalid`` () =
            let result = ConstrainedTypes.createEmailAddress "testexample.com"
            Assertions.assertResultError result
        
        [<Fact>]
        let ``Missing domain should be invalid`` () =
            let result = ConstrainedTypes.createEmailAddress "test@"
            Assertions.assertResultError result
        
        [<Fact>]
        let ``Missing local part should be invalid`` () =
            let result = ConstrainedTypes.createEmailAddress "@example.com"
            Assertions.assertResultError result
        
        [<Fact>]
        let ``Empty string should be invalid`` () =
            let result = ConstrainedTypes.createEmailAddress ""
            Assertions.assertResultError result
        
        [<Fact>]
        let ``Null should be invalid`` () =
            let result = ConstrainedTypes.createEmailAddress null
            Assertions.assertResultError result
    
    /// SpotifyUri validation tests
    module SpotifyUriTests =
        
        [<Fact>]
        let ``User URI should be valid`` () =
            let result = ConstrainedTypes.createSpotifyUri "spotify:user:username"
            Assertions.assertResultOk (SpotifyUri "spotify:user:username") result
        
        [<Fact>]
        let ``Track URI should be valid`` () =
            let result = ConstrainedTypes.createSpotifyUri "spotify:track:4iV5W9uYEdYUVa79Axb7Rh"
            Assertions.assertResultOk (SpotifyUri "spotify:track:4iV5W9uYEdYUVa79Axb7Rh") result
        
        [<Fact>]
        let ``Playlist URI should be valid`` () =
            let result = ConstrainedTypes.createSpotifyUri "spotify:playlist:37i9dQZF1DXcBWIGoYBM5M"
            Assertions.assertResultOk (SpotifyUri "spotify:playlist:37i9dQZF1DXcBWIGoYBM5M") result
        
        // [<Property>]
        // let ``Generated Spotify URIs should be valid`` () =
        //     Prop.forAll
        //         (Arb.fromGen Generators.validSpotifyUri)
        //         (fun validUri ->
        //             let result = ConstrainedTypes.createSpotifyUri validUri
        //             match result with
        //             | Ok (SpotifyUri uri) -> uri = validUri
        //             | Error _ -> false)
        
        [<Fact>]
        let ``HTTP URL should be invalid`` () =
            let result = ConstrainedTypes.createSpotifyUri "https://open.spotify.com/track/123"
            Assertions.assertResultError result
        
        [<Fact>]
        let ``Empty string should be invalid`` () =
            let result = ConstrainedTypes.createSpotifyUri ""
            Assertions.assertResultError result
        
        [<Fact>]
        let ``Random text should be invalid`` () =
            let result = ConstrainedTypes.createSpotifyUri "not-a-uri"
            Assertions.assertResultError result
    
    /// TrackCount validation tests
    module TrackCountTests =
        
        [<Fact>]
        let ``Zero tracks should be valid`` () =
            let result = ConstrainedTypes.createTrackCount 0
            Assertions.assertResultOk (TrackCount 0) result
        
        [<Fact>]
        let ``Positive track count should be valid`` () =
            let result = ConstrainedTypes.createTrackCount 42
            Assertions.assertResultOk (TrackCount 42) result
        
        [<Fact>]
        let ``Large track count should be valid`` () =
            let result = ConstrainedTypes.createTrackCount 10000
            Assertions.assertResultOk (TrackCount 10000) result
        
        [<Property>]
        let ``Non-negative integers should create TrackCount`` (NonNegativeInt count) =
            let result = ConstrainedTypes.createTrackCount count
            match result with
            | Ok (TrackCount c) -> c = count
            | Error _ -> false
        
        [<Fact>]
        let ``Negative track count should be invalid`` () =
            let result = ConstrainedTypes.createTrackCount -1
            Assertions.assertResultError result
        
        [<Fact>]
        let ``Large negative count should be invalid`` () =
            let result = ConstrainedTypes.createTrackCount -100
            Assertions.assertResultError result
    
    /// Domain validation tests
    module DomainValidationTests =
        
        [<Fact>]
        let ``Future token should not be expired`` () =
            let futureToken = {
                TestData.sampleTokenStorage with
                    ExpiresAt = System.DateTime.UtcNow.AddHours(1.0)
            }
            let isExpired = DomainValidation.isTokenExpired futureToken
            Assert.False(isExpired)
        
        [<Fact>]
        let ``Past token should be expired`` () =
            let isExpired = DomainValidation.isTokenExpired TestData.expiredTokenStorage
            Assert.True(isExpired)
        
        [<Fact>]
        let ``Token expiring now should be expired`` () =
            let nowToken = {
                TestData.sampleTokenStorage with
                    ExpiresAt = System.DateTime.UtcNow
            }
            let isExpired = DomainValidation.isTokenExpired nowToken
            Assert.True(isExpired)
        
        [<Fact>]
        let ``Valid profile should pass validation`` () =
            let result = DomainValidation.validateUserProfile TestData.sampleUserProfile
            Assertions.assertResultOk TestData.sampleUserProfile result
        
        // [<Property>]
        // let ``Generated valid profiles should pass validation`` () =
        //     Prop.forAll
        //         (Arb.fromGen Generators.validUserProfile)
        //         (fun profile ->
        //             let result = DomainValidation.validateUserProfile profile
        //             match result with
        //             | Ok _ -> true
        //             | Error _ -> false)
    
    /// Type extraction tests
    module TypeExtractionTests =
        
        [<Fact>]
        let ``String50 extraction should return original value`` () =
            let original = "test string"
            let string50 = String50 original
            let extracted = TypeExtraction.getString50 string50
            Assert.Equal(original, extracted)
        
        [<Fact>]
        let ``EmailAddress extraction should return original value`` () =
            let original = "test@example.com"
            let email = EmailAddress original
            let extracted = TypeExtraction.getEmailAddress email
            Assert.Equal(original, extracted)
        
        [<Fact>]
        let ``TrackCount extraction should return original value`` () =
            let original = 42
            let trackCount = TrackCount original
            let extracted = TypeExtraction.getTrackCount trackCount
            Assert.Equal(original, extracted)
    
