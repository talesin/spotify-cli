namespace SpotifyCLI.Services

open System
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open SpotifyCLI.Domain
open SpotifyCLI.Infrastructure

/// User profile service interface
type IUserProfileService =
    abstract GetCurrentUserProfile: unit -> Task<Result<UserProfile, UserProfileError>>
    abstract EnsureValidAuthentication: unit -> Task<Result<TokenStorage, UserProfileError>>

/// User profile service implementation with automatic token refresh
type UserProfileService(configService: IConfigService, oauthService: IOAuthService, spotifyApiClient: ISpotifyApiClient) =
    
    /// Check if token needs refresh (expired or within 5 minutes of expiry)
    let isTokenNearExpiry (tokens: TokenStorage) : bool =
        let fiveMinutesFromNow = DateTime.UtcNow.AddMinutes(5.0)
        tokens.ExpiresAt <= fiveMinutesFromNow
    
    /// Get OAuth configuration for token refresh
    let getOAuthConfig () : Result<SpotifyOAuthConfig, UserProfileError> =
        result {
            let! clientId = 
                configService.GetSpotifyClientId()
                |> Result.mapError (ConfigurationError)
            
            let! clientSecret = 
                configService.GetSpotifyClientSecret()
                |> Result.mapError (ConfigurationError)
            
            let! redirectUri = 
                configService.GetSpotifyRedirectUri()
                |> Result.mapError (ConfigurationError)
                |> Result.bind (fun uri -> 
                    ConstrainedTypes.createHttpUrl uri
                    |> Result.mapError (fun err -> ConfigurationError(ConfigError.InvalidFormat($"Invalid redirect URI: {err}"))))
            
            let! authBaseUrl = 
                ConstrainedTypes.createHttpUrl "https://accounts.spotify.com/authorize"
                |> Result.mapError (fun err -> ConfigurationError(ConfigError.InvalidFormat($"Invalid authorization URL: {err}")))
            
            let! tokenBaseUrl = 
                ConstrainedTypes.createHttpUrl "https://accounts.spotify.com/api/token"
                |> Result.mapError (fun err -> ConfigurationError(ConfigError.InvalidFormat($"Invalid token URL: {err}")))
            
            return {
                ClientId = clientId
                ClientSecret = clientSecret
                RedirectUri = redirectUri
                Scopes = [ "user-read-private"; "user-read-email"; "playlist-read-private" ]
                AuthorizationBaseUrl = authBaseUrl
                TokenBaseUrl = tokenBaseUrl
            }
        }
    
    /// Load tokens from storage
    let loadTokens () : Result<TokenStorage, UserProfileError> =
        configService.ReadTokens()
        |> Result.mapError (fun configError ->
            match configError with
            | ConfigError.FileNotFound _ -> NotAuthenticated
            | ConfigError.InvalidFormat _ -> AuthenticationExpired
            | other -> ConfigurationError other)
    
    /// Refresh tokens if needed
    let refreshTokensIfNeeded (tokens: TokenStorage) : Task<Result<TokenStorage, UserProfileError>> =
        taskResult {
            if isTokenNearExpiry tokens then
                Console.WriteLine("🔄 Access token is near expiry, refreshing...")
                
                let! oauthConfig = getOAuthConfig() |> Task.FromResult
                
                let! newTokens = 
                    oauthService.RefreshAccessToken oauthConfig tokens.RefreshToken
                    |> Result.mapError TokenRefreshFailed
                    |> Task.FromResult
                
                // Save the refreshed tokens
                do! configService.WriteTokens(newTokens)
                    |> Result.mapError ConfigurationError
                    |> Task.FromResult
                
                Console.WriteLine("✅ Access token refreshed successfully")
                return newTokens
            else
                return tokens
        }
    
    /// Ensure we have valid authentication tokens
    let ensureValidAuthentication () : Task<Result<TokenStorage, UserProfileError>> =
        taskResult {
            let! tokens = loadTokens() |> Task.FromResult
            
            // Check if token is completely expired (not just near expiry)
            if DomainValidation.isTokenExpired tokens then
                // Try to refresh
                return! refreshTokensIfNeeded tokens
            else
                // Check if we should proactively refresh
                return! refreshTokensIfNeeded tokens
        }
    
    /// Get user profile from Spotify API
    let getCurrentUserProfile () : Task<Result<UserProfile, UserProfileError>> =
        taskResult {
            let! tokens = ensureValidAuthentication()
            
            let! userProfile = 
                spotifyApiClient.GetUserProfile(tokens.AccessToken)
                |> Result.mapError ProfileRetrievalFailed
                |> Task.FromResult
            
            return userProfile
        }
    
    interface IUserProfileService with
        
        member _.GetCurrentUserProfile() =
            getCurrentUserProfile()
        
        member _.EnsureValidAuthentication() =
            ensureValidAuthentication()

/// Factory functions for creating user profile service
module UserProfileService =
    
    let create (configService: IConfigService) (oauthService: IOAuthService) (spotifyApiClient: ISpotifyApiClient) : IUserProfileService =
        UserProfileService(configService, oauthService, spotifyApiClient) :> IUserProfileService

/// User profile service utilities and helpers
module UserProfileServiceHelpers =
    
    /// Format user profile error for display
    let formatUserProfileError = function
        | NotAuthenticated -> "Not authenticated. Please run 'spotify-cli --auth' first."
        | AuthenticationExpired -> "Authentication expired. Please run 'spotify-cli --auth' to re-authenticate."
        | TokenRefreshFailed oauthError -> $"Failed to refresh token: {OAuthServiceHelpers.formatOAuthError oauthError}"
        | ProfileRetrievalFailed spotifyError -> $"Failed to retrieve profile: {ErrorFormatting.formatSpotifyError spotifyError}"
        | ConfigurationError configError -> $"Configuration error: {ErrorFormatting.formatConfigError configError}"
    
    /// Check if authentication is available
    let isAuthenticationAvailable (userProfileService: IUserProfileService) : Task<bool> =
        task {
            let! result = userProfileService.EnsureValidAuthentication()
            return Result.isOk result
        }
    
    /// Get user profile with user-friendly error handling
    let getUserProfileSafely (userProfileService: IUserProfileService) : Task<Result<UserProfile, string>> =
        task {
            let! result = userProfileService.GetCurrentUserProfile()
            return Result.mapError formatUserProfileError result
        }