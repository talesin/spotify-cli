namespace SpotifyCLI.Domain

open System
open System.Text.RegularExpressions

/// <summary>
/// Constrained string type with maximum length of 50 characters.
/// Used for general purpose short text fields like display names.
/// </summary>
/// <example>
/// let name = ConstrainedTypes.createString50 "User Name"
/// match name with
/// | Ok (String50 value) -> printfn "Valid name: %s" value
/// | Error message -> printfn "Invalid: %s" message
/// </example>
type String50 = String50 of string

/// <summary>
/// Constrained string type for Spotify playlist names with maximum length of 100 characters.
/// Handles empty names gracefully by defaulting to "Untitled Playlist".
/// </summary>
/// <example>
/// let playlistName = ConstrainedTypes.createPlaylistName "My Favorite Songs"
/// </example>
type PlaylistName = PlaylistName of string

/// <summary>
/// Constrained string type that must contain exactly one character.
/// Used for single character inputs where validation is required.
/// </summary>
/// <example>
/// let char = ConstrainedTypes.createString1 "A"
/// </example>
type String1 = String1 of string

/// <summary>
/// Validated email address type that enforces proper email format.
/// Uses regex validation to ensure the string is a valid email address.
/// </summary>
/// <example>
/// let email = ConstrainedTypes.createEmailAddress "user@example.com"
/// </example>
type EmailAddress = EmailAddress of string

/// <summary>
/// Spotify URI type with validation for proper Spotify URI format.
/// Must start with "spotify:" prefix to be considered valid.
/// </summary>
/// <example>
/// let uri = ConstrainedTypes.createSpotifyUri "spotify:user:username"
/// </example>
type SpotifyUri = SpotifyUri of string

/// <summary>
/// OAuth access token for authenticating API requests to Spotify.
/// This token has a limited lifetime and may need refreshing.
/// </summary>
type AccessToken = AccessToken of string

/// <summary>
/// OAuth refresh token used to obtain new access tokens.
/// Has a longer lifetime than access tokens and is stored persistently.
/// </summary>
type RefreshToken = RefreshToken of string

/// OAuth authorization code
type AuthorizationCode = AuthorizationCode of string

/// PKCE code verifier
type CodeVerifier = CodeVerifier of string

/// PKCE code challenge
type CodeChallenge = CodeChallenge of string

/// HTTP URL type
type HttpUrl = HttpUrl of string

/// Positive integer constraint for track counts
type TrackCount = TrackCount of int

/// Port number constraint for local server
type PortNumber = PortNumber of int

/// <summary>
/// Playlist visibility options in Spotify.
/// Determines whether a playlist is publicly visible or private to the user.
/// </summary>
type PlaylistVisibility = 
    /// Playlist is visible to all users
    | Public 
    /// Playlist is only visible to the owner
    | Private

/// <summary>
/// User profile information retrieved from Spotify API.
/// Contains essential user data for display and identification purposes.
/// </summary>
[<Struct>]
type UserProfile = {
    /// User's display name (may be None if not set)
    DisplayName: String50 option
    /// User's verified email address
    Email: EmailAddress
    /// User's country code (ISO 3166-1 alpha-2)
    Country: string option
    /// User's Spotify URI for identification
    SpotifyUri: SpotifyUri
    /// Unique user ID in Spotify
    Id: string
    /// Number of followers (may be None for private profiles)
    Followers: int option
}

/// <summary>
/// Playlist information retrieved from Spotify API.
/// Contains metadata about a user's playlist including track count and visibility.
/// </summary>
[<Struct>]
type PlaylistInfo = {
    /// Name of the playlist (max 100 characters)
    Name: PlaylistName
    /// Number of tracks in the playlist
    TrackCount: TrackCount
    /// Whether the playlist is public or private
    Visibility: PlaylistVisibility
    /// Unique playlist ID in Spotify
    Id: string
    /// Playlist's Spotify URI
    SpotifyUri: SpotifyUri
    /// Optional description of the playlist
    Description: string option
}

/// OAuth token storage
[<Struct>]  
type TokenStorage = {
    AccessToken: AccessToken
    RefreshToken: RefreshToken
    ExpiresAt: DateTime
    TokenType: string
}

/// OAuth authorization request parameters
[<Struct>]
type AuthorizationRequest = {
    ClientId: string
    RedirectUri: HttpUrl
    CodeChallenge: CodeChallenge
    State: string
    Scopes: string list
}

/// OAuth token exchange request
[<Struct>]
type TokenExchangeRequest = {
    ClientId: string
    AuthorizationCode: AuthorizationCode
    RedirectUri: HttpUrl
    CodeVerifier: CodeVerifier
}

/// OAuth token refresh request
[<Struct>]
type TokenRefreshRequest = {
    ClientId: string
    RefreshToken: RefreshToken
}

/// <summary>
/// Constrained type creation functions following F# coding guide principles.
/// Provides safe construction of domain types with validation and error handling.
/// All functions return Result types to enable functional error handling.
/// </summary>
/// <example>
/// // Creating validated types
/// let email = ConstrainedTypes.createEmailAddress "user@example.com"
/// let name = ConstrainedTypes.createString50 "Display Name"
/// match email, name with
/// | Ok emailAddr, Ok displayName -> (* use validated values *)
/// | Error emailErr, _ -> (* handle email error *)
/// | _, Error nameErr -> (* handle name error *)
/// </example>
module ConstrainedTypes =
    
    /// <summary>
    /// Creates a validated String50 type with maximum length validation.
    /// </summary>
    /// <param name="s">The string to validate (must be non-empty and ≤50 characters)</param>
    /// <returns>Result containing validated String50 or error message</returns>
    let createString50 (s: string) : Result<String50, string> =
        if String.IsNullOrEmpty(s) then
            Error "String cannot be null or empty"
        elif s.Length > 50 then
            Error $"String length {s.Length} exceeds maximum of 50 characters"
        else
            Ok(String50 s)
    
    /// <summary>
    /// Creates a validated PlaylistName type with graceful empty name handling.
    /// Empty or null names are automatically converted to "Untitled Playlist".
    /// </summary>
    /// <param name="s">The playlist name to validate</param>
    /// <returns>Result containing validated PlaylistName or error message</returns>
    let createPlaylistName (s: string) : Result<PlaylistName, string> =
        if String.IsNullOrEmpty(s) then
            Ok(PlaylistName "Untitled Playlist")  // Handle empty names gracefully
        elif s.Length > 100 then
            Error $"Playlist name length {s.Length} exceeds maximum of 100 characters"
        else
            Ok(PlaylistName s)
    
    let createString1 (s: string) : Result<String1, string> =
        if String.IsNullOrEmpty(s) then
            Error "String cannot be null or empty"
        elif s.Length <> 1 then
            Error $"String must be exactly 1 character, got {s.Length}"
        else
            Ok(String1 s)
    
    let createEmailAddress (s: string) : Result<EmailAddress, string> =
        if String.IsNullOrEmpty(s) then
            Error "Email address cannot be null or empty"
        elif not (Regex.IsMatch(s, @"^[\w\.-]+@[\w\.-]+\.\w+$")) then
            Error $"Invalid email address format: {s}"
        else
            Ok(EmailAddress s)
    
    let createSpotifyUri (s: string) : Result<SpotifyUri, string> =
        if String.IsNullOrEmpty(s) then
            Error "Spotify URI cannot be null or empty"
        elif not (s.StartsWith("spotify:")) then
            Error $"Spotify URI must start with 'spotify:', got: {s}"
        else
            Ok(SpotifyUri s)
    
    let createHttpUrl (s: string) : Result<HttpUrl, string> =
        match Uri.TryCreate(s, UriKind.Absolute) with
        | true, uri when uri.Scheme = "http" || uri.Scheme = "https" -> 
            Ok(HttpUrl s)
        | true, _ -> 
            Error $"URL must use HTTP or HTTPS scheme: {s}"
        | false, _ -> 
            Error $"Invalid URL format: {s}"
    
    let createTrackCount (count: int) : Result<TrackCount, string> =
        if count < 0 then
            Error $"Track count cannot be negative: {count}"
        else
            Ok(TrackCount count)
    
    let createPortNumber (port: int) : Result<PortNumber, string> =
        if port < 1 || port > 65535 then
            Error $"Port number must be between 1 and 65535: {port}"
        else
            Ok(PortNumber port)

/// Value extraction functions for constrained types
module TypeExtraction =
    
    let getString50 (String50 s) = s
    let getPlaylistName (PlaylistName s) = s
    let getString1 (String1 s) = s
    let getEmailAddress (EmailAddress s) = s
    let getSpotifyUri (SpotifyUri s) = s
    let getAccessToken (AccessToken s) = s
    let getRefreshToken (RefreshToken s) = s
    let getAuthorizationCode (AuthorizationCode s) = s
    let getCodeVerifier (CodeVerifier s) = s
    let getCodeChallenge (CodeChallenge s) = s
    let getHttpUrl (HttpUrl s) = s
    let getTrackCount (TrackCount i) = i
    let getPortNumber (PortNumber i) = i

/// Domain validation functions
module DomainValidation =
    
    let isTokenExpired (tokenStorage: TokenStorage) : bool =
        DateTime.UtcNow >= tokenStorage.ExpiresAt
    
    let isValidPlaylistName (name: string) : bool =
        not (String.IsNullOrWhiteSpace(name)) && name.Length <= 50
    
    let hasRequiredScope (requiredScope: string) (availableScopes: string list) : bool =
        availableScopes |> List.contains requiredScope
    
    let validateUserProfile (profile: UserProfile) : Result<UserProfile, string list> =
        let errors = ResizeArray<string>()
        
        // Email is required and already validated by type
        match profile.Email with
        | EmailAddress email when String.IsNullOrEmpty(email) -> 
            errors.Add("Email address is required")
        | _ -> ()
        
        // SpotifyUri is required and already validated by type
        match profile.SpotifyUri with
        | SpotifyUri uri when String.IsNullOrEmpty(uri) ->
            errors.Add("Spotify URI is required")
        | _ -> ()
        
        if errors.Count = 0 then
            Ok profile
        else
            Error (errors |> List.ofSeq)