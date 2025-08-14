namespace SpotifyCLI.Services

open System
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open SpotifyCLI.Domain
open SpotifyCLI.Infrastructure

/// Playlist service interface
type IPlaylistService =
    abstract GetUserPlaylists: unit -> Task<Result<PlaylistInfo list, PlaylistError>>
    abstract GetUserPlaylistsPaginated: limit: int option * offset: int option -> Task<Result<PlaylistInfo list, PlaylistError>>
    abstract EnsureValidAuthentication: unit -> Task<Result<TokenStorage, PlaylistError>>

/// Playlist service implementation with automatic token refresh
type PlaylistService(configService: IConfigService, oauthService: IOAuthService, spotifyApiClient: ISpotifyApiClient) =
    
    /// Check if token needs refresh (expired or within 5 minutes of expiry)
    let isTokenNearExpiry (tokens: TokenStorage) : bool =
        let fiveMinutesFromNow = DateTime.UtcNow.AddMinutes(5.0)
        tokens.ExpiresAt <= fiveMinutesFromNow
    
    /// Get OAuth configuration for token refresh
    let getOAuthConfig () : Result<SpotifyOAuthConfig, PlaylistError> =
        result {
            let! clientId = 
                configService.GetSpotifyClientId()
                |> Result.mapError (PlaylistConfigurationError)
            
            let! clientSecret = 
                configService.GetSpotifyClientSecret()
                |> Result.mapError (PlaylistConfigurationError)
            
            let! redirectUri = 
                configService.GetSpotifyRedirectUri()
                |> Result.mapError (PlaylistConfigurationError)
                |> Result.bind (fun uri -> 
                    ConstrainedTypes.createHttpUrl uri
                    |> Result.mapError (fun err -> PlaylistConfigurationError(ConfigError.InvalidFormat($"Invalid redirect URI: {err}"))))
            
            let! authBaseUrl = 
                ConstrainedTypes.createHttpUrl "https://accounts.spotify.com/authorize"
                |> Result.mapError (fun err -> PlaylistConfigurationError(ConfigError.InvalidFormat($"Invalid authorization URL: {err}")))
            
            let! tokenBaseUrl = 
                ConstrainedTypes.createHttpUrl "https://accounts.spotify.com/api/token"
                |> Result.mapError (fun err -> PlaylistConfigurationError(ConfigError.InvalidFormat($"Invalid token URL: {err}")))
            
            return {
                ClientId = clientId
                ClientSecret = clientSecret
                RedirectUri = redirectUri
                Scopes = [ "user-read-private"; "user-read-email"; "playlist-read-private"; "playlist-read-collaborative" ]
                AuthorizationBaseUrl = authBaseUrl
                TokenBaseUrl = tokenBaseUrl
            }
        }
    
    /// Load tokens from storage
    let loadTokens () : Result<TokenStorage, PlaylistError> =
        configService.ReadTokens()
        |> Result.mapError (fun configError ->
            match configError with
            | ConfigError.FileNotFound _ -> PlaylistNotAuthenticated
            | ConfigError.InvalidFormat _ -> PlaylistAuthenticationExpired
            | other -> PlaylistConfigurationError other)
    
    /// Refresh tokens if needed
    let refreshTokensIfNeeded (tokens: TokenStorage) : Task<Result<TokenStorage, PlaylistError>> =
        taskResult {
            if isTokenNearExpiry tokens then
                Console.WriteLine("🔄 Access token is near expiry, refreshing...")
                
                let! oauthConfig = getOAuthConfig() |> Task.FromResult
                
                let! newTokens = 
                    oauthService.RefreshAccessToken oauthConfig tokens.RefreshToken
                    |> Result.mapError PlaylistTokenRefreshFailed
                    |> Task.FromResult
                
                // Save the refreshed tokens
                do! configService.WriteTokens(newTokens)
                    |> Result.mapError PlaylistConfigurationError
                    |> Task.FromResult
                
                Console.WriteLine("✅ Access token refreshed successfully")
                return newTokens
            else
                return tokens
        }
    
    /// Ensure we have valid authentication tokens
    let ensureValidAuthentication () : Task<Result<TokenStorage, PlaylistError>> =
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
    
    /// Get user playlists with automatic pagination (all playlists)
    let getUserPlaylists () : Task<Result<PlaylistInfo list, PlaylistError>> =
        taskResult {
            let! tokens = ensureValidAuthentication()
            
            let! playlists = 
                spotifyApiClient.GetUserPlaylists(tokens.AccessToken)
                |> Result.mapError PlaylistRetrievalFailed
                |> Task.FromResult
            
            return playlists
        }
    
    /// Get user playlists with explicit pagination parameters
    let getUserPlaylistsPaginated (limit: int option, offset: int option) : Task<Result<PlaylistInfo list, PlaylistError>> =
        // For now, delegate to the full fetch implementation
        // TODO: Implement paginated version if needed for performance
        getUserPlaylists()
    
    interface IPlaylistService with
        
        member _.GetUserPlaylists() =
            getUserPlaylists()
        
        member _.GetUserPlaylistsPaginated(limit, offset) =
            getUserPlaylistsPaginated(limit, offset)
        
        member _.EnsureValidAuthentication() =
            ensureValidAuthentication()

/// Factory functions for creating playlist service
module PlaylistService =
    
    let create (configService: IConfigService) (oauthService: IOAuthService) (spotifyApiClient: ISpotifyApiClient) : IPlaylistService =
        PlaylistService(configService, oauthService, spotifyApiClient) :> IPlaylistService

/// Playlist service utilities and helpers
module PlaylistServiceHelpers =
    
    /// Format playlist error for display
    let formatPlaylistError = function
        | PlaylistNotAuthenticated -> "Not authenticated. Please run 'spotify-cli --auth' first."
        | PlaylistAuthenticationExpired -> "Authentication expired. Please run 'spotify-cli --auth' to re-authenticate."
        | PlaylistTokenRefreshFailed oauthError -> $"Failed to refresh token: {OAuthServiceHelpers.formatOAuthError oauthError}"
        | PlaylistRetrievalFailed spotifyError -> $"Failed to retrieve playlists: {ErrorFormatting.formatSpotifyError spotifyError}"
        | PlaylistConfigurationError configError -> $"Configuration error: {ErrorFormatting.formatConfigError configError}"
        | PaginationError reason -> $"Pagination error: {reason}"
    
    /// Check if playlist authentication is available
    let isAuthenticationAvailable (playlistService: IPlaylistService) : Task<bool> =
        task {
            let! result = playlistService.EnsureValidAuthentication()
            return Result.isOk result
        }
    
    /// Get user playlists with user-friendly error handling
    let getUserPlaylistsSafely (playlistService: IPlaylistService) : Task<Result<PlaylistInfo list, string>> =
        task {
            let! result = playlistService.GetUserPlaylists()
            return Result.mapError formatPlaylistError result
        }
    
    /// Display playlist statistics summary
    let getPlaylistStats (playlists: PlaylistInfo list) : string =
        let totalPlaylists = playlists.Length
        let totalTracks = playlists |> List.sumBy (fun p -> TypeExtraction.getTrackCount p.TrackCount)
        let publicCount = playlists |> List.filter (fun p -> p.Visibility = Public) |> List.length
        let privateCount = totalPlaylists - publicCount
        
        [
            $"Total playlists: {totalPlaylists}"
            $"Total tracks: {totalTracks}"
            $"Public: {publicCount}, Private: {privateCount}"
        ]
        |> String.concat " | "