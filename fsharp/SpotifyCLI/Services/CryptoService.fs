namespace SpotifyCLI.Services

open System
open System.Security.Cryptography
open System.Text
open FsToolkit.ErrorHandling
open SpotifyCLI.Domain

/// Cryptographic operations abstraction for dependency injection
type ICryptoService =
    abstract GenerateRandomString: int -> Result<string, CryptoError>
    abstract GenerateCodeVerifier: unit -> Result<CodeVerifier, CryptoError>
    abstract GenerateCodeChallenge: CodeVerifier -> Result<CodeChallenge, CryptoError>
    abstract GenerateState: unit -> Result<string, CryptoError>

/// System cryptographic service implementation using .NET crypto APIs
type SystemCryptoService() =
    
    /// Character set for URL-safe base64 encoding (RFC 4648)
    let base64UrlChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_"
    
    /// Generate cryptographically secure random bytes
    let generateRandomBytes (length: int) : Result<byte[], CryptoError> =
        try
            use rng = RandomNumberGenerator.Create()
            let bytes = Array.zeroCreate length
            rng.GetBytes(bytes)
            Ok bytes
        with
        | ex -> Error(RandomGenerationFailed ex.Message)
    
    /// Encode bytes to URL-safe base64 string
    let encodeBase64Url (bytes: byte[]) : Result<string, CryptoError> =
        try
            let base64 = Convert.ToBase64String(bytes)
            let urlSafe = base64.TrimEnd('=').Replace('+', '-').Replace('/', '_')
            Ok urlSafe
        with
        | ex -> Error(EncodingError(Convert.ToBase64String(bytes), ex.Message))
    
    /// Generate URL-safe random string of specified length
    let generateUrlSafeString (length: int) : Result<string, CryptoError> =
        result {
            let! randomBytes = generateRandomBytes length
            return! encodeBase64Url randomBytes
        }
    
    /// Compute SHA256 hash of input string
    let computeSHA256Hash (input: string) : Result<byte[], CryptoError> =
        try
            use sha256 = SHA256.Create()
            let inputBytes = Encoding.UTF8.GetBytes(input)
            let hashBytes = sha256.ComputeHash(inputBytes)
            Ok hashBytes
        with
        | ex -> Error(HashingError ex.Message)
    
    interface ICryptoService with
        
        member _.GenerateRandomString(length: int) =
            if length <= 0 then
                Error(RandomGenerationFailed "Length must be positive")
            else
                generateUrlSafeString length
        
        member _.GenerateCodeVerifier() =
            // PKCE code verifier: 43-128 URL-safe characters (RFC 7636)
            let verifierLength = 43 // Minimum recommended length
            generateUrlSafeString verifierLength
            |> Result.map CodeVerifier
        
        member _.GenerateCodeChallenge(codeVerifier: CodeVerifier) =
            let verifierString = TypeExtraction.getCodeVerifier codeVerifier
            result {
                let! hashBytes = computeSHA256Hash verifierString
                let! encoded = encodeBase64Url hashBytes
                return CodeChallenge encoded
            }
        
        member _.GenerateState() =
            // OAuth state parameter: 32 URL-safe characters
            let stateLength = 32
            generateUrlSafeString stateLength

/// Factory functions for creating crypto service
module CryptoService =
    
    let create () : ICryptoService =
        SystemCryptoService() :> ICryptoService

/// Crypto service utilities and helpers
module CryptoServiceHelpers =
    
    /// Generate complete PKCE pair (verifier and challenge)
    let generatePKCEPair (cryptoService: ICryptoService) : Result<CodeVerifier * CodeChallenge, CryptoError> =
        result {
            let! verifier = cryptoService.GenerateCodeVerifier()
            let! challenge = cryptoService.GenerateCodeChallenge(verifier)
            return (verifier, challenge)
        }
    
    /// Validate code verifier format (URL-safe base64, 43-128 chars)
    let isValidCodeVerifier (verifier: string) : bool =
        let length = verifier.Length
        length >= 43 && length <= 128 &&
        verifier |> Seq.forall (fun c ->
            (c >= 'A' && c <= 'Z') || 
            (c >= 'a' && c <= 'z') || 
            (c >= '0' && c <= '9') ||
            c = '-' || c = '_')
    
    /// Validate state parameter format (URL-safe, reasonable length)
    let isValidState (state: string) : bool =
        let length = state.Length
        length >= 16 && length <= 128 &&
        state |> Seq.forall (fun c ->
            (c >= 'A' && c <= 'Z') || 
            (c >= 'a' && c <= 'z') || 
            (c >= '0' && c <= '9') ||
            c = '-' || c = '_')
    
    /// Generate OAuth nonce for additional security
    let generateNonce (cryptoService: ICryptoService) : Result<string, CryptoError> =
        cryptoService.GenerateRandomString(16)
    
    /// Create secure random password for temporary use
    let generateTemporaryPassword (cryptoService: ICryptoService) (length: int) : Result<string, CryptoError> =
        if length < 8 then
            Error(RandomGenerationFailed "Password length must be at least 8 characters")
        elif length > 128 then
            Error(RandomGenerationFailed "Password length cannot exceed 128 characters")
        else
            cryptoService.GenerateRandomString(length)