namespace SpotifyCLI.CLI

open System
open Argu
open SpotifyCLI.Domain
open SpotifyCLI.Services

/// Simplified CLI command definitions for service integration
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

/// Application services interface for dependency injection
type IAppServices = {
    Config: IConfigService
    Http: IHttpService  
    Crypto: ICryptoService
    Browser: IBrowserService
    OAuth: IOAuthService
    AuthWorkflow: IAuthenticationWorkflowService
}

/// OAuth authentication command handlers
module AuthCommandHandlers =
    
    /// Real OAuth authentication flow using AuthenticationWorkflow service
    let handleAuth (services: IAppServices) : Result<unit, AppError> =
        async {
            try
                // Check if environment variables are configured
                match services.Config.GetSpotifyClientId() with
                | Error configErr ->
                    Console.WriteLine("❌ Environment configuration missing:")
                    Console.WriteLine($"   {ErrorFormatting.formatConfigError configErr}")
                    Console.WriteLine()
                    Console.WriteLine("💡 Please ensure your .envrc file is loaded:")
                    Console.WriteLine("   1. Check that .envrc exists in your project root")
                    Console.WriteLine("   2. Run 'direnv allow' to load environment variables")
                    Console.WriteLine("   3. Verify SPOTIFY_CLIENT_ID is set")
                    return Error(AppError.ConfigError(ConfigError.InvalidFormat "Environment variables not configured"))
                | Ok clientId ->
                
                // Create authentication workflow configuration
                let config = AuthenticationWorkflowHelpers.createDefaultConfig clientId
                
                // Run the complete authentication flow
                let! result = services.AuthWorkflow.StartAuthenticationFlow(config) |> Async.AwaitTask
                
                match result with
                | Ok authResult ->
                    Console.WriteLine()
                    Console.WriteLine(authResult.UserMessage)
                    Console.WriteLine($"📁 Tokens saved to: {authResult.ConfigPath}")
                    Console.WriteLine()
                    Console.WriteLine("✨ You can now use other spotify-cli commands like 'me' and 'playlists'!")
                    return Ok()
                | Error authErr ->
                    Console.WriteLine()
                    Console.WriteLine($"❌ Authentication failed: {AuthenticationWorkflowHelpers.formatAuthWorkflowError authErr}")
                    Console.WriteLine()
                    Console.WriteLine("💡 Common issues:")
                    Console.WriteLine("   - Check your internet connection")
                    Console.WriteLine("   - Verify environment variables are loaded")
                    Console.WriteLine("   - Ensure callback server port (3000) is available")
                    return Error(AppError.AuthWorkflowError authErr)
            with
            | ex ->
                Console.WriteLine($"❌ Unexpected error during authentication: {ex.Message}")
                return Error(AppError.UnexpectedError ex.Message)
        } |> Async.RunSynchronously
    
    let handleMe (services: IAppServices) : Result<UserProfile, AppError> =
        Console.WriteLine("👤 Me command - Service layer ready, API integration needed")
        // Return sample user profile for demonstration
        Ok({
            DisplayName = Some(String50 "Test User")
            Email = EmailAddress "test@example.com"
            Country = Some "US"
            SpotifyUri = SpotifyUri "spotify:user:testuser"
            Id = "testuser123"
            Followers = Some 42
        })
    
    let handlePlaylists (services: IAppServices) : Result<PlaylistInfo list, AppError> =
        Console.WriteLine("🎶 Playlists command - Service layer ready, API integration needed")
        // Return sample playlists for demonstration
        Ok([{
            Name = String50 "My Playlist"
            TrackCount = TrackCount 25
            Visibility = Public
            Id = "playlist123"
            SpotifyUri = SpotifyUri "spotify:playlist:playlist123"
            Description = Some "A test playlist"
        }])

/// Simple command processor for service integration testing
module SimpleCommandProcessor =
    
    let processCommand (services: IAppServices) = function
        | Auth -> AuthCommandHandlers.handleAuth services |> ignore
        | Me -> AuthCommandHandlers.handleMe services |> ignore
        | Playlists -> AuthCommandHandlers.handlePlaylists services |> ignore
        | Version -> Console.WriteLine("spotify-cli version 1.0.0 - OAuth Services Integrated")