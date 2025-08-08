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
 * - SPOTIFY_REDIRECT_URI: The OAuth callback URL (e.g., http://localhost:3000/callback)
 *
 * @module Environment
 */

import { Data, Effect, Schema } from 'effect'

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
 * Load Spotify Configuration from Environment
 *
 * Reads and validates Spotify OAuth credentials from environment variables.
 * This function checks for the presence of required variables and ensures
 * they meet basic validation requirements (non-empty strings).
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
 * const config = yield* loadSpotifyConfig()
 * console.log(`Using client ID: ${config.clientId}`)
 * ```
 */
export const loadSpotifyConfig = Effect.gen(function* () {
  const clientId = process.env['SPOTIFY_CLIENT_ID']
  const clientSecret = process.env['SPOTIFY_CLIENT_SECRET']
  const redirectUri = process.env['SPOTIFY_REDIRECT_URI']

  const missingVars: string[] = []

  if (clientId === undefined || clientId === '') missingVars.push('SPOTIFY_CLIENT_ID')
  if (clientSecret === undefined || clientSecret === '') missingVars.push('SPOTIFY_CLIENT_SECRET')
  if (redirectUri === undefined || redirectUri === '') missingVars.push('SPOTIFY_REDIRECT_URI')

  if (missingVars.length > 0) {
    return yield* Effect.fail(
      new EnvironmentError({
        message:
          `Missing required environment variables: ${missingVars.join(', ')}\n\n` +
          'Please set the following environment variables:\n' +
          '- SPOTIFY_CLIENT_ID: Your Spotify app client ID\n' +
          '- SPOTIFY_CLIENT_SECRET: Your Spotify app client secret\n' +
          '- SPOTIFY_REDIRECT_URI: OAuth callback URI (e.g., http://localhost:3000/callback)\n\n' +
          'You can get these values from: https://developer.spotify.com/dashboard',
        missingVars
      })
    )
  }

  const rawConfig = {
    clientId,
    clientSecret,
    redirectUri
  }

  return yield* Schema.decodeUnknown(SpotifyConfigSchema)(rawConfig).pipe(
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
