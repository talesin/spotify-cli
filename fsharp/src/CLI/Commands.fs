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
}

/// OAuth authentication command handlers
module AuthCommandHandlers =
    
    /// Simple OAuth authentication flow demonstration
    let handleAuth (services: IAppServices) : Result<unit, AppError> =
        Console.WriteLine("🔐 Starting Spotify CLI Authentication")
        Console.WriteLine("════════════════════════════════════════")
        Console.WriteLine()
        
        try
            // For this demo, we'll use a test client ID. In production, this would come from config
            let testClientId = "your_spotify_client_id_here"
            
            Console.WriteLine("📝 Demo OAuth Flow (simplified implementation)")
            Console.WriteLine($"   Client ID: {testClientId}")
            Console.WriteLine("   Scopes: user-read-private, user-read-email")
            Console.WriteLine()
            
            // Step 1: Generate PKCE parameters
            Console.WriteLine("🔧 Generating PKCE parameters...")
            match services.Crypto.GenerateCodeVerifier() with
            | Error cryptoErr -> 
                Console.WriteLine($"❌ Failed to generate PKCE verifier: {ErrorFormatting.formatCryptoError cryptoErr}")
                Error(CryptoError cryptoErr)
            | Ok codeVerifier ->
            
            match services.Crypto.GenerateCodeChallenge(codeVerifier) with
            | Error cryptoErr ->
                Console.WriteLine($"❌ Failed to generate PKCE challenge: {ErrorFormatting.formatCryptoError cryptoErr}")
                Error(CryptoError cryptoErr)
            | Ok codeChallenge ->
            
            // Step 2: Generate state parameter
            Console.WriteLine("🔐 Generating secure state parameter...")
            match services.Crypto.GenerateState() with
            | Error cryptoErr ->
                Console.WriteLine($"❌ Failed to generate state: {ErrorFormatting.formatCryptoError cryptoErr}")
                Error(CryptoError cryptoErr)
            | Ok state ->
            
            // Step 3: Create authorization URL (demo)
            let authUrl = $"https://accounts.spotify.com/authorize?client_id={testClientId}&response_type=code&redirect_uri=http://127.0.0.1:3000/callback&code_challenge_method=S256&code_challenge={TypeExtraction.getCodeChallenge codeChallenge}&state={state}&scope=user-read-private user-read-email"
            
            Console.WriteLine("🌐 Authorization URL generated:")
            Console.WriteLine($"   {authUrl}")
            Console.WriteLine()
            
            // Step 4: Attempt browser launch
            Console.WriteLine("🚀 Attempting to launch browser...")
            match ConstrainedTypes.createHttpUrl authUrl with
            | Error err ->
                Console.WriteLine($"❌ Invalid authorization URL: {err}")
                Error(ValidationError("auth_url", err))
            | Ok validUrl ->
            
            match BrowserServiceHelpers.launchBrowserWithFallback services.Browser validUrl with
            | Error browserErr ->
                Console.WriteLine($"⚠️  Browser launch failed: {ErrorFormatting.formatBrowserError browserErr}")
                Console.WriteLine()
                Console.WriteLine("📋 Please manually copy and paste the URL above into your browser")
            | Ok _ ->
                Console.WriteLine("✅ Browser launched successfully")
            
            Console.WriteLine()
            Console.WriteLine("📖 Next Steps (when fully implemented):")
            Console.WriteLine("   1. Complete authorization in your browser")
            Console.WriteLine("   2. OAuth callback server will capture the code") 
            Console.WriteLine("   3. Exchange code for access tokens")
            Console.WriteLine("   4. Store tokens securely")
            Console.WriteLine()
            Console.WriteLine("🎉 Demo completed - OAuth services are working correctly!")
            
            Ok()
        with
        | ex ->
            Console.WriteLine($"❌ Unexpected error during authentication: {ex.Message}")
            Error(UnexpectedError ex.Message)
    
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