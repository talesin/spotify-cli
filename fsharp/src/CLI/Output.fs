namespace SpotifyCLI.CLI

open System
open SpotifyCLI.Domain

/// Console output formatting utilities following F# functional principles
module ConsoleOutput =
    
    /// ANSI color codes for terminal formatting
    module Colors =
        let Reset = "\u001b[0m"
        let Red = "\u001b[31m"
        let Green = "\u001b[32m"
        let Yellow = "\u001b[33m"
        let Blue = "\u001b[34m"
        let Magenta = "\u001b[35m"
        let Cyan = "\u001b[36m"
        let White = "\u001b[37m"
        let Gray = "\u001b[90m"
    
    /// Format text with color (if terminal supports it)
    let colorize (color: string) (text: string) : string =
        if Console.IsOutputRedirected then
            text
        else
            $"{color}{text}{Colors.Reset}"
    
    /// Format success message
    let formatSuccess (message: string) : string =
        colorize Colors.Green $"✓ {message}"
    
    /// Format error message  
    let formatError (message: string) : string =
        colorize Colors.Red $"✗ {message}"
    
    /// Format warning message
    let formatWarning (message: string) : string =
        colorize Colors.Yellow $"⚠ {message}"
    
    /// Format info message
    let formatInfo (message: string) : string =
        colorize Colors.Blue $"ℹ {message}"
    
    /// Format header/title
    let formatHeader (title: string) : string =
        let separator = String.replicate title.Length "="
        $"\n{colorize Colors.Cyan title}\n{colorize Colors.Gray separator}\n"
    
    /// Print success message to console
    let printSuccess (message: string) : unit =
        Console.WriteLine(formatSuccess message)
    
    /// Print error message to console
    let printError (message: string) : unit =
        Console.Error.WriteLine(formatError message)
    
    /// Print warning message to console
    let printWarning (message: string) : unit =
        Console.WriteLine(formatWarning message)
    
    /// Print info message to console
    let printInfo (message: string) : unit =
        Console.WriteLine(formatInfo message)
    
    /// Print header to console
    let printHeader (title: string) : unit =
        Console.WriteLine(formatHeader title)

/// Domain-specific output formatting functions
module DomainOutput =
    
    open ConsoleOutput
    
    /// Format user profile for display in a bordered box
    let formatUserProfile (profile: UserProfile) : string =
        let displayName = 
            profile.DisplayName
            |> Option.map TypeExtraction.getString50
            |> Option.defaultValue "N/A"
        
        let email = TypeExtraction.getEmailAddress profile.Email
        let country = profile.Country |> Option.defaultValue "N/A"
        let spotifyUri = TypeExtraction.getSpotifyUri profile.SpotifyUri
        let followers = 
            profile.Followers
            |> Option.map string
            |> Option.defaultValue "N/A"
        
        // Define box dimensions
        let boxWidth = 47 // Total width including borders
        let labelWidth = 14 // Width for labels like "Display Name:"
        let valueWidth = boxWidth - labelWidth - 4 // 4 for "│ " and " │"
        
        // Helper function to create a table row
        let createRow (label: string) (value: string) =
            let truncatedValue = 
                if value.Length > valueWidth then value.Substring(0, valueWidth)
                else value
            let paddedValue = truncatedValue.PadRight(valueWidth)
            $"│ {label.PadRight(labelWidth - 1)}: {paddedValue} │"
        
        [
            "┌─────────────────────────────────────────────┐"
            createRow "Display Name" displayName
            createRow "Email" email
            createRow "Country" country
            createRow "User ID" profile.Id
            createRow "Spotify URI" spotifyUri
            createRow "Followers" followers
            "└─────────────────────────────────────────────┘"
        ]
        |> String.concat "\n"
    
    /// Format playlist info for display
    let formatPlaylistInfo (playlist: PlaylistInfo) : string =
        let name = TypeExtraction.getString50 playlist.Name
        let trackCount = TypeExtraction.getTrackCount playlist.TrackCount
        let visibility = 
            match playlist.Visibility with
            | Public -> colorize Colors.Green "Public"
            | Private -> colorize Colors.Yellow "Private"
        let uri = TypeExtraction.getSpotifyUri playlist.SpotifyUri
        let description = 
            playlist.Description
            |> Option.defaultValue "No description"
        
        [
            $"  {colorize Colors.Cyan name}"
            $"    Tracks:      {colorize Colors.White (string trackCount)}"
            $"    Visibility:  {visibility}"
            $"    Description: {colorize Colors.Gray description}"
            $"    URI:         {colorize Colors.Blue uri}"
        ]
        |> String.concat "\n"
    
    /// Format list of playlists for display
    let formatPlaylistList (playlists: PlaylistInfo list) : string =
        match playlists with
        | [] -> colorize Colors.Gray "No playlists found"
        | playlists ->
            let header = formatHeader $"Your Playlists ({playlists.Length})"
            let playlistLines = 
                playlists
                |> List.mapi (fun i playlist -> 
                    let number = colorize Colors.Gray $"{i + 1}."
                    $"{number} {formatPlaylistInfo playlist}")
                |> String.concat "\n\n"
            
            $"{header}{playlistLines}"
    
    /// Print user profile to console
    let printUserProfile (profile: UserProfile) : unit =
        printHeader "Your Spotify Profile"
        Console.WriteLine(formatUserProfile profile)
    
    /// Print playlist list to console
    let printPlaylistList (playlists: PlaylistInfo list) : unit =
        Console.WriteLine(formatPlaylistList playlists)

/// Error output formatting
module ErrorOutput =
    
    open ConsoleOutput
    
    /// Format application error for user display
    let formatAppError (error: AppError) : string =
        let message = ErrorFormatting.formatAppError error
        
        // Add context-specific help for common errors
        let helpText = 
            match error with
            | SpotifyError TokenExpired ->
                "\nTry running: spotify-cli auth"
            | SpotifyError InvalidClientCredentials ->
                "\nPlease check your client credentials in the Spotify Developer Dashboard"
            | ConfigError(FileNotFound _) ->
                "\nRun 'spotify-cli auth' to authenticate first"
            | HttpError Unauthorized ->
                "\nYour authentication may have expired. Try: spotify-cli auth"
            | HttpError(NetworkTimeout _) ->
                "\nPlease check your internet connection and try again"
            | _ -> ""
        
        $"{message}{helpText}"
    
    /// Print application error to console
    let printAppError (error: AppError) : unit =
        let formattedError = formatAppError error
        printError formattedError
    
    /// Print simple error message to console
    let printSimpleError (message: string) : unit =
        printError message

/// Progress and status output
module StatusOutput =
    
    open ConsoleOutput
    
    /// Show progress indicator
    let showProgress (message: string) : unit =
        let progressIcon = colorize Colors.Blue "⏳"
        Console.Write($"{progressIcon} {message}...")
        Console.Out.Flush()
    
    /// Show completion of progress
    let showProgressComplete () : unit =
        Console.WriteLine(colorize Colors.Green " Done!")
    
    /// Show authentication flow status
    let showAuthStatus (step: string) : unit =
        printInfo $"Auth Step: {step}"
    
    /// Show waiting message
    let showWaiting (message: string) : unit =
        Console.WriteLine(colorize Colors.Yellow $"⏱  {message}")
    
    /// Show OAuth flow instructions
    let showOAuthInstructions () : unit =
        [
            ""
            "🔐 Spotify Authentication Required"
            ""
            "1. Your browser will open to the Spotify login page"
            "2. Log in with your Spotify account"  
            "3. Click 'Agree' to authorize this application"
            "4. You'll be redirected back automatically"
            ""
            "If your browser doesn't open automatically, copy and paste the URL."
        ]
        |> List.iter (fun line ->
            Console.WriteLine(colorize Colors.Cyan line))

/// Table formatting utilities
module TableOutput =
    
    open ConsoleOutput
    
    /// Simple table row data
    type TableRow = string list
    
    /// Format data as a simple table
    let formatTable (headers: string list) (rows: TableRow list) : string =
        if List.isEmpty headers || List.isEmpty rows then
            "No data to display"
        else
            // Calculate column widths
            let allRows = headers :: rows
            let columnWidths = 
                [0 .. headers.Length - 1]
                |> List.map (fun colIndex ->
                    allRows
                    |> List.map (fun row -> 
                        if colIndex < row.Length then row.[colIndex].Length else 0)
                    |> List.max)
            
            // Format header
            let formatRow (row: string list) (isHeader: bool) =
                row
                |> List.mapi (fun i cell ->
                    let width = columnWidths.[i]
                    let padded = cell.PadRight(width)
                    if isHeader then colorize Colors.Cyan padded else padded)
                |> String.concat " | "
            
            let separator = 
                columnWidths
                |> List.map (fun width -> String.replicate width "-")
                |> String.concat "-+-"
            
            [
                yield formatRow headers true
                yield colorize Colors.Gray separator
                yield! rows |> List.map (fun row -> formatRow row false)
            ]
            |> String.concat "\n"