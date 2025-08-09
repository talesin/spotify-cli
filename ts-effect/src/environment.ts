/**
 * Environment Configuration Module
 *
 * This module handles the validation and loading of environment variables
 * required for Spotify OAuth integration. It ensures that all necessary
 * credentials are present and properly formatted before the application
 * can authenticate with Spotify's API.
 *
 * Required Environment Variables:
 * - SPOTIFY_CLIENT_ID: The application's client ID from Spotify Developer Dashboard
 * - SPOTIFY_CLIENT_SECRET: The application's client secret (keep secure)
 * - SPOTIFY_REDIRECT_URI: The OAuth callback URL (e.g., http://127.0.0.1:3000/callback)
 *
 * @module Environment
 */

import { Config, Data, Effect, Redacted, Schema } from 'effect'

/**
 * Environment Configuration Error
 *
 * Thrown when required environment variables are missing or invalid.
 * This helps users understand which configuration is needed to run
 * the application successfully.
 */
export class EnvironmentError extends Data.TaggedError('EnvironmentError')<{
  readonly message: string
  readonly missingVars?: readonly string[]
}> {}

/**
 * Spotify OAuth Configuration Schema
 *
 * Validates the structure and presence of required OAuth credentials.
 * This ensures type safety and provides clear error messages when
 * configuration is missing or malformed.
 */
export const SpotifyConfigSchema = Schema.Struct({
  clientId: Schema.String.pipe(Schema.nonEmptyString()),
  clientSecret: Schema.String.pipe(Schema.nonEmptyString()),
  redirectUri: Schema.String.pipe(Schema.nonEmptyString())
})

export type SpotifyConfig = Schema.Schema.Type<typeof SpotifyConfigSchema>

/**
 * Individual Configuration Descriptors
 *
 * These describe how to load and validate each environment variable
 * using Effect-TS Config patterns for type safety and composability.
 */
const clientIdConfig = Config.string('SPOTIFY_CLIENT_ID').pipe(
  Config.withDescription('The application client ID from Spotify Developer Dashboard')
)

const clientSecretConfig = Config.redacted('SPOTIFY_CLIENT_SECRET').pipe(
  Config.withDescription('The application client secret (keep secure!)')
)

const redirectUriConfig = Config.string('SPOTIFY_REDIRECT_URI').pipe(
  Config.withDescription('The OAuth callback URL (e.g., http://127.0.0.1:3000/callback)')
)

/**
 * Load Spotify Configuration from Environment
 *
 * Reads and validates Spotify OAuth credentials from environment variables
 * using Effect-TS Config patterns. This provides type-safe configuration
 * loading with built-in validation and helpful error messages.
 *
 * Environment Variables:
 * - SPOTIFY_CLIENT_ID: Required client ID from Spotify app registration
 * - SPOTIFY_CLIENT_SECRET: Required client secret (keep secure!)
 * - SPOTIFY_REDIRECT_URI: Required callback URL for OAuth flow
 *
 * @returns Effect that yields SpotifyConfig or fails with EnvironmentError
 *
 * @example
 * ```typescript
 * const config = yield* loadSpotifyConfig
 * console.log(`Using client ID: ${config.clientId}`)
 * ```
 */
export const loadSpotifyConfig = Effect.gen(function* () {
  const configResult = yield* Config.all({
    clientId: clientIdConfig,
    clientSecret: clientSecretConfig,
    redirectUri: redirectUriConfig
  }).pipe(
    Effect.mapError(
      (error) =>
        new EnvironmentError({
          message:
            `Missing or invalid Spotify configuration: ${error.message}\n\n` +
            'Please set the following environment variables:\n' +
            '- SPOTIFY_CLIENT_ID: Your Spotify app client ID\n' +
            '- SPOTIFY_CLIENT_SECRET: Your Spotify app client secret\n' +
            '- SPOTIFY_REDIRECT_URI: OAuth callback URI (e.g., http://127.0.0.1:3000)\n\n' +
            'You can get these values from: https://developer.spotify.com/dashboard'
        })
    )
  )

  // Validate and return the final configuration
  return yield* Schema.decodeUnknown(SpotifyConfigSchema)({
    clientId: configResult.clientId,
    clientSecret: Redacted.value(configResult.clientSecret),
    redirectUri: configResult.redirectUri
  }).pipe(
    Effect.mapError(
      (error) =>
        new EnvironmentError({
          message: `Invalid environment configuration: ${error.message}`
        })
    )
  )
})

/**
 * Spotify OAuth Scopes
 *
 * Defines the permissions that the application will request from Spotify.
 * These scopes determine what user data and actions the app can access
 * after successful authentication.
 *
 * Current scopes:
 * - user-read-private: Access to user's subscription details, country, etc.
 * - user-read-email: Access to user's email address
 * - playlist-read-private: Access to user's private playlists
 * - playlist-read-collaborative: Access to collaborative playlists
 */
export const SPOTIFY_SCOPES = [
  'user-read-private',
  'user-read-email',
  'playlist-read-private',
  'playlist-read-collaborative'
] as const

export type SpotifyScope = (typeof SPOTIFY_SCOPES)[number]

/**
 * Default OAuth Configuration Constants
 *
 * Standard OAuth parameters used across the authentication flow.
 * These values align with Spotify's OAuth 2.0 implementation requirements.
 */
export const OAUTH_CONSTANTS = {
  AUTHORIZATION_URL: 'https://accounts.spotify.com/authorize',
  TOKEN_URL: 'https://accounts.spotify.com/api/token',
  RESPONSE_TYPE: 'code',
  DEFAULT_PORT: 3000,
  CALLBACK_PATH: '/callback'
} as const
