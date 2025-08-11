namespace SpotifyCLI.Infrastructure

open System
open System.Text.Json
open FSharp.SystemTextJson
open SpotifyCLI.Domain
open SpotifyCLI.Services

/// Spotify API endpoints
module SpotifyEndpoints =
    
    let BaseApiUrl = "https://api.spotify.com/v1"
    let AuthorizeUrl = "https://accounts.spotify.com/authorize"
    let TokenUrl = "https://accounts.spotify.com/api/token"
    
    let getMeUrl () = $"{BaseApiUrl}/me"
    let getPlaylistsUrl () = $"{BaseApiUrl}/me/playlists"
    
    /// Create authorization URL with parameters
    let createAuthorizationUrl (request: AuthorizationRequest) : Result<HttpUrl, string> =
        let queryParams = [
            ("client_id", request.ClientId)
            ("response_type", "code")
            ("redirect_uri", TypeExtraction.getHttpUrl request.RedirectUri)
            ("code_challenge_method", "S256")
            ("code_challenge", TypeExtraction.getCodeChallenge request.CodeChallenge)
            ("state", request.State)
            ("scope", String.concat " " request.Scopes)
        ]
        
        HttpServiceHelpers.createUrlWithQuery AuthorizeUrl queryParams

/// Spotify API response types for JSON deserialization
module SpotifyApiTypes =
    
    /// JSON response type for user profile
    [<Struct>]
    type SpotifyUserJson = {
        id: string
        display_name: string option
        email: string
        country: string option
        uri: string
        followers: {| total: int |} option
    }
    
    /// JSON response type for playlist
    [<Struct>]
    type SpotifyPlaylistJson = {
        id: string
        name: string
        tracks: {| total: int |}
        ``public``: bool option
        uri: string
        description: string option
    }
    
    /// JSON response type for playlists collection
    [<Struct>]
    type SpotifyPlaylistsResponse = {
        items: SpotifyPlaylistJson[]
        total: int
        limit: int
        offset: int
        next: string option
    }

/// Spotify API client interface
type ISpotifyApiClient =
    abstract GetUserProfile: AccessToken -> Result<UserProfile, SpotifyError>
    abstract GetUserPlaylists: AccessToken -> Result<PlaylistInfo list, SpotifyError>
    abstract ExchangeCodeForTokens: TokenExchangeRequest -> Result<TokenStorage, SpotifyError>
    abstract RefreshTokens: TokenRefreshRequest -> Result<TokenStorage, SpotifyError>

/// Spotify API client implementation
type SpotifyApiClient(httpService: IHttpService) =
    
    let jsonOptions = 
        let options = JsonSerializerOptions()
        options.Converters.Add(JsonFSharpConverter(JsonUnionEncoding.InternalTag ||| JsonUnionEncoding.NamedFields))
        options.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
        options
    
    /// Map HTTP errors to Spotify errors
    let mapHttpErrorToSpotifyError = function
        | Unauthorized -> TokenExpired
        | NotFound -> ProfileNotFound
        | Forbidden reason -> InvalidScope ["user-read-email"]
        | ServerError(code, message) -> ApiError $"Server error {code}: {message}"
        | JsonParseError(content, error) -> ApiError $"Failed to parse response: {error}"
        | NetworkTimeout _ -> ApiError "Request timed out"
        | BadRequest reason -> ApiError $"Bad request: {reason}"
        | RateLimited _ -> ApiError "Rate limited - please try again later"
    
    /// Convert Spotify user JSON to domain UserProfile
    let convertUserProfile (json: SpotifyApiTypes.SpotifyUserJson) : Result<UserProfile, SpotifyError> =
        // Validate and create constrained types
        let emailResult = ConstrainedTypes.createEmailAddress json.email
        let spotifyUriResult = ConstrainedTypes.createSpotifyUri json.uri
        let displayNameResult = 
            json.display_name
            |> Option.map ConstrainedTypes.createString50
            |> Option.map (Result.map Some)
            |> Option.defaultValue (Ok None)
        
        match emailResult, spotifyUriResult, displayNameResult with
        | Ok email, Ok uri, Ok displayName ->
            let profile = {
                DisplayName = displayName
                Email = email
                Country = json.country
                SpotifyUri = uri
                Id = json.id
                Followers = json.followers |> Option.map (fun f -> f.total)
            }
            Ok profile
        | Error emailError, _, _ -> Error(ApiError $"Invalid email: {emailError}")
        | _, Error uriError, _ -> Error(ApiError $"Invalid Spotify URI: {uriError}")
        | _, _, Error nameError -> Error(ApiError $"Invalid display name: {nameError}")
    
    /// Convert Spotify playlist JSON to domain PlaylistInfo
    let convertPlaylist (json: SpotifyApiTypes.SpotifyPlaylistJson) : Result<PlaylistInfo, SpotifyError> =
        let nameResult = ConstrainedTypes.createString50 json.name
        let trackCountResult = ConstrainedTypes.createTrackCount json.tracks.total
        let spotifyUriResult = ConstrainedTypes.createSpotifyUri json.uri
        
        let visibility = 
            match json.``public`` with
            | Some true -> Public
            | Some false | None -> Private
        
        match nameResult, trackCountResult, spotifyUriResult with
        | Ok name, Ok trackCount, Ok uri ->
            let playlist = {
                Name = name
                TrackCount = trackCount
                Visibility = visibility
                Id = json.id
                SpotifyUri = uri
                Description = json.description
            }
            Ok playlist
        | Error nameError, _, _ -> Error(ApiError $"Invalid playlist name: {nameError}")
        | _, Error trackError, _ -> Error(ApiError $"Invalid track count: {trackError}")
        | _, _, Error uriError -> Error(ApiError $"Invalid playlist URI: {uriError}")
    
    interface ISpotifyApiClient with
        
        member _.GetUserProfile(accessToken: AccessToken) =
            match ConstrainedTypes.createHttpUrl (SpotifyEndpoints.getMeUrl()) with
            | Error error -> Error(ApiError error)
            | Ok url ->
                let headers = HttpServiceHelpers.createAuthorizationHeader accessToken
                
                httpService.Get url headers
                |> Result.mapError mapHttpErrorToSpotifyError
                |> Result.bind (HttpServiceHelpers.extractJsonFromResponse >> Result.mapError mapHttpErrorToSpotifyError)
                |> Result.bind (fun jsonContent ->
                    try
                        let userJson = JsonSerializer.Deserialize<SpotifyApiTypes.SpotifyUserJson>(jsonContent, jsonOptions)
                        convertUserProfile userJson
                    with
                    | :? JsonException as ex -> Error(ApiError $"Failed to parse user profile: {ex.Message}")
                    | ex -> Error(ApiError $"Unexpected error parsing user profile: {ex.Message}"))
        
        member _.GetUserPlaylists(accessToken: AccessToken) =
            match ConstrainedTypes.createHttpUrl (SpotifyEndpoints.getPlaylistsUrl()) with
            | Error error -> Error(ApiError error)
            | Ok url ->
                let headers = HttpServiceHelpers.createAuthorizationHeader accessToken
                
                httpService.Get url headers
                |> Result.mapError mapHttpErrorToSpotifyError
                |> Result.bind (HttpServiceHelpers.extractJsonFromResponse >> Result.mapError mapHttpErrorToSpotifyError)
                |> Result.bind (fun jsonContent ->
                    try
                        let playlistsResponse = JsonSerializer.Deserialize<SpotifyApiTypes.SpotifyPlaylistsResponse>(jsonContent, jsonOptions)
                        
                        playlistsResponse.items
                        |> Array.map convertPlaylist
                        |> Array.toList
                        |> List.fold (fun acc result ->
                            match acc, result with
                            | Ok playlists, Ok playlist -> Ok(playlist :: playlists)
                            | Error error, _ -> Error error
                            | _, Error error -> Error error) (Ok [])
                        |> Result.map List.rev
                    with
                    | :? JsonException as ex -> Error(ApiError $"Failed to parse playlists: {ex.Message}")
                    | ex -> Error(ApiError $"Unexpected error parsing playlists: {ex.Message}"))
        
        member _.ExchangeCodeForTokens(_request: TokenExchangeRequest) =
            // TODO: Implement OAuth token exchange
            Error(ApiError "Token exchange not yet implemented")
        
        member _.RefreshTokens(_request: TokenRefreshRequest) =
            // TODO: Implement token refresh
            Error(ApiError "Token refresh not yet implemented")

/// Factory functions for creating Spotify API client
module SpotifyApiClient =
    
    let create (httpService: IHttpService) : ISpotifyApiClient =
        SpotifyApiClient(httpService) :> ISpotifyApiClient

/// Spotify API utilities and helpers
module SpotifyApiHelpers =
    
    /// Required Spotify API scopes for the application
    let getRequiredScopes () : string list = [
        "user-read-email"
        "user-read-private"
        "playlist-read-private"
        "playlist-read-collaborative"
    ]
    
    /// Check if token has required scopes (placeholder - actual scope validation needs token info)
    let hasRequiredScopes (_token: AccessToken) : bool =
        // TODO: Implement scope validation when we have token introspection
        true
    
    /// Create default redirect URI for OAuth flow
    let getDefaultRedirectUri () : Result<HttpUrl, string> =
        ConstrainedTypes.createHttpUrl "http://127.0.0.1:3000/callback"