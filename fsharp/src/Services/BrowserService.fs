namespace SpotifyCLI.Services

open System
open System.Diagnostics
open System.IO
open System.Runtime.InteropServices
open FsToolkit.ErrorHandling
open SpotifyCLI.Domain

/// Browser platform detection results
type BrowserPlatform =
    | Windows
    | MacOS  
    | Linux
    | Unknown of osDescription: string


/// Browser service interface for dependency injection
type IBrowserService =
    abstract DetectPlatform: unit -> BrowserPlatform
    abstract LaunchBrowser: HttpUrl -> Result<unit, BrowserError>
    abstract GetDefaultBrowserCommand: BrowserPlatform -> Result<string * string[], BrowserError>

/// System browser service implementation using platform-specific commands
type SystemBrowserService() =
    
    /// Detect the current operating system platform
    let detectCurrentPlatform () : BrowserPlatform =
        if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
            Windows
        elif RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
            MacOS
        elif RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
            Linux
        else
            Unknown(RuntimeInformation.OSDescription)
    
    /// Get platform-specific browser launch command and arguments
    let getBrowserCommand (platform: BrowserPlatform) : Result<string * string[], BrowserError> =
        match platform with
        | Windows -> Ok("cmd.exe", [| "/c"; "start"; "" |])
        | MacOS -> Ok("open", [| |])
        | Linux -> 
            // Try common Linux browsers in order of preference
            let browsers = [ "xdg-open"; "x-www-browser"; "firefox"; "chromium"; "google-chrome" ]
            let availableBrowser = 
                browsers
                |> List.tryFind (fun browser ->
                    try
                        use proc = new Process()
                        proc.StartInfo.FileName <- "which"
                        proc.StartInfo.Arguments <- browser
                        proc.StartInfo.UseShellExecute <- false
                        proc.StartInfo.RedirectStandardOutput <- true
                        proc.StartInfo.CreateNoWindow <- true
                        proc.Start() |> ignore
                        proc.WaitForExit()
                        proc.ExitCode = 0
                    with
                    | _ -> false)
            
            match availableBrowser with
            | Some browser -> Ok(browser, [| |])
            | None -> Error(BrowserNotFound("No suitable browser found on Linux system"))
        | Unknown osDesc -> Error(UnsupportedPlatform osDesc)
    
    /// Launch browser with URL using platform-appropriate command
    let launchBrowserWithUrl (command: string) (args: string[]) (url: HttpUrl) : Result<unit, BrowserError> =
        try
            let urlString = TypeExtraction.getHttpUrl url
            use proc = new Process()
            proc.StartInfo.FileName <- command
            
            // Construct arguments based on platform
            let allArgs = Array.append args [| urlString |]
            proc.StartInfo.Arguments <- String.Join(" ", allArgs)
            
            proc.StartInfo.UseShellExecute <- false
            proc.StartInfo.CreateNoWindow <- true
            
            let started = proc.Start()
            if started then
                Ok()
            else
                Error(ProcessStartFailed(command, "Process.Start() returned false"))
        with
        | :? FileNotFoundException -> 
            Error(BrowserNotFound($"Browser command not found: {command}"))
        | :? System.ComponentModel.Win32Exception as ex ->
            Error(ProcessStartFailed(command, ex.Message))
        | ex ->
            Error(BrowserLaunchFailed($"Unexpected error launching browser: {ex.Message}"))
    
    interface IBrowserService with
        
        member _.DetectPlatform() = detectCurrentPlatform()
        
        member _.LaunchBrowser(url: HttpUrl) =
            let platform = detectCurrentPlatform()
            result {
                let! (command, args) = getBrowserCommand platform
                return! launchBrowserWithUrl command args url
            }
        
        member _.GetDefaultBrowserCommand(platform: BrowserPlatform) =
            getBrowserCommand platform

/// Factory functions for creating browser service
module BrowserService =
    
    let create () : IBrowserService =
        SystemBrowserService() :> IBrowserService

/// Browser service utilities and helpers
module BrowserServiceHelpers =
    
    /// Format browser error for user display with recovery suggestions
    let formatBrowserError = function
        | BrowserLaunchFailed reason -> 
            $"Failed to launch browser: {reason}\nPlease manually open the authorization URL in your browser."
        | UnsupportedPlatform platform -> 
            $"Unsupported platform: {platform}\nPlease manually copy and paste the authorization URL into your browser."
        | BrowserNotFound browserName -> 
            $"Browser not found: {browserName}\nPlease install a web browser or manually open the authorization URL."
        | ProcessStartFailed(command, error) -> 
            $"Failed to start browser process '{command}': {error}\nPlease manually open the authorization URL in your browser."
    
    /// Check if browser service is available on current platform
    let isBrowserServiceAvailable (browserService: IBrowserService) : bool =
        match browserService.DetectPlatform() with
        | Windows | MacOS | Linux -> true
        | Unknown _ -> false
    
    /// Try to launch browser with fallback to manual instruction
    let launchBrowserWithFallback (browserService: IBrowserService) (url: HttpUrl) : Result<unit, BrowserError> =
        match browserService.LaunchBrowser(url) with
        | Ok() -> Ok()
        | Error err -> 
            // Provide helpful fallback message
            let urlString = TypeExtraction.getHttpUrl url
            Console.WriteLine()
            Console.WriteLine("⚠️  Automatic browser launch failed")
            Console.WriteLine($"   {formatBrowserError err}")
            Console.WriteLine()
            Console.WriteLine("📋 Please manually copy and paste this URL into your browser:")
            Console.WriteLine($"   {urlString}")
            Console.WriteLine()
            Ok() // Don't fail the auth flow, user can manually open
    
    /// Create browser launch instructions for manual process
    let createManualBrowserInstructions (url: HttpUrl) : string =
        let urlString = TypeExtraction.getHttpUrl url
        [
            "Manual Browser Setup Required:"
            "1. Copy the following URL:"
            $"   {urlString}"
            "2. Open your web browser"
            "3. Paste the URL into the address bar"
            "4. Follow the Spotify authorization prompts"
            "5. Return to this terminal when complete"
        ] |> String.concat "\n"
    
    /// Validate that URL is suitable for browser launching
    let validateBrowserUrl (url: HttpUrl) : Result<HttpUrl, BrowserError> =
        let urlString = TypeExtraction.getHttpUrl url
        
        // Basic validation for browser-safe URLs
        if urlString.StartsWith("http://") || urlString.StartsWith("https://") then
            // Check for potentially dangerous characters that might break shell commands
            if urlString.Contains("\"") || urlString.Contains("'") || urlString.Contains("`") then
                Error(BrowserLaunchFailed("URL contains potentially unsafe characters"))
            else
                Ok(url)
        else
            Error(BrowserLaunchFailed("URL must use HTTP or HTTPS protocol"))
    
    /// Get platform-specific browser recommendations
    let getPlatformBrowserRecommendations (platform: BrowserPlatform) : string list =
        match platform with
        | Windows -> [ "Microsoft Edge"; "Chrome"; "Firefox" ]
        | MacOS -> [ "Safari"; "Chrome"; "Firefox" ]
        | Linux -> [ "Firefox"; "Chromium"; "Chrome"; "xdg-open" ]
        | Unknown _ -> [ "Any modern web browser" ]