namespace SpotifyCLI.Services

open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open FSharp.SystemTextJson
open FsToolkit.ErrorHandling
open SpotifyCLI.Domain

/// File system abstraction for dependency injection following F# coding guide
type IFileSystem =
    abstract ReadAllText: string -> Result<string, FileSystemError>
    abstract WriteAllText: string * string -> Result<unit, FileSystemError>
    abstract DirectoryExists: string -> bool
    abstract CreateDirectory: string -> Result<unit, FileSystemError>
    abstract FileExists: string -> bool

/// System file system implementation  
type SystemFileSystem() =
    interface IFileSystem with
        member _.ReadAllText(path: string) =
            try
                let content = File.ReadAllText(path)
                Ok content
            with
            | :? FileNotFoundException -> Error(PathNotFound path)
            | :? DirectoryNotFoundException -> Error(PathNotFound(Path.GetDirectoryName(path)))
            | :? UnauthorizedAccessException -> Error(ReadError(path, "Access denied"))
            | ex -> Error(ReadError(path, ex.Message))
        
        member _.WriteAllText(path: string, content: string) =
            try
                File.WriteAllText(path, content)
                Ok()
            with
            | :? DirectoryNotFoundException -> Error(WriteError(path, "Directory not found"))
            | :? UnauthorizedAccessException -> Error(WriteError(path, "Access denied"))
            | ex -> Error(WriteError(path, ex.Message))
        
        member _.DirectoryExists(path: string) = Directory.Exists(path)
        
        member _.CreateDirectory(path: string) =
            try
                Directory.CreateDirectory(path) |> ignore
                Ok()
            with
            | :? UnauthorizedAccessException -> Error(CreateDirectoryError(path, "Access denied"))
            | ex -> Error(CreateDirectoryError(path, ex.Message))
        
        member _.FileExists(path: string) = File.Exists(path)

/// Configuration service interface following dependency injection pattern
type IConfigService =
    abstract ReadTokens: unit -> Result<TokenStorage, ConfigError>
    abstract WriteTokens: TokenStorage -> Result<unit, ConfigError>
    abstract EnsureConfigDirectory: unit -> Result<unit, ConfigError>
    abstract GetConfigPath: unit -> string

/// JSON serialization types for token storage
[<Struct>]
type TokenStorageJson = {
    AccessToken: string
    RefreshToken: string  
    ExpiresAt: DateTime
    TokenType: string
}

/// Configuration service implementation using functional dependency injection
type ConfigService(fileSystem: IFileSystem) =
    
    let configDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".spotify-cli")
    let configFilePath = Path.Combine(configDirectory, "spotify.json")
    
    let jsonOptions = 
        JsonFSharpOptions.Default()
            .WithUnionInternalTag()
            .ToJsonSerializerOptions()
    
    /// Convert domain TokenStorage to JSON representation
    let toJsonTokenStorage (tokens: TokenStorage) : TokenStorageJson = {
        AccessToken = TypeExtraction.getAccessToken tokens.AccessToken
        RefreshToken = TypeExtraction.getRefreshToken tokens.RefreshToken
        ExpiresAt = tokens.ExpiresAt
        TokenType = tokens.TokenType
    }
    
    /// Convert JSON representation to domain TokenStorage
    let fromJsonTokenStorage (json: TokenStorageJson) : TokenStorage = {
        AccessToken = AccessToken json.AccessToken
        RefreshToken = RefreshToken json.RefreshToken
        ExpiresAt = json.ExpiresAt
        TokenType = json.TokenType
    }
    
    /// Map file system errors to configuration errors
    let mapFileSystemError = function
        | PathNotFound path -> ConfigError.FileNotFound path
        | ReadError(path, reason) -> ConfigError.PermissionDenied path
        | WriteError(path, reason) -> ConfigError.PermissionDenied path
        | CreateDirectoryError(path, reason) -> ConfigError.DirectoryCreationFailed(path, reason)
    
    interface IConfigService with
        
        member _.ReadTokens() =
            match fileSystem.FileExists(configFilePath) with
            | false -> Error(ConfigError.FileNotFound configFilePath)
            | true ->
                result {
                    let! jsonContent = 
                        fileSystem.ReadAllText(configFilePath)
                        |> Result.mapError mapFileSystemError
                    
                    try
                        let tokenJson = JsonSerializer.Deserialize<TokenStorageJson>(jsonContent, jsonOptions)
                        return fromJsonTokenStorage tokenJson
                    with
                    | :? JsonException as ex -> return! Error(ConfigError.InvalidFormat ex.Message)
                    | ex -> return! Error(ConfigError.InvalidFormat ex.Message)
                }
        
        member _.WriteTokens(tokens: TokenStorage) =
            let jsonTokens = toJsonTokenStorage tokens
            try
                let jsonContent = JsonSerializer.Serialize(jsonTokens, jsonOptions)
                fileSystem.WriteAllText(configFilePath, jsonContent)
                |> Result.mapError mapFileSystemError
            with
            | :? JsonException as ex -> Error(ConfigError.InvalidFormat ex.Message)
            | ex -> Error(ConfigError.InvalidFormat ex.Message)
        
        member _.EnsureConfigDirectory() =
            match fileSystem.DirectoryExists(configDirectory) with
            | true -> Ok()
            | false -> 
                fileSystem.CreateDirectory(configDirectory)
                |> Result.mapError mapFileSystemError
        
        member _.GetConfigPath() = configFilePath

/// Factory function for creating ConfigService following F# coding guide
module ConfigService =
    
    let create (fileSystem: IFileSystem) : IConfigService =
        ConfigService(fileSystem) :> IConfigService
    
    let createWithSystemFileSystem () : IConfigService =
        let fileSystem = SystemFileSystem() :> IFileSystem
        create fileSystem

/// Configuration service utilities and helpers
module ConfigServiceHelpers =
    
    let isTokenValid (configService: IConfigService) : Result<bool, ConfigError> =
        configService.ReadTokens()
        |> Result.map (fun tokens -> 
            not (DomainValidation.isTokenExpired tokens))
    
    let getValidTokenOrError (configService: IConfigService) : Result<TokenStorage, ConfigError> =
        result {
            let! tokens = configService.ReadTokens()
            if DomainValidation.isTokenExpired tokens then
                return! Error(ConfigError.InvalidFormat "Token has expired")
            else
                return tokens
        }
    
    let updateTokenExpiry (tokens: TokenStorage) (expiresInSeconds: int) : TokenStorage =
        { tokens with ExpiresAt = DateTime.UtcNow.AddSeconds(float expiresInSeconds) }