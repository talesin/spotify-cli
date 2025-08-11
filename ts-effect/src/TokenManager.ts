/**
 * Token Management Service Module
 *
 * This module provides automatic token refresh functionality for the Spotify CLI.
 * It handles checking token expiration, automatically refreshing expired tokens,
 * and updating stored credentials without requiring user intervention.
 *
 * Key Features:
 * - Automatic token expiration detection
 * - Transparent token refresh using refresh tokens
 * - Fallback to re-authentication when refresh tokens expire
 * - Integration with existing ConfigService for token persistence
 *
 * @module TokenManager
 */

import { Data, Effect, Layer } from 'effect'
import { ConfigService, TokenData, isTokenExpired } from './ConfigService'
import { SpotifyApi } from './SpotifyApi'
import { loadSpotifyConfig } from './environment'

/**
 * Token Manager Error
 *
 * Represents errors that occur during token management operations
 * including refresh failures and authentication requirements.
 */
export class TokenManagerError extends Data.TaggedError('TokenManagerError')<{
  readonly message: string
  readonly cause?: unknown
}> {}

/**
 * Token Refresh Result
 *
 * Result type indicating whether token refresh was successful,
 * not needed, or requires user re-authentication.
 */
export type TokenRefreshResult =
  | { _tag: 'TokenValid'; tokens: TokenData }
  | { _tag: 'TokenRefreshed'; tokens: TokenData }
  | { _tag: 'ReauthRequired'; reason: string }

/**
 * Ensure Valid Access Token
 *
 * Checks if the current access token is valid, and automatically refreshes
 * it if expired. This function should be called before making any Spotify
 * API requests to ensure the token is valid.
 *
 * @param configService - ConfigService for loading/saving tokens
 * @param spotifyApi - SpotifyApi for refreshing tokens
 * @param tokens - Current token data
 * @returns Effect that yields TokenRefreshResult
 *
 * @example
 * ```typescript
 * const result = yield* ensureValidAccessToken(configService, spotifyApi, tokens)
 *
 * if (result._tag === 'TokenValid' || result._tag === 'TokenRefreshed') {
 *   // Use result.tokens.accessToken for API calls
 * } else {
 *   // Handle re-authentication required
 *   yield* Console.log('Please run `spotify-cli auth` to re-authenticate')
 * }
 * ```
 */
export const ensureValidAccessToken =
  (configService: ConfigService, spotifyApi: SpotifyApi) =>
  (tokens: TokenData): Effect.Effect<TokenRefreshResult, TokenManagerError> =>
    Effect.gen(function* () {
      // Check if token is still valid
      if (!isTokenExpired(tokens)) {
        return { _tag: 'TokenValid' as const, tokens }
      }

      try {
        // Load Spotify configuration for refresh
        const config = yield* loadSpotifyConfig

        // Attempt to refresh the access token
        const refreshedTokens = yield* spotifyApi.refreshAccessToken(
          config.clientId,
          config.clientSecret,
          tokens.refreshToken
        )

        // Create new token data with updated expiration
        const newTokenData: TokenData = {
          _tag: 'TokenData',
          accessToken: refreshedTokens.access_token,
          refreshToken: refreshedTokens.refresh_token ?? tokens.refreshToken,
          expiresAt: Date.now() + refreshedTokens.expires_in * 1000
        }

        // Save the refreshed tokens
        yield* configService.saveTokens(newTokenData)

        return { _tag: 'TokenRefreshed' as const, tokens: newTokenData }
      } catch (error) {
        // If refresh fails, user needs to re-authenticate
        return {
          _tag: 'ReauthRequired' as const,
          reason: error instanceof Error ? error.message : 'Token refresh failed'
        }
      }
    }).pipe(
      Effect.catchAll((error) =>
        Effect.succeed({
          _tag: 'ReauthRequired' as const,
          reason: `Failed to refresh token: ${error}`
        })
      )
    )

/**
 * Get Valid Access Token
 *
 * Convenience function that loads tokens from storage, ensures they are valid
 * (refreshing if necessary), and returns a valid access token ready for use.
 *
 * @param configService - ConfigService for token management
 * @param spotifyApi - SpotifyApi for token refresh
 * @returns Effect that yields valid access token or fails with error
 *
 * @example
 * ```typescript
 * const accessToken = yield* getValidAccessToken(configService, spotifyApi)
 * const user = yield* spotifyApi.getCurrentUser(accessToken)
 * ```
 */
export const getValidAccessToken = (configService: ConfigService, spotifyApi: SpotifyApi) =>
  Effect.gen(function* () {
    // Load existing tokens
    const existingTokens = yield* configService.loadTokens()

    if (existingTokens._tag === 'None') {
      return yield* Effect.fail(
        new TokenManagerError({
          message: 'No authentication tokens found. Please run `spotify-cli auth` first.'
        })
      )
    }

    // Ensure token is valid, refreshing if necessary
    const result = yield* ensureValidAccessToken(configService, spotifyApi)(existingTokens.value)

    if (result._tag === 'ReauthRequired') {
      return yield* Effect.fail(
        new TokenManagerError({
          message: `Re-authentication required: ${result.reason}. Please run \`spotify-cli auth\` to refresh your authentication.`
        })
      )
    }

    return result.tokens.accessToken
  })

/**
 * Token Manager Service
 *
 * Effect Service that provides token management operations with automatic
 * refresh functionality. Depends on ConfigService and SpotifyApi for
 * token storage and refresh operations.
 */
export class TokenManager extends Effect.Service<TokenManager>()('TokenManager', {
  effect: Effect.gen(function* () {
    const configService = yield* ConfigService
    const spotifyApi = yield* SpotifyApi

    return {
      /**
       * Ensure the provided tokens are valid, refreshing if necessary
       */
      ensureValidAccessToken: ensureValidAccessToken(configService, spotifyApi),

      /**
       * Get a valid access token, handling loading and refresh automatically
       */
      getValidAccessToken: () => getValidAccessToken(configService, spotifyApi),

      /**
       * Check if tokens need refreshing without actually refreshing them
       */
      needsRefresh: (tokens: TokenData) => Effect.succeed(isTokenExpired(tokens))
    }
  }),
  dependencies: [ConfigService.Default, SpotifyApi.Default]
}) {}

/**
 * Test Token Manager Service Layer
 *
 * Creates a test layer for TokenManager that allows mocking of token
 * management operations during testing.
 */
export const TestTokenManagerLayer = (mockFunctions?: {
  ensureValidAccessToken?: TokenManager['ensureValidAccessToken']
  getValidAccessToken?: TokenManager['getValidAccessToken']
  needsRefresh?: TokenManager['needsRefresh']
}) =>
  Layer.succeed(
    TokenManager,
    TokenManager.of({
      _tag: 'TokenManager',
      ensureValidAccessToken:
        mockFunctions?.ensureValidAccessToken ??
        ((_tokens) =>
          Effect.succeed({
            _tag: 'TokenValid',
            tokens: {
              _tag: 'TokenData',
              accessToken: 'mock-access-token',
              refreshToken: 'mock-refresh-token',
              expiresAt: Date.now() + 3600000
            }
          })),
      getValidAccessToken:
        mockFunctions?.getValidAccessToken ?? (() => Effect.succeed('mock-access-token')),
      needsRefresh: mockFunctions?.needsRefresh ?? ((_tokens) => Effect.succeed(false))
    })
  )
