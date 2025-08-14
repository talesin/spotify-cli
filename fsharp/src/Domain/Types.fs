namespace SpotifyCLI.Domain

open System
open System.Text.RegularExpressions

/// Constrained string type following F# coding guide principles
type String50 = String50 of string

/// Constrained string type for playlist names (longer than general strings)
type PlaylistName = PlaylistName of string

/// Constrained string type for single characters
type String1 = String1 of string

/// Validated email address type
type EmailAddress = EmailAddress of string

/// Spotify URI type with validation
type SpotifyUri = SpotifyUri of string

/// OAuth access token
type AccessToken = AccessToken of string

/// OAuth refresh token  
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

/// Playlist visibility options
type PlaylistVisibility = Public | Private

/// User profile information from Spotify API
[<Struct>]
type UserProfile = {
    DisplayName: String50 option
    Email: EmailAddress
    Country: string option
    SpotifyUri: SpotifyUri
    Id: string
    Followers: int option
}

/// Playlist information from Spotify API
[<Struct>]
type PlaylistInfo = {
    Name: PlaylistName
    TrackCount: TrackCount
    Visibility: PlaylistVisibility
    Id: string
    SpotifyUri: SpotifyUri
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

/// Constrained type creation functions following F# coding guide
module ConstrainedTypes =
    
    let createString50 (s: string) : Result<String50, string> =
        if String.IsNullOrEmpty(s) then
            Error "String cannot be null or empty"
        elif s.Length > 50 then
            Error $"String length {s.Length} exceeds maximum of 50 characters"
        else
            Ok(String50 s)
    
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