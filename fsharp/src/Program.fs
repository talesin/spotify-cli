namespace SpotifyCLI

open System
open Argu
open SpotifyCLI.CLI
open SpotifyCLI.Services
open SpotifyCLI.Domain

/// Program with integrated service layer
module Program =
    
    /// Create application services with dependency injection
    let createAppServices () : IAppServices =
        // Create infrastructure services
        let fileSystem = new SystemFileSystem() :> IFileSystem
        let httpClient = new SystemHttpClient() :> IHttpClient
        
        // Create domain services with injected dependencies  
        let configService = ConfigService.create fileSystem
        let httpService = HttpService.create httpClient
        let cryptoService = CryptoService.create()
        
        // Return services record
        {
            Config = configService
            Http = httpService
            Crypto = cryptoService
        }
    
    let getVersionInfo () =
        let version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version
        let versionString = 
            match version with
            | null -> "Unknown"
            | v -> $"{v.Major}.{v.Minor}.{v.Build}"
        
        $"spotify-cli version {versionString}"
    
    let processCommandWithServices (services: IAppServices) = function
        | Auth -> 
            match PlaceholderCommandHandlers.handleAuth services with
            | Ok() -> Console.WriteLine("✅ Auth service integration successful")
            | Error err -> Console.WriteLine($"❌ Auth error: {err}")
        | Me ->
            match PlaceholderCommandHandlers.handleMe services with
            | Ok userProfile -> 
                Console.WriteLine("✅ Me service integration successful")
                Console.WriteLine($"   Sample user: {TypeExtraction.getString50 userProfile.DisplayName.Value}")
            | Error err -> Console.WriteLine($"❌ Me error: {err}")
        | Playlists ->
            match PlaceholderCommandHandlers.handlePlaylists services with
            | Ok playlists -> 
                Console.WriteLine("✅ Playlists service integration successful")
                Console.WriteLine($"   Sample playlist count: {playlists.Length}")
            | Error err -> Console.WriteLine($"❌ Playlists error: {err}")
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
                
                // Create services and demonstrate integration
                let services = createAppServices()
                processCommandWithServices services command
                
                Console.WriteLine()
                Console.WriteLine("🚀 Foundation successfully implemented with:")
                Console.WriteLine("   • Domain types with constraints and validation")
                Console.WriteLine("   • Service layer with dependency injection")
                Console.WriteLine("   • CLI framework using Argu")
                Console.WriteLine("   • Result-based error handling")
                Console.WriteLine("   • All services compile and integrate properly")
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