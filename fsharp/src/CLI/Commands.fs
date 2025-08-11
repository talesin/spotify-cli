namespace SpotifyCLI.CLI

open Argu
open SpotifyCLI.Domain

/// CLI command definitions using Argu discriminated unions
type SpotifyCliArguments =
    | Auth
    | Me  
    | Playlists
    | [<AltCommandLine("-v")>] Version
    | [<AltCommandLine("-h")>] Help

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Auth -> "Authenticate with Spotify using OAuth2 flow"
            | Me -> "Display your Spotify user profile information"
            | Playlists -> "List your Spotify playlists with details"
            | Version -> "Show version information"
            | Help -> "Show this help message"

/// Command execution results following F# Result pattern
type CommandResult =
    | Success of message: string option
    | Error of AppError
    | Help of helpText: string

/// Application services interface for dependency injection
type IAppServices = {
    Config: SpotifyCLI.Services.IConfigService
    Http: SpotifyCLI.Services.IHttpService  
    Crypto: SpotifyCLI.Services.ICryptoService
}

/// Command handlers interface following functional pattern
type ICommandHandlers = {
    HandleAuth: IAppServices -> Result<unit, AppError>
    HandleMe: IAppServices -> Result<UserProfile, AppError>
    HandlePlaylists: IAppServices -> Result<PlaylistInfo list, AppError>
}

/// CLI command processor using functional composition
module CommandProcessor =
    
    open SpotifyCLI.CLI.ConsoleOutput
    open SpotifyCLI.CLI.DomainOutput
    open SpotifyCLI.CLI.ErrorOutput
    open SpotifyCLI.CLI.StatusOutput
    
    /// Application version information
    let getVersionInfo () =
        let version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version
        let versionString = 
            match version with
            | null -> "Unknown"
            | v -> $"{v.Major}.{v.Minor}.{v.Build}"
        
        [
            $"spotify-cli version {versionString}"
            "F# CLI for interacting with Spotify"
            ""
            "https://github.com/talesin/spotify-cli"
        ]
        |> String.concat "\n"
    
    /// Create argument parser with custom configuration
    let createParser () =
        ArgumentParser.Create<SpotifyCliArguments>(
            programName = "spotify-cli",
            helpDescription = "A functional F# CLI for Spotify",
            errorHandler = ProcessExiter()
        )
    
    /// Process auth command
    let processAuthCommand (handlers: ICommandHandlers) (services: IAppServices) : CommandResult =
        showOAuthInstructions()
        showProgress "Initiating Spotify authentication"
        
        match handlers.HandleAuth services with
        | Ok() -> 
            showProgressComplete()
            printSuccess "Successfully authenticated with Spotify!"
            printInfo "You can now use commands like 'spotify-cli me' and 'spotify-cli playlists'"
            Success None
        | Error error ->
            Console.WriteLine() // New line after progress
            Error error
    
    /// Process me command
    let processMeCommand (handlers: ICommandHandlers) (services: IAppServices) : CommandResult =
        showProgress "Fetching your Spotify profile"
        
        match handlers.HandleMe services with
        | Ok profile ->
            showProgressComplete()
            printUserProfile profile
            Success None
        | Error error ->
            Console.WriteLine() // New line after progress  
            Error error
    
    /// Process playlists command
    let processPlaylistsCommand (handlers: ICommandHandlers) (services: IAppServices) : CommandResult =
        showProgress "Fetching your Spotify playlists"
        
        match handlers.HandlePlaylists services with
        | Ok playlists ->
            showProgressComplete()
            printPlaylistList playlists
            Success None
        | Error error ->
            Console.WriteLine() // New line after progress
            Error error
    
    /// Process single command based on parsed arguments
    let processCommand (handlers: ICommandHandlers) (services: IAppServices) (command: SpotifyCliArguments) : CommandResult =
        match command with
        | Auth -> processAuthCommand handlers services
        | Me -> processMeCommand handlers services
        | Playlists -> processPlaylistsCommand handlers services
        | Version -> 
            Console.WriteLine(getVersionInfo())
            Success None
        | Help -> 
            let parser = createParser()
            Help(parser.PrintUsage())
    
    /// Process command line arguments and execute appropriate command
    let processArguments (handlers: ICommandHandlers) (services: IAppServices) (args: string[]) : CommandResult =
        let parser = createParser()
        
        try
            let parseResults = parser.ParseCommandLine(args)
            let commands = parseResults.GetAllResults()
            
            match commands with
            | [] -> Help(parser.PrintUsage())
            | [command] -> processCommand handlers services command
            | _ -> 
                Error(ValidationError("Command", "Please specify only one command at a time"))
        with
        | :? ArguParseException as ex ->
            Error(ValidationError("Arguments", ex.Message))
        | ex ->
            Error(UnexpectedError($"Failed to parse arguments: {ex.Message}"))

/// Command execution utilities
module CommandExecution =
    
    open SpotifyCLI.CLI.ErrorOutput
    open SpotifyCLI.CLI.ConsoleOutput
    
    /// Execute command and handle result with proper exit codes
    let executeCommand (handlers: ICommandHandlers) (services: IAppServices) (args: string[]) : int =
        match CommandProcessor.processArguments handlers services args with
        | Success _ -> 0
        | Error error ->
            printAppError error
            1
        | Help helpText ->
            Console.WriteLine(helpText)
            0
    
    /// Execute command with exception handling
    let executeCommandSafely (handlers: ICommandHandlers) (services: IAppServices) (args: string[]) : int =
        try
            executeCommand handlers services args
        with
        | ex ->
            printSimpleError $"Unexpected error: {ex.Message}"
            1

/// Default command handlers placeholder - to be implemented in infrastructure layer
module DefaultCommandHandlers =
    
    /// Placeholder auth handler - will be replaced with actual OAuth implementation
    let handleAuth (services: IAppServices) : Result<unit, AppError> =
        // TODO: Implement OAuth flow using SpotifyApi service
        Error(SpotifyError(ApiError "Auth command not yet implemented"))
    
    /// Placeholder me handler - will be replaced with actual API call
    let handleMe (services: IAppServices) : Result<UserProfile, AppError> =
        // TODO: Implement user profile fetching
        Error(SpotifyError(ApiError "Me command not yet implemented"))
    
    /// Placeholder playlists handler - will be replaced with actual API call
    let handlePlaylists (services: IAppServices) : Result<PlaylistInfo list, AppError> =
        // TODO: Implement playlists fetching
        Error(SpotifyError(ApiError "Playlists command not yet implemented"))
    
    /// Create default command handlers
    let create () : ICommandHandlers = {
        HandleAuth = handleAuth
        HandleMe = handleMe
        HandlePlaylists = handlePlaylists
    }