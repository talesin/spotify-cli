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
        | FileSystemError err -> formatFileSystemError err
        | ValidationError(field, reason) -> $"Validation error in field '{field}': {reason}"
        | UnexpectedError message -> $"Unexpected error: {message}"