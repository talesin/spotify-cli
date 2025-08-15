namespace SpotifyCLI.Services

open System
open System.Collections.Generic
open System.Text.Json
open FSharp.SystemTextJson
open FsToolkit.ErrorHandling
open SpotifyCLI.Domain

/// Spotify OAuth configuration parameters
[<Struct>]
type SpotifyOAuthConfig = {
    ClientId: string
    ClientSecret: string
    RedirectUri: HttpUrl
    Scopes: string list
    AuthorizationBaseUrl: HttpUrl
    TokenBaseUrl: HttpUrl
}

/// OAuth token response from Spotify
[<Struct>]
type TokenResponse = {
    AccessToken: AccessToken
    RefreshToken: RefreshToken
    ExpiresIn: int
    TokenType: string
    Scope: string
}

/// OAuth error response from Spotify
[<Struct>]
type OAuthErrorResponse = {
    Error: string
    ErrorDescription: string option
}


/// OAuth service interface for dependency injection
type IOAuthService =
    abstract GenerateAuthorizationUrl: SpotifyOAuthConfig -> string -> CodeChallenge -> Result<HttpUrl, OAuthError>
    abstract ExchangeCodeForTokens: SpotifyOAuthConfig -> AuthorizationCode -> CodeVerifier -> Result<TokenStorage, OAuthError>
    abstract RefreshAccessToken: SpotifyOAuthConfig -> RefreshToken -> Result<TokenStorage, OAuthError>
    abstract ValidateConfiguration: SpotifyOAuthConfig -> Result<unit, OAuthError>

/// Spotify OAuth service implementation
type SpotifyOAuthService(httpService: IHttpService, cryptoService: ICryptoService) =
    
    /// Default Spotify OAuth endpoints
    let spotifyAuthorizationUrl = "https://accounts.spotify.com/authorize"
    let spotifyTokenUrl = "https://accounts.spotify.com/api/token"
    
    /// JSON serialization options for OAuth responses
    let jsonOptions = JsonSerializerOptions()
    
    /// Validate OAuth configuration parameters
    let validateConfig (config: SpotifyOAuthConfig) : Result<unit, OAuthError> =
        let errors = ResizeArray<string>()
        
        if String.IsNullOrWhiteSpace(config.ClientId) then
            errors.Add("Client ID is required")
        
        if List.isEmpty config.Scopes then
            errors.Add("At least one scope is required")
        
        let redirectUriString = TypeExtraction.getHttpUrl config.RedirectUri
        if not (redirectUriString.StartsWith("http://") || redirectUriString.StartsWith("https://")) then
            errors.Add("Redirect URI must use HTTP or HTTPS protocol")
        
        if errors.Count > 0 then
            let errorList = errors |> List.ofSeq
            Error(InvalidConfiguration(String.concat "; " errorList))
        else
            Ok()
    
    /// Generate authorization URL with provided PKCE challenge
    let generateAuthUrl (config: SpotifyOAuthConfig) (state: string) (codeChallenge: CodeChallenge) : Result<HttpUrl, OAuthError> =
        result {
            // Build query parameters using provided PKCE challenge
            let queryParams = [
                ("client_id", config.ClientId)
                ("response_type", "code")
                ("redirect_uri", TypeExtraction.getHttpUrl config.RedirectUri)
                ("code_challenge_method", "S256")
                ("code_challenge", TypeExtraction.getCodeChallenge codeChallenge)
                ("state", state)
                ("scope", String.concat " " config.Scopes)
            ]
            
            // Create authorization URL
            let baseUrl = TypeExtraction.getHttpUrl config.AuthorizationBaseUrl
            return! HttpServiceHelpers.createUrlWithQuery baseUrl queryParams
                    |> Result.mapError (fun err -> AuthorizationUrlGenerationFailed err)
        }
    
    /// Parse token response JSON
    let parseTokenResponse (jsonContent: string) : Result<TokenResponse, OAuthError> =
        try
            // First try to parse as success response
            let tokenData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonContent)
            
            if tokenData.ContainsKey("error") then
                // This is an error response
                let error = tokenData.["error"].GetString()
                let errorDescription = 
                    if tokenData.ContainsKey("error_description") then
                        Some(tokenData.["error_description"].GetString())
                    else None
                Error(TokenExchangeFailed(error, errorDescription))
            else
                // This is a success response
                let accessToken = AccessToken(tokenData.["access_token"].GetString())
                let tokenType = tokenData.["token_type"].GetString()
                let expiresIn = tokenData.["expires_in"].GetInt32()
                let scope = tokenData.["scope"].GetString()
                
                let refreshToken = 
                    if tokenData.ContainsKey("refresh_token") then
                        RefreshToken(tokenData.["refresh_token"].GetString())
                    else
                        RefreshToken("") // Some flows don't provide refresh token
                
                Ok({
                    AccessToken = accessToken
                    RefreshToken = refreshToken
                    ExpiresIn = expiresIn
                    TokenType = tokenType
                    Scope = scope
                })
        with
        | :? JsonException as ex -> Error(InvalidTokenResponse ex.Message)
        | ex -> Error(InvalidTokenResponse ex.Message)
    
    /// Convert TokenResponse to TokenStorage
    let toTokenStorage (tokenResponse: TokenResponse) : TokenStorage = {
        AccessToken = tokenResponse.AccessToken
        RefreshToken = tokenResponse.RefreshToken
        ExpiresAt = DateTime.UtcNow.AddSeconds(float tokenResponse.ExpiresIn)
        TokenType = tokenResponse.TokenType
    }
    
    /// Exchange authorization code for access tokens
    let exchangeCodeForTokens (config: SpotifyOAuthConfig) (authCode: AuthorizationCode) (codeVerifier: CodeVerifier) : Result<TokenStorage, OAuthError> =
        // Prepare token exchange request body
        let formData = [
            ("grant_type", "authorization_code")
            ("client_id", config.ClientId)
            ("client_secret", config.ClientSecret)
            ("code", TypeExtraction.getAuthorizationCode authCode)
            ("redirect_uri", TypeExtraction.getHttpUrl config.RedirectUri)
            ("code_verifier", TypeExtraction.getCodeVerifier codeVerifier)
        ]
        
        let requestBody = 
            formData
            |> List.map (fun (key, value) -> $"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}")
            |> String.concat "&"
        
        // Set up headers for form submission
        let formHeaders = Map.ofList [
            ("Content-Type", "application/x-www-form-urlencoded")
        ]
        
        // Make token exchange request and parse response
        result {
            let! response = 
                httpService.Post config.TokenBaseUrl requestBody formHeaders
                |> Result.mapError (fun err -> TokenExchangeFailed("HTTP request failed", Some(ErrorFormatting.formatHttpError err)))
            
            let! jsonContent = 
                HttpServiceHelpers.extractJsonFromResponse response
                |> Result.mapError (fun err -> TokenExchangeFailed("Response extraction failed", Some(ErrorFormatting.formatHttpError err)))
            
            let! tokenResponse = parseTokenResponse jsonContent
            
            return toTokenStorage tokenResponse
        }
    
    /// Refresh access token using refresh token
    let refreshAccessToken (config: SpotifyOAuthConfig) (refreshToken: RefreshToken) : Result<TokenStorage, OAuthError> =
        // Prepare token refresh request body
        let formData = [
            ("grant_type", "refresh_token")
            ("client_id", config.ClientId)
            ("client_secret", config.ClientSecret)
            ("refresh_token", TypeExtraction.getRefreshToken refreshToken)
        ]
        
        let requestBody = 
            formData
            |> List.map (fun (key, value) -> $"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}")
            |> String.concat "&"
        
        // Set up headers for form submission
        let formHeaders = Map.ofList [
            ("Content-Type", "application/x-www-form-urlencoded")
        ]
        
        // Make token refresh request and parse response
        result {
            let! response = 
                httpService.Post config.TokenBaseUrl requestBody formHeaders
                |> Result.mapError (fun err -> TokenExchangeFailed("Token refresh failed", Some(ErrorFormatting.formatHttpError err)))
            
            let! jsonContent = 
                HttpServiceHelpers.extractJsonFromResponse response
                |> Result.mapError (fun err -> TokenExchangeFailed("Token refresh response extraction failed", Some(ErrorFormatting.formatHttpError err)))
            
            let! tokenResponse = parseTokenResponse jsonContent
            
            // Preserve original refresh token if new one not provided
            let updatedTokenResponse = 
                if TypeExtraction.getRefreshToken tokenResponse.RefreshToken = "" then
                    { tokenResponse with RefreshToken = refreshToken }
                else
                    tokenResponse
            
            return toTokenStorage updatedTokenResponse
        }
    
    interface IOAuthService with
        
        member _.GenerateAuthorizationUrl(config: SpotifyOAuthConfig) (state: string) (codeChallenge: CodeChallenge) =
            result {
                do! validateConfig config
                return! generateAuthUrl config state codeChallenge
            }
        
        member _.ExchangeCodeForTokens(config: SpotifyOAuthConfig) (authCode: AuthorizationCode) (codeVerifier: CodeVerifier) =
            result {
                do! validateConfig config
                return! exchangeCodeForTokens config authCode codeVerifier
            }
        
        member _.RefreshAccessToken(config: SpotifyOAuthConfig) (refreshToken: RefreshToken) =
            result {
                do! validateConfig config
                return! refreshAccessToken config refreshToken
            }
        
        member _.ValidateConfiguration(config: SpotifyOAuthConfig) =
            validateConfig config

/// Factory functions for creating OAuth service
module OAuthService =
    
    let create (httpService: IHttpService) (cryptoService: ICryptoService) : IOAuthService =
        SpotifyOAuthService(httpService, cryptoService) :> IOAuthService

/// OAuth service utilities and helpers
module OAuthServiceHelpers =
    
    /// Default Spotify OAuth endpoints (duplicated for module scope)
    let private spotifyAuthorizationUrl = "https://accounts.spotify.com/authorize"
    let private spotifyTokenUrl = "https://accounts.spotify.com/api/token"
    
    /// Create default Spotify OAuth configuration
    let createSpotifyOAuthConfig (clientId: string) (clientSecret: string) (redirectPort: PortNumber) : Result<SpotifyOAuthConfig, OAuthError> =
        let port = TypeExtraction.getPortNumber redirectPort
        
        result {
            let! redirectUri = 
                ConstrainedTypes.createHttpUrl $"http://127.0.0.1:{port}/callback"
                |> Result.mapError (fun err -> InvalidConfiguration($"Invalid redirect URI: {err}"))
            
            let! authBaseUrl = 
                ConstrainedTypes.createHttpUrl spotifyAuthorizationUrl
                |> Result.mapError (fun err -> InvalidConfiguration($"Invalid authorization URL: {err}"))
            
            let! tokenBaseUrl = 
                ConstrainedTypes.createHttpUrl spotifyTokenUrl
                |> Result.mapError (fun err -> InvalidConfiguration($"Invalid token URL: {err}"))
            
            return {
                ClientId = clientId
                ClientSecret = clientSecret
                RedirectUri = redirectUri
                Scopes = [ "user-read-private"; "user-read-email"; "playlist-read-private" ]
                AuthorizationBaseUrl = authBaseUrl
                TokenBaseUrl = tokenBaseUrl
            }
        }
    
    /// Create Spotify OAuth configuration from environment variables
    let createSpotifyOAuthConfigFromEnvironment (configService: IConfigService) (redirectPort: PortNumber) : Result<SpotifyOAuthConfig, OAuthError> =
        result {
            let! clientId = 
                configService.GetSpotifyClientId()
                |> Result.mapError (fun err -> InvalidConfiguration($"Failed to load client ID: {ErrorFormatting.formatConfigError err}"))
            
            let! clientSecret = 
                configService.GetSpotifyClientSecret()
                |> Result.mapError (fun err -> InvalidConfiguration($"Failed to load client secret: {ErrorFormatting.formatConfigError err}"))
            
            let! redirectUri = 
                configService.GetSpotifyRedirectUri()
                |> Result.mapError (fun err -> InvalidConfiguration($"Failed to load redirect URI: {ErrorFormatting.formatConfigError err}"))
                |> Result.bind (fun uri -> 
                    ConstrainedTypes.createHttpUrl uri
                    |> Result.mapError (fun err -> InvalidConfiguration($"Invalid redirect URI: {err}")))
            
            let! authBaseUrl = 
                ConstrainedTypes.createHttpUrl spotifyAuthorizationUrl
                |> Result.mapError (fun err -> InvalidConfiguration($"Invalid authorization URL: {err}"))
            
            let! tokenBaseUrl = 
                ConstrainedTypes.createHttpUrl spotifyTokenUrl
                |> Result.mapError (fun err -> InvalidConfiguration($"Invalid token URL: {err}"))
            
            return {
                ClientId = clientId
                ClientSecret = clientSecret
                RedirectUri = redirectUri
                Scopes = [ "user-read-private"; "user-read-email"; "playlist-read-private" ]
                AuthorizationBaseUrl = authBaseUrl
                TokenBaseUrl = tokenBaseUrl
            }
        }
    
    /// Format OAuth error for user display
    let formatOAuthError = function
        | InvalidConfiguration reason -> $"OAuth configuration error: {reason}"
        | AuthorizationUrlGenerationFailed reason -> $"Failed to generate authorization URL: {reason}"
        | TokenExchangeFailed(error, Some description) -> $"Token exchange failed - {error}: {description}"
        | TokenExchangeFailed(error, None) -> $"Token exchange failed: {error}"
        | InvalidTokenResponse reason -> $"Invalid token response: {reason}"
        | StateValidationFailed(expected, Some received) -> $"State validation failed - expected: {expected}, received: {received}"
        | StateValidationFailed(expected, None) -> $"State validation failed - expected: {expected}, but no state received"
    
    /// Validate state parameter
    let validateState (expectedState: string) (receivedState: string option) : Result<unit, OAuthError> =
        match receivedState with
        | Some state when state = expectedState -> Ok()
        | Some state -> Error(StateValidationFailed(expectedState, Some state))
        | None -> Error(StateValidationFailed(expectedState, None))
    
    /// Check if token is near expiry (within 5 minutes)
    let isTokenNearExpiry (tokenStorage: TokenStorage) : bool =
        let fiveMinutesFromNow = DateTime.UtcNow.AddMinutes(5.0)
        tokenStorage.ExpiresAt <= fiveMinutesFromNow
    
    /// Get recommended OAuth scopes for different use cases
    let getRecommendedScopes (includePlaylistAccess: bool) : string list =
        let baseScopes = [ "user-read-private"; "user-read-email" ]
        if includePlaylistAccess then
            baseScopes @ [ "playlist-read-private"; "playlist-read-collaborative" ]
        else
            baseScopes
    
    /// Extract error information from OAuth error response
    let extractOAuthError (errorResponse: string) : OAuthError =
        try
            let errorData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(errorResponse)
            let error = if errorData.ContainsKey("error") then errorData.["error"].GetString() else "unknown_error"
            let description = 
                if errorData.ContainsKey("error_description") then 
                    Some(errorData.["error_description"].GetString())
                else None
            TokenExchangeFailed(error, description)
        with
        | _ -> TokenExchangeFailed("parse_error", Some("Failed to parse error response"))
    
    /// Create OAuth state with timestamp for additional security
    let createTimestampedState (cryptoService: ICryptoService) : Result<string, CryptoError> =
        cryptoService.GenerateState()
        |> Result.map (fun randomPart ->
            let timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            $"{randomPart}_{timestamp}")
    
    /// Validate timestamped state (ensure it's not too old)
    let validateTimestampedState (state: string) (maxAgeMinutes: int) : bool =
        try
            let parts = state.Split('_')
            if parts.Length <> 2 then false
            else
                let timestamp = Int64.Parse(parts.[1])
                let stateTime = DateTimeOffset.FromUnixTimeSeconds(timestamp)
                let age = DateTimeOffset.UtcNow - stateTime
                age.TotalMinutes <= float maxAgeMinutes
        with
        | _ -> false