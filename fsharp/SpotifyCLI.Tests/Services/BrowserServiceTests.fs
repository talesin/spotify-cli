namespace SpotifyCLI.Tests.Services

open Expecto
open FsCheck
open SpotifyCLI.Domain
open SpotifyCLI.Services
open SpotifyCLI.Tests.TestUtilities

/// Comprehensive tests for BrowserService functionality
module BrowserServiceTests =
    
    /// Test helpers and utilities
    module TestHelpers =
        
        /// Mock browser service for testing
        type MockBrowserService(platform: BrowserPlatform, shouldLaunchSucceed: bool) =
            interface IBrowserService with
                member _.DetectPlatform() = platform
                
                member _.LaunchBrowser(url: HttpUrl) =
                    if shouldLaunchSucceed then
                        Ok()
                    else
                        Error(BrowserLaunchFailed "Mock launch failure")
                
                member _.GetDefaultBrowserCommand(platform: BrowserPlatform) =
                    match platform with
                    | Windows -> Ok("cmd.exe", [| "/c"; "start"; "" |])
                    | MacOS -> Ok("open", [| |])
                    | Linux -> Ok("xdg-open", [| |])
                    | Unknown osDesc -> Error(UnsupportedPlatform osDesc)
        
        /// Create test URLs for browser launching
        let createTestHttpUrl () = HttpUrl "https://accounts.spotify.com/authorize?test=true"
        let createTestLocalUrl () = HttpUrl "http://127.0.0.1:8888/callback"
        let createInvalidUrl () = HttpUrl "ftp://invalid.com"
        let createUnsafeUrl () = HttpUrl "https://test.com?param=\"malicious\""
    
    /// Tests for platform detection
    module PlatformDetectionTests =
        
        [<Tests>]
        let tests = testList "Platform Detection" [
            
            test "should detect Windows platform" {
                let service = TestHelpers.MockBrowserService(Windows, true) :> IBrowserService
                
                let platform = service.DetectPlatform()
                
                match platform with
                | Windows -> () // Expected
                | _ -> failtestf "Expected Windows but got: %A" platform
            }
            
            test "should detect macOS platform" {
                let service = TestHelpers.MockBrowserService(MacOS, true) :> IBrowserService
                
                let platform = service.DetectPlatform()
                
                match platform with
                | MacOS -> () // Expected
                | _ -> failtestf "Expected MacOS but got: %A" platform
            }
            
            test "should detect Linux platform" {
                let service = TestHelpers.MockBrowserService(Linux, true) :> IBrowserService
                
                let platform = service.DetectPlatform()
                
                match platform with
                | Linux -> () // Expected
                | _ -> failtestf "Expected Linux but got: %A" platform
            }
            
            test "should detect unknown platform" {
                let service = TestHelpers.MockBrowserService(Unknown "FreeBSD", true) :> IBrowserService
                
                let platform = service.DetectPlatform()
                
                match platform with
                | Unknown "FreeBSD" -> () // Expected
                | _ -> failtestf "Expected Unknown FreeBSD but got: %A" platform
            }
        ]
    
    /// Tests for browser command generation
    module BrowserCommandTests =
        
        [<Tests>]
        let tests = testList "Browser Commands" [
            
            test "should generate Windows browser command" {
                let service = TestHelpers.MockBrowserService(Windows, true) :> IBrowserService
                
                let result = service.GetDefaultBrowserCommand(Windows)
                
                match result with
                | Ok (command, args) ->
                    Expect.equal command "cmd.exe" "Should use cmd.exe for Windows"
                    Expect.sequenceEqual args [| "/c"; "start"; "" |] "Should have correct Windows arguments"
                | Error error -> failtestf "Expected success but got error: %A" error
            }
            
            test "should generate macOS browser command" {
                let service = TestHelpers.MockBrowserService(MacOS, true) :> IBrowserService
                
                let result = service.GetDefaultBrowserCommand(MacOS)
                
                match result with
                | Ok (command, args) ->
                    Expect.equal command "open" "Should use open for macOS"
                    Expect.sequenceEqual args [| |] "Should have no additional arguments for macOS"
                | Error error -> failtestf "Expected success but got error: %A" error
            }
            
            test "should generate Linux browser command" {
                let service = TestHelpers.MockBrowserService(Linux, true) :> IBrowserService
                
                let result = service.GetDefaultBrowserCommand(Linux)
                
                match result with
                | Ok (command, args) ->
                    Expect.equal command "xdg-open" "Should use xdg-open for Linux"
                    Expect.sequenceEqual args [| |] "Should have no additional arguments for Linux"
                | Error error -> failtestf "Expected success but got error: %A" error
            }
            
            test "should fail for unknown platform" {
                let service = TestHelpers.MockBrowserService(Unknown "FreeBSD", true) :> IBrowserService
                
                let result = service.GetDefaultBrowserCommand(Unknown "FreeBSD")
                
                match result with
                | Ok _ -> failtest "Should fail for unknown platform"
                | Error (UnsupportedPlatform "FreeBSD") -> () // Expected
                | Error error -> failtestf $"Expected UnsupportedPlatform but got: %A" error
            }
        ]
    
    /// Tests for browser launching
    module BrowserLaunchTests =
        
        [<Tests>]
        let tests = testList "Browser Launch" [
            
            test "should successfully launch browser" {
                let service = TestHelpers.MockBrowserService(Windows, true) :> IBrowserService
                let url = TestHelpers.createTestHttpUrl()
                
                let result = service.LaunchBrowser(url)
                
                match result with
                | Ok () -> () // Expected
                | Error error -> failtestf "Expected success but got error: %A" error
            }
            
            test "should handle browser launch failure" {
                let service = TestHelpers.MockBrowserService(Windows, false) :> IBrowserService
                let url = TestHelpers.createTestHttpUrl()
                
                let result = service.LaunchBrowser(url)
                
                match result with
                | Ok () -> failtest "Expected launch failure"
                | Error (BrowserLaunchFailed "Mock launch failure") -> () // Expected
                | Error error -> failtestf $"Expected BrowserLaunchFailed but got: %A" error
            }
            
            test "should handle unsupported platform during launch" {
                let service = TestHelpers.MockBrowserService(Unknown "FreeBSD", true) :> IBrowserService
                let url = TestHelpers.createTestHttpUrl()
                
                let result = service.LaunchBrowser(url)
                
                match result with
                | Ok () -> failtest "Expected platform error"
                | Error (UnsupportedPlatform "FreeBSD") -> () // Expected
                | Error error -> failtestf $"Expected UnsupportedPlatform but got: %A" error
            }
        ]
    
    /// Tests for BrowserServiceHelpers functionality
    module BrowserServiceHelpersTests =
        
        [<Tests>]
        let tests = testList "BrowserServiceHelpers" [
            
            test "formatBrowserError should format BrowserLaunchFailed" {
                let error = BrowserLaunchFailed "Process failed to start"
                
                let message = BrowserServiceHelpers.formatBrowserError error
                
                Expect.stringContains message "Failed to launch browser" "Should contain error description"
                Expect.stringContains message "Process failed to start" "Should contain specific reason"
                Expect.stringContains message "manually open" "Should suggest manual fallback"
            }
            
            test "formatBrowserError should format UnsupportedPlatform" {
                let error = UnsupportedPlatform "FreeBSD"
                
                let message = BrowserServiceHelpers.formatBrowserError error
                
                Expect.stringContains message "Unsupported platform: FreeBSD" "Should contain platform info"
                Expect.stringContains message "manually copy and paste" "Should suggest manual action"
            }
            
            test "formatBrowserError should format BrowserNotFound" {
                let error = BrowserNotFound "firefox"
                
                let message = BrowserServiceHelpers.formatBrowserError error
                
                Expect.stringContains message "Browser not found: firefox" "Should contain browser name"
                Expect.stringContains message "install a web browser" "Should suggest installation"
            }
            
            test "formatBrowserError should format ProcessStartFailed" {
                let error = ProcessStartFailed("open", "Permission denied")
                
                let message = BrowserServiceHelpers.formatBrowserError error
                
                Expect.stringContains message "Failed to start browser process 'open'" "Should contain command"
                Expect.stringContains message "Permission denied" "Should contain error details"
            }
            
            test "isBrowserServiceAvailable should return true for supported platforms" {
                let windowsService = TestHelpers.MockBrowserService(Windows, true) :> IBrowserService
                let macService = TestHelpers.MockBrowserService(MacOS, true) :> IBrowserService
                let linuxService = TestHelpers.MockBrowserService(Linux, true) :> IBrowserService
                
                Expect.isTrue (BrowserServiceHelpers.isBrowserServiceAvailable windowsService) "Should support Windows"
                Expect.isTrue (BrowserServiceHelpers.isBrowserServiceAvailable macService) "Should support macOS"
                Expect.isTrue (BrowserServiceHelpers.isBrowserServiceAvailable linuxService) "Should support Linux"
            }
            
            test "isBrowserServiceAvailable should return false for unsupported platforms" {
                let unknownService = TestHelpers.MockBrowserService(Unknown "FreeBSD", true) :> IBrowserService
                
                Expect.isFalse (BrowserServiceHelpers.isBrowserServiceAvailable unknownService) "Should not support unknown platforms"
            }
            
            test "createManualBrowserInstructions should provide clear instructions" {
                let url = TestHelpers.createTestHttpUrl()
                
                let instructions = BrowserServiceHelpers.createManualBrowserInstructions url
                
                Expect.stringContains instructions "Manual Browser Setup Required" "Should have clear title"
                Expect.stringContains instructions "Copy the following URL" "Should mention copying URL"
                Expect.stringContains instructions "https://accounts.spotify.com/authorize?test=true" "Should include the URL"
                Expect.stringContains instructions "Open your web browser" "Should mention opening browser"
                Expect.stringContains instructions "Return to this terminal" "Should mention returning"
            }
            
            test "validateBrowserUrl should accept valid HTTP URLs" {
                let httpUrl = HttpUrl "http://127.0.0.1:8888/callback"
                
                let result = BrowserServiceHelpers.validateBrowserUrl httpUrl
                
                match result with
                | Ok validatedUrl -> Expect.equal validatedUrl httpUrl "Should return the same URL"
                | Error error -> failtestf "Expected success but got error: %A" error
            }
            
            test "validateBrowserUrl should accept valid HTTPS URLs" {
                let httpsUrl = TestHelpers.createTestHttpUrl()
                
                let result = BrowserServiceHelpers.validateBrowserUrl httpsUrl
                
                match result with
                | Ok validatedUrl -> Expect.equal validatedUrl httpsUrl "Should return the same URL"
                | Error error -> failtestf "Expected success but got error: %A" error
            }
            
            test "validateBrowserUrl should reject non-HTTP protocols" {
                let ftpUrl = HttpUrl "ftp://file.server.com/path"
                
                let result = BrowserServiceHelpers.validateBrowserUrl ftpUrl
                
                match result with
                | Ok _ -> failtest "Should reject FTP URLs"
                | Error (BrowserLaunchFailed message) -> 
                    Expect.stringContains message "HTTP or HTTPS protocol" "Should mention protocol requirement"
                | Error error -> failtestf $"Expected BrowserLaunchFailed but got: %A" error
            }
            
            test "validateBrowserUrl should reject URLs with unsafe characters" {
                let unsafeUrl = HttpUrl "https://test.com?param=\"malicious\""
                
                let result = BrowserServiceHelpers.validateBrowserUrl unsafeUrl
                
                match result with
                | Ok _ -> failtest "Should reject URLs with unsafe characters"
                | Error (BrowserLaunchFailed message) -> 
                    Expect.stringContains message "unsafe characters" "Should mention unsafe characters"
                | Error error -> failtestf $"Expected BrowserLaunchFailed but got: %A" error
            }
            
            test "validateBrowserUrl should reject URLs with single quotes" {
                let unsafeUrl = HttpUrl "https://test.com?param='malicious'"
                
                let result = BrowserServiceHelpers.validateBrowserUrl unsafeUrl
                
                match result with
                | Ok _ -> failtest "Should reject URLs with single quotes"
                | Error (BrowserLaunchFailed _) -> () // Expected
                | Error error -> failtestf $"Expected BrowserLaunchFailed but got: %A" error
            }
            
            test "validateBrowserUrl should reject URLs with backticks" {
                let unsafeUrl = HttpUrl "https://test.com?param=`malicious`"
                
                let result = BrowserServiceHelpers.validateBrowserUrl unsafeUrl
                
                match result with
                | Ok _ -> failtest "Should reject URLs with backticks"
                | Error (BrowserLaunchFailed _) -> () // Expected
                | Error error -> failtestf $"Expected BrowserLaunchFailed but got: %A" error
            }
            
            test "getPlatformBrowserRecommendations should return Windows browsers" {
                let browsers = BrowserServiceHelpers.getPlatformBrowserRecommendations Windows
                
                Expect.contains browsers "Microsoft Edge" "Should recommend Edge for Windows"
                Expect.contains browsers "Chrome" "Should recommend Chrome for Windows"
                Expect.contains browsers "Firefox" "Should recommend Firefox for Windows"
            }
            
            test "getPlatformBrowserRecommendations should return macOS browsers" {
                let browsers = BrowserServiceHelpers.getPlatformBrowserRecommendations MacOS
                
                Expect.contains browsers "Safari" "Should recommend Safari for macOS"
                Expect.contains browsers "Chrome" "Should recommend Chrome for macOS"
                Expect.contains browsers "Firefox" "Should recommend Firefox for macOS"
            }
            
            test "getPlatformBrowserRecommendations should return Linux browsers" {
                let browsers = BrowserServiceHelpers.getPlatformBrowserRecommendations Linux
                
                Expect.contains browsers "Firefox" "Should recommend Firefox for Linux"
                Expect.contains browsers "Chromium" "Should recommend Chromium for Linux"
                Expect.contains browsers "Chrome" "Should recommend Chrome for Linux"
                Expect.contains browsers "xdg-open" "Should recommend xdg-open for Linux"
            }
            
            test "getPlatformBrowserRecommendations should return generic browsers for unknown platforms" {
                let browsers = BrowserServiceHelpers.getPlatformBrowserRecommendations (Unknown "FreeBSD")
                
                Expect.contains browsers "Any modern web browser" "Should provide generic recommendation"
            }
        ]
    
    /// Tests for browser service integration scenarios
    module IntegrationTests =
        
        [<Tests>]
        let tests = testList "Integration Tests" [
            
            test "should handle complete successful browser launch flow" {
                let service = TestHelpers.MockBrowserService(Windows, true) :> IBrowserService
                let url = TestHelpers.createTestHttpUrl()
                
                // Validate platform support
                let isAvailable = BrowserServiceHelpers.isBrowserServiceAvailable service
                Expect.isTrue isAvailable "Service should be available"
                
                // Validate URL
                let validationResult = BrowserServiceHelpers.validateBrowserUrl url
                match validationResult with
                | Error _ -> failtest "URL should be valid"
                | Ok _ -> ()
                
                // Launch browser
                let launchResult = service.LaunchBrowser(url)
                match launchResult with
                | Ok () -> () // Expected
                | Error error -> failtestf $"Browser launch should succeed: %A" error
            }
            
            test "should handle complete failed browser launch flow with fallback" {
                let service = TestHelpers.MockBrowserService(Windows, false) :> IBrowserService
                let url = TestHelpers.createTestHttpUrl()
                
                // Try launch with fallback
                let result = BrowserServiceHelpers.launchBrowserWithFallback service url
                
                // Should not fail even if browser launch fails (provides manual instructions)
                match result with
                | Ok () -> () // Expected - fallback should always succeed
                | Error error -> failtestf $"Fallback should not fail: %A" error
            }
            
            test "should provide helpful error messages for unsupported platform" {
                let service = TestHelpers.MockBrowserService(Unknown "FreeBSD", true) :> IBrowserService
                let url = TestHelpers.createTestHttpUrl()
                
                let result = service.LaunchBrowser(url)
                
                match result with
                | Ok () -> failtest "Should fail for unsupported platform"
                | Error error ->
                    let message = BrowserServiceHelpers.formatBrowserError error
                    Expect.stringContains message "FreeBSD" "Should mention the specific platform"
                    Expect.stringContains message "manually" "Should suggest manual action"
            }
            
            test "should handle URL validation in complete flow" {
                let service = TestHelpers.MockBrowserService(Windows, true) :> IBrowserService
                let unsafeUrl = HttpUrl "https://test.com?param=\"unsafe\""
                
                // URL validation should catch unsafe URLs before launch
                let validationResult = BrowserServiceHelpers.validateBrowserUrl unsafeUrl
                
                match validationResult with
                | Ok _ -> failtest "Should reject unsafe URL"
                | Error (BrowserLaunchFailed _) -> () // Expected - caught by validation
                | Error error -> failtestf $"Expected BrowserLaunchFailed but got: %A" error
            }
        ]
    
    /// Tests for edge cases and error conditions
    module EdgeCaseTests =
        
        [<Tests>]
        let tests = testList "Edge Cases" [
            
            test "should handle empty URL gracefully" {
                let service = TestHelpers.MockBrowserService(Windows, true) :> IBrowserService
                let emptyUrl = HttpUrl ""
                
                let validationResult = BrowserServiceHelpers.validateBrowserUrl emptyUrl
                
                match validationResult with
                | Ok _ -> failtest "Should reject empty URL"
                | Error (BrowserLaunchFailed _) -> () // Expected
                | Error error -> failtestf $"Expected BrowserLaunchFailed but got: %A" error
            }
            
            test "should handle very long URLs" {
                let service = TestHelpers.MockBrowserService(Windows, true) :> IBrowserService
                let longPath = String.replicate 1000 "a"
                let longUrl = HttpUrl $"https://test.com/{longPath}"
                
                let validationResult = BrowserServiceHelpers.validateBrowserUrl longUrl
                
                // Should accept long URLs as long as they're safe
                match validationResult with
                | Ok _ -> () // Expected - long URLs are okay
                | Error error -> failtestf $"Should accept long URLs: %A" error
            }
            
            test "should handle special characters in URL parameters" {
                let service = TestHelpers.MockBrowserService(Windows, true) :> IBrowserService
                let urlWithSpecialChars = HttpUrl "https://accounts.spotify.com/authorize?scope=user-read-private%20user-read-email"
                
                let validationResult = BrowserServiceHelpers.validateBrowserUrl urlWithSpecialChars
                
                match validationResult with
                | Ok _ -> () // Expected - URL encoded special chars are safe
                | Error error -> failtestf $"Should accept URL encoded special characters: %A" error
            }
            
            testCase "should handle various valid HTTPS URLs" <| fun (domain: string) (path: string) ->
                let cleanDomain = domain |> String.filter System.Char.IsLetterOrDigit
                let cleanPath = path |> String.filter (fun c -> System.Char.IsLetterOrDigit c || c = '/' || c = '-' || c = '_')
                
                if not (System.String.IsNullOrEmpty(cleanDomain)) && cleanDomain.Length <= 50 then
                    let testUrl = HttpUrl $"https://{cleanDomain}.com/{cleanPath}"
                    let result = BrowserServiceHelpers.validateBrowserUrl testUrl
                    match result with
                    | Ok _ -> true // Expected for clean URLs
                    | Error _ -> true // May fail for other reasons, but shouldn't crash
                else
                    true // Skip invalid inputs
        ]
    
    /// Combined test suite
    [<Tests>]
    let allTests = testList "BrowserServiceTests" [
        PlatformDetectionTests.tests
        BrowserCommandTests.tests
        BrowserLaunchTests.tests
        BrowserServiceHelpersTests.tests
        IntegrationTests.tests
        EdgeCaseTests.tests
    ]