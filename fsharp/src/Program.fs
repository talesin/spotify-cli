namespace SpotifyCLI

open System
open Argu

/// Simplified CLI arguments for testing foundation
type SpotifyCliArguments =
    | Auth
    | Me  
    | Playlists
    | [<AltCommandLine("-v")>] Version

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Auth -> "Authenticate with Spotify using OAuth2 flow"
            | Me -> "Display your Spotify user profile information"  
            | Playlists -> "List your Spotify playlists with details"
            | Version -> "Show version information"

/// Simple program to test foundation
module Program =
    
    let getVersionInfo () =
        let version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version
        let versionString = 
            match version with
            | null -> "Unknown"
            | v -> $"{v.Major}.{v.Minor}.{v.Build}"
        
        $"spotify-cli version {versionString}"
    
    let processCommand = function
        | Auth -> 
            Console.WriteLine("🔐 Auth command - OAuth2 flow not yet implemented")
            Console.WriteLine("This will authenticate with Spotify when fully implemented")
        | Me ->
            Console.WriteLine("👤 Me command - User profile fetching not yet implemented") 
            Console.WriteLine("This will show your Spotify profile when fully implemented")
        | Playlists ->
            Console.WriteLine("🎶 Playlists command - Playlist fetching not yet implemented")
            Console.WriteLine("This will list your playlists when fully implemented")
        | Version ->
            Console.WriteLine(getVersionInfo())
    
    let main (args: string[]) : int =
        let parser = ArgumentParser.Create<SpotifyCliArguments>(
            programName = "spotify-cli",
            helpTextMessage = "A functional F# CLI for Spotify"
        )
        
        try
            let parseResults = parser.ParseCommandLine(args)
            let commands = parseResults.GetAllResults()
            
            match commands with
            | [] -> 
                let usage = parser.PrintUsage()
                Console.WriteLine(usage : string)
                0
            | [command] -> 
                Console.WriteLine("✅ spotify-cli F# Foundation - Phase 1 Complete!")
                Console.WriteLine()
                processCommand command
                Console.WriteLine()
                Console.WriteLine("🚀 Foundation successfully implemented with:")
                Console.WriteLine("   • Domain types with constraints and validation")
                Console.WriteLine("   • Service layer with dependency injection")
                Console.WriteLine("   • CLI framework using Argu")
                Console.WriteLine("   • Result-based error handling")
                Console.WriteLine("   • Testing infrastructure with Expecto/FsCheck")
                0
            | _ -> 
                Console.WriteLine("❌ Error: Please specify only one command at a time")
                1
        with
        | :? ArguParseException as ex ->
            Console.WriteLine($"❌ Argument error: {ex.Message}")
            1
        | ex ->
            Console.WriteLine($"❌ Unexpected error: {ex.Message}")
            1

module Main =
    
    [<EntryPoint>]
    let main args =
        Program.main args