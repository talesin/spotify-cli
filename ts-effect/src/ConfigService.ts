/**
 * Configuration Management Module
 *
 * This module handles the secure storage and retrieval of Spotify OAuth tokens
 * and other configuration data for the CLI application. It provides a functional
 * approach to configuration management using Effect-TS patterns.
 *
 * Key Features:
 * - Secure token storage in user's home directory
 * - Functional error handling with tagged unions
 * - Platform-agnostic file system operations
 * - Type-safe configuration parsing and validation
 *
 * Storage Location: ~/.spotify-cli/spotify.json
 *
 * @module config
 */

import { Data, Effect, Layer, Option, Schema } from 'effect'
import { FileSystem } from '@effect/platform'
import * as Path from 'path'
import * as Os from 'os'
import { ParseError } from 'effect/ParseResult'
import { PlatformError } from '@effect/platform/Error'

/**
 * Token Data Schema
 *
 * Defines the schema for Spotify OAuth tokens with proper validation.
 * This schema ensures type safety and validation when loading tokens from
 * storage or receiving them from the Spotify API.
 *
 * @property accessToken - Bearer token for Spotify API requests
 * @property refreshToken - Token used to refresh expired access tokens
 * @property expiresAt - Unix timestamp when the access token expires
 */
export const TokenDataSchema = Schema.TaggedStruct('TokenData', {
  accessToken: Schema.String,
  refreshToken: Schema.String,
  expiresAt: Schema.Number
})

/**
 * Token Data Type
 *
 * TypeScript type derived from the TokenData schema for use throughout
 * the application.
 */
export type TokenData = typeof TokenDataSchema.Type

/**
 * Spotify Tokens Type Alias
 *
 * Currently represents only TokenData, but could be extended to support
 * different types of token storage formats in the future.
 */
export type SpotifyTokens = TokenData

/**
 * Configuration Not Found Error
 *
 * Thrown when the configuration file doesn't exist at the expected location.
 * This is not necessarily an error condition - it may indicate the user
 * needs to authenticate for the first time.
 */
export class ConfigNotFound extends Data.TaggedError('ConfigNotFound')<Record<string, never>> {}

/**
 * Configuration Parse Error
 *
 * Thrown when the configuration file exists but cannot be parsed as valid JSON
 * or doesn't contain the expected token structure. This may indicate file
 * corruption or manual modification of the config file.
 */
export class ConfigParseError extends Data.TaggedError('ConfigParseError')<{
  readonly message: string
  readonly error: ParseError | PlatformError
}> {}

/**
 * Configuration Write Error
 *
 * Thrown when the system cannot write the configuration file. This may be due to
 * permission issues, disk space problems, or other file system errors.
 */
export class ConfigWriteError extends Data.TaggedError('ConfigWriteError')<{
  readonly message: string
}> {}

/**
 * Configuration Error Union Type
 *
 * Represents all possible errors that can occur during configuration operations.
 * Using a union type enables exhaustive pattern matching and type-safe error handling.
 */
export type ConfigError = ConfigNotFound | ConfigParseError | ConfigWriteError

/**
 * Get Configuration File Path
 *
 * Constructs the full path to the configuration file in the user's home directory.
 * The configuration is stored in ~/.spotify-cli/spotify.json for easy access
 * and to follow Unix conventions for user-specific application data.
 *
 * @returns Effect that yields the configuration file path
 */
const getConfigPath = Effect.sync(() => Path.join(Os.homedir(), '.spotify-cli', 'spotify.json')) // TODO: Os.homedir should be a resource

/**
 * Ensure Configuration Directory Exists
 *
 * Creates the ~/.spotify-cli directory if it doesn't exist. This is necessary
 * before writing the configuration file. Uses recursive creation to handle
 * cases where parent directories might not exist.
 *
 * @param fs - FileSystem service for directory operations
 * @returns Effect that ensures the config directory exists
 */
const ensureConfigDirectory = (fs: FileSystem.FileSystem) =>
  Effect.gen(function* () {
    const configPath = yield* getConfigPath
    const configDir = Path.dirname(configPath)

    const exists = yield* fs.exists(configDir)
    if (!exists) {
      yield* fs.makeDirectory(configDir, { recursive: true })
    }
  })

/**
 * Load Spotify Tokens from Configuration
 *
 * Attempts to load previously stored Spotify OAuth tokens from the configuration file.
 * This function handles several scenarios:
 *
 * 1. File doesn't exist -> Returns None (user needs to authenticate)
 * 2. File exists but is malformed -> Throws ConfigParseError
 * 3. File exists and is valid -> Returns Some(TokenData)
 *
 * The function uses functional error handling with Effect-TS, making error
 * cases explicit and type-safe.
 *
 * @param fs - FileSystem service for file operations
 * @returns Effect that yields Option<SpotifyTokens> or fails with ConfigError
 *
 * @example
 * ```typescript
 * const fs = yield* FileSystem.FileSystem
 * const result = yield* loadTokens(fs)
 * if (Option.isSome(result)) {
 *   const tokens = result.value
 *   console.log(`Access token: ${tokens.accessToken}`)
 * } else {
 *   console.log('No tokens found - user needs to authenticate')
 * }
 * ```
 */
export const loadTokens = (fs: FileSystem.FileSystem) =>
  Effect.fn(function* () {
    const configPath = yield* getConfigPath

    const exists = yield* fs.exists(configPath).pipe(Effect.catchAll(() => Effect.succeed(false)))
    if (!exists) {
      return Option.none<SpotifyTokens>()
    }

    const content = yield* fs
      .readFileString(configPath)
      .pipe(Effect.mapError((error) => new ConfigParseError({ message: error.message, error })))

    // Decode the textual content to JSON
    const json = yield* Schema.decodeUnknown(Schema.parseJson())(content).pipe(
      Effect.mapError(
        (error) => new ConfigParseError({ message: `Invalid JSON: ${error.message}`, error })
      )
    )

    // Decode the JSON using TokenData schema for validation
    const tokenData: TokenData = yield* Schema.decodeUnknown(TokenDataSchema)(json).pipe(
      Effect.mapError(
        (error) =>
          new ConfigParseError({
            message: `Invalid token format: ${error.message}`,
            error
          })
      )
    )

    return Option.some(tokenData)
  })

/**
 * Save Spotify Tokens to Configuration
 *
 * Securely stores Spotify OAuth tokens to the configuration file. The function:
 *
 * 1. Ensures the configuration directory exists
 * 2. Serializes tokens to JSON format with pretty printing
 * 3. Writes to the configuration file atomically
 * 4. Handles file system errors gracefully
 *
 * The tokens are stored in JSON format for human readability and easy debugging.
 * File permissions should be set appropriately by the OS to prevent unauthorized access.
 *
 * @param fs - FileSystem service for file operations
 * @returns Effect function that takes tokens and saves them
 *
 * @example
 * ```typescript
 * const fs = yield* FileSystem.FileSystem
 * const tokens = TokenDataSchema.make({
 *   accessToken: 'BQC4TJW...',
 *   refreshToken: 'AQDTy8x...',
 *   expiresAt: Date.now() + 3600000
 * })
 *
 * yield* saveTokens(fs)(tokens)
 * console.log('Tokens saved successfully')
 * ```
 */
export const saveTokens = (fs: FileSystem.FileSystem) => (tokens: SpotifyTokens) =>
  Effect.gen(function* () {
    const configPath = yield* getConfigPath

    yield* ensureConfigDirectory(fs).pipe(
      Effect.mapError((error) => new ConfigWriteError({ message: String(error) }))
    )

    const data = JSON.stringify(tokens, null, 2)
    yield* fs
      .writeFileString(configPath, data)
      .pipe(Effect.mapError((error) => new ConfigWriteError({ message: String(error) })))
  })

/**
 * Configuration Service
 *
 * Effect Service that provides configuration management operations.
 * Follows the coding guide pattern with Effect.Service class and automatic
 * Default layer generation for dependency injection.
 */
export class ConfigService extends Effect.Service<ConfigService>()('ConfigService', {
  effect: Effect.gen(function* () {
    const fs = yield* FileSystem.FileSystem
    return {
      loadTokens: loadTokens(fs),
      saveTokens: saveTokens(fs)
    }
  })
}) {}

/**
 * Test Configuration Service Layer
 *
 * Creates a test layer for ConfigService that allows mocking of configuration
 * operations during testing. This follows the pattern shown in the coding guide.
 */
export const TestConfigServiceLayer = (fn?: {
  loadTokens?: ConfigService['loadTokens']
  saveTokens?: ConfigService['saveTokens']
}) =>
  Layer.succeed(
    ConfigService,
    ConfigService.of({
      _tag: 'ConfigService',
      loadTokens: fn?.loadTokens ?? (() => Effect.succeed(Option.none())),
      saveTokens: fn?.saveTokens ?? (() => Effect.succeed(undefined))
    })
  )

/**
 * Check if Token is Expired
 *
 * Determines whether the stored access token has expired by comparing the
 * expiresAt timestamp with the current time. This is a pure function that
 * performs no side effects.
 *
 * Spotify access tokens typically expire after 1 hour (3600 seconds).
 * When a token is expired, the application should use the refresh token
 * to obtain a new access token before making API calls.
 *
 * @param tokens - The tokens to check for expiration
 * @returns true if the token is expired or invalid, false otherwise
 *
 * @example
 * ```typescript
 * const tokens = TokenDataSchema.make({
 *   accessToken: 'BQC4TJW...',
 *   refreshToken: 'AQDTy8x...',
 *   expiresAt: Date.now() - 1000 // Expired 1 second ago
 * })
 *
 * if (isTokenExpired(tokens)) {
 *   console.log('Token needs refresh')
 * }
 * ```
 */
export const isTokenExpired = (tokens: SpotifyTokens): boolean => {
  if (tokens._tag !== 'TokenData') return true
  return Date.now() >= tokens.expiresAt
}
