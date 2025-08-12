namespace SpotifyCLI.Domain

/// Configuration-related errors following F# coding guide error modeling principles
type ConfigError =
    | FileNotFound of path: string
    | PermissionDenied of path: string  
    | InvalidFormat of reason: string
    | DirectoryCreationFailed of path: string * error: string

/// HTTP communication errors with domain-specific error types
type HttpError =
    | NetworkTimeout of timeoutMs: int
    | Unauthorized
    | NotFound
    | RateLimited of retryAfterSeconds: int option
    | ServerError of statusCode: int * message: string
    | BadRequest of reason: string
    | Forbidden of reason: string
    | JsonParseError of content: string * error: string

/// Spotify-specific API and business logic errors
type SpotifyError =
    | TokenExpired
    | InvalidScope of required: string list
    | ApiError of message: string
    | InvalidRedirectUri of uri: string
    | AuthorizationDenied
    | InvalidClientCredentials
    | ProfileNotFound
    | PlaylistsNotAccessible

/// Cryptographic operation errors
type CryptoError =
    | RandomGenerationFailed of reason: string
    | EncodingError of input: string * reason: string
    | HashingError of reason: string

/// Browser operation errors for OAuth authentication
type BrowserError =
    | BrowserLaunchFailed of reason: string
    | UnsupportedPlatform of platform: string
    | BrowserNotFound of browserName: string
    | ProcessStartFailed of command: string * error: string

/// Callback server errors for OAuth flow
type CallbackServerError =
    | ServerStartFailed of port: int * reason: string
    | CallbackTimeout of timeoutMs: int
    | InvalidCallback of reason: string
    | ServerShutdownFailed of reason: string
    | PortAlreadyInUse of port: int

/// OAuth service errors
type OAuthError =
    | InvalidConfiguration of reason: string
    | AuthorizationUrlGenerationFailed of reason: string
    | TokenExchangeFailed of error: string * description: string option
    | InvalidTokenResponse of reason: string
    | StateValidationFailed of expected: string * received: string option

/// Authentication workflow errors
type AuthWorkflowError =
    | ConfigurationError of reason: string
    | BrowserLaunchError of BrowserError
    | CallbackServerError of CallbackServerError
    | OAuthError of OAuthError
    | CallbackTimeoutError of timeoutMinutes: int
    | StateValidationError of expected: string * received: string option
    | TokenStorageError of ConfigError
    | WorkflowCancelled
    | UnexpectedWorkflowError of message: string

/// File system operation errors
type FileSystemError =
    | ReadError of path: string * reason: string
    | WriteError of path: string * reason: string
    | CreateDirectoryError of path: string * reason: string
    | PathNotFound of path: string

/// Application-level errors that combine multiple error domains
type AppError =
    | ConfigError of ConfigError
    | HttpError of HttpError
    | SpotifyError of SpotifyError  
    | CryptoError of CryptoError
    | BrowserError of BrowserError
    | CallbackServerError of CallbackServerError
    | OAuthError of OAuthError
    | AuthWorkflowError of AuthWorkflowError
    | FileSystemError of FileSystemError
    | ValidationError of field: string * reason: string
    | UnexpectedError of message: string

/// Error formatting utilities following F# coding guide patterns
module ErrorFormatting =
    
    let formatConfigError = function
        | FileNotFound path -> $"Configuration file not found: {path}"
        | PermissionDenied path -> $"Permission denied accessing: {path}"
        | InvalidFormat reason -> $"Invalid configuration format: {reason}"
        | DirectoryCreationFailed(path, error) -> $"Failed to create directory {path}: {error}"
    
    let formatHttpError = function
        | NetworkTimeout timeoutMs -> $"Network request timed out after {timeoutMs}ms"
        | Unauthorized -> "Unauthorized - authentication required"
        | NotFound -> "Resource not found"
        | RateLimited None -> "Rate limited - please try again later"
        | RateLimited(Some seconds) -> $"Rate limited - try again in {seconds} seconds"
        | ServerError(code, message) -> $"Server error {code}: {message}"
        | BadRequest reason -> $"Bad request: {reason}"
        | Forbidden reason -> $"Forbidden: {reason}"
        | JsonParseError(content, error) -> $"Failed to parse JSON response: {error}"
    
    let formatSpotifyError = function
        | TokenExpired -> "Access token has expired - please re-authenticate"
        | InvalidScope required -> 
            let scopeList = String.concat ", " required
            $"Missing required scopes: {scopeList}"
        | ApiError message -> $"Spotify API error: {message}"
        | InvalidRedirectUri uri -> $"Invalid redirect URI: {uri}"
        | AuthorizationDenied -> "User denied authorization"
        | InvalidClientCredentials -> "Invalid client credentials"
        | ProfileNotFound -> "User profile not found"
        | PlaylistsNotAccessible -> "Unable to access user playlists"
    
    let formatCryptoError = function
        | RandomGenerationFailed reason -> $"Failed to generate random data: {reason}"
        | EncodingError(input, reason) -> $"Encoding failed for input '{input}': {reason}"
        | HashingError reason -> $"Hashing operation failed: {reason}"
    
    let formatBrowserError = function
        | BrowserLaunchFailed reason -> $"Failed to launch browser: {reason}"
        | UnsupportedPlatform platform -> $"Unsupported platform: {platform}"
        | BrowserNotFound browserName -> $"Browser not found: {browserName}"
        | ProcessStartFailed(command, error) -> $"Failed to start process '{command}': {error}"
    
    let formatCallbackServerError = function
        | ServerStartFailed(port, reason) -> $"Failed to start callback server on port {port}: {reason}"
        | CallbackTimeout timeoutMs -> $"Callback timeout after {timeoutMs}ms"
        | InvalidCallback reason -> $"Invalid callback received: {reason}"
        | ServerShutdownFailed reason -> $"Failed to shutdown callback server: {reason}"
        | PortAlreadyInUse port -> $"Port {port} is already in use"
    
    let formatOAuthError = function
        | InvalidConfiguration reason -> $"OAuth configuration error: {reason}"
        | AuthorizationUrlGenerationFailed reason -> $"Failed to generate authorization URL: {reason}"
        | TokenExchangeFailed(error, Some description) -> $"Token exchange failed - {error}: {description}"
        | TokenExchangeFailed(error, None) -> $"Token exchange failed: {error}"
        | InvalidTokenResponse reason -> $"Invalid token response: {reason}"
        | StateValidationFailed(expected, Some received) -> $"State validation failed - expected: {expected}, received: {received}"
        | StateValidationFailed(expected, None) -> $"State validation failed - expected: {expected}, but no state received"
    
    let formatAuthWorkflowError = function
        | AuthWorkflowError.ConfigurationError reason -> $"Configuration error: {reason}"
        | AuthWorkflowError.BrowserLaunchError err -> $"Browser launch error: {formatBrowserError err}"
        | AuthWorkflowError.CallbackServerError err -> $"Callback server error: {formatCallbackServerError err}"
        | AuthWorkflowError.OAuthError err -> $"OAuth error: {formatOAuthError err}"
        | AuthWorkflowError.CallbackTimeoutError timeoutMinutes -> $"Authentication timed out after {timeoutMinutes} minutes"
        | AuthWorkflowError.StateValidationError(expected, Some received) -> $"Security validation failed - expected: {expected}, received: {received}"
        | AuthWorkflowError.StateValidationError(expected, None) -> $"Security validation failed - expected: {expected}, no state received"
        | AuthWorkflowError.TokenStorageError err -> $"Token storage error: {formatConfigError err}"
        | AuthWorkflowError.WorkflowCancelled -> "Authentication workflow was cancelled"
        | AuthWorkflowError.UnexpectedWorkflowError message -> $"Unexpected error during authentication: {message}"
    
    let formatFileSystemError = function
        | ReadError(path, reason) -> $"Failed to read file {path}: {reason}"
        | WriteError(path, reason) -> $"Failed to write file {path}: {reason}"
        | CreateDirectoryError(path, reason) -> $"Failed to create directory {path}: {reason}"
        | PathNotFound path -> $"Path not found: {path}"
    
    let formatAppError = function
        | ConfigError err -> formatConfigError err
        | HttpError err -> formatHttpError err
        | SpotifyError err -> formatSpotifyError err
        | CryptoError err -> formatCryptoError err
        | BrowserError err -> formatBrowserError err
        | CallbackServerError err -> formatCallbackServerError err
        | OAuthError err -> formatOAuthError err
        | AuthWorkflowError err -> formatAuthWorkflowError err
        | FileSystemError err -> formatFileSystemError err
        | ValidationError(field, reason) -> $"Validation error in field '{field}': {reason}"
        | UnexpectedError message -> $"Unexpected error: {message}"

/// Error conversion utilities for consistent error mapping
module ErrorConversion =
    
    /// Convert individual error types to AuthWorkflowError
    let configErrorToAuthWorkflowError (err: ConfigError) : AuthWorkflowError =
        TokenStorageError err
    
    let browserErrorToAuthWorkflowError (err: BrowserError) : AuthWorkflowError =
        BrowserLaunchError err
    
    let callbackServerErrorToAuthWorkflowError (err: CallbackServerError) : AuthWorkflowError =
        AuthWorkflowError.CallbackServerError err
    
    let oauthErrorToAuthWorkflowError (err: OAuthError) : AuthWorkflowError =
        AuthWorkflowError.OAuthError err
    
    let cryptoErrorToAuthWorkflowError (err: CryptoError) : AuthWorkflowError =
        ConfigurationError($"Crypto operation failed: {ErrorFormatting.formatCryptoError err}")
    
    /// Convert AuthWorkflowError to AppError
    let authWorkflowErrorToAppError (err: AuthWorkflowError) : AppError =
        AppError.AuthWorkflowError err
    
    /// Convert AppError to AuthWorkflowError if possible
    let appErrorToAuthWorkflowError (err: AppError) : AuthWorkflowError =
        match err with
        | AppError.ConfigError configErr -> configErrorToAuthWorkflowError configErr
        | AppError.BrowserError browserErr -> browserErrorToAuthWorkflowError browserErr
        | AppError.CallbackServerError callbackErr -> callbackServerErrorToAuthWorkflowError callbackErr
        | AppError.OAuthError oauthErr -> oauthErrorToAuthWorkflowError oauthErr
        | AppError.CryptoError cryptoErr -> cryptoErrorToAuthWorkflowError cryptoErr
        | AppError.AuthWorkflowError workflowErr -> workflowErr
        | AppError.HttpError httpErr -> AuthWorkflowError.ConfigurationError($"HTTP error: {ErrorFormatting.formatHttpError httpErr}")
        | AppError.SpotifyError spotifyErr -> AuthWorkflowError.ConfigurationError($"Spotify error: {ErrorFormatting.formatSpotifyError spotifyErr}")
        | AppError.FileSystemError fsErr -> AuthWorkflowError.ConfigurationError($"File system error: {ErrorFormatting.formatFileSystemError fsErr}")
        | AppError.ValidationError(field, reason) -> AuthWorkflowError.ConfigurationError($"Validation error in {field}: {reason}")
        | AppError.UnexpectedError message -> AuthWorkflowError.UnexpectedWorkflowError message