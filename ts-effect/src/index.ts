#!/usr/bin/env node

/**
 * Spotify CLI Application Entry Point
 *
 * This is the main entry point for the Spotify CLI application built with Effect-TS.
 * It provides a command-line interface for interacting with the Spotify Web API,
 * supporting authentication, user profile retrieval, and playlist management.
 *
 * The application is structured as a functional CLI using @effect/cli, which provides
 * type-safe command definition, argument parsing, and error handling through the
 * Effect-TS ecosystem.
 *
 * @author Spotify CLI Team
 * @version 1.0.0
 */

import { Command } from '@effect/cli'
import { Console, Effect } from 'effect'
import { NodeContext, NodeRuntime } from '@effect/platform-node'
import { ConfigService, TokenDataSchema, isTokenExpired } from './config'
import { FetchHttpClient } from '@effect/platform'
import { SpotifyApi } from './SpotifyApi'
import { loadSpotifyConfig, OAUTH_CONSTANTS } from './environment'
import { OAuthService } from './OAuthService'
import { CryptoService } from './CryptoService'
import { BrowserService } from './BrowserService'

/**
 * Authentication Command
 *
 * Handles the complete OAuth flow with Spotify to obtain access and refresh tokens.
 * This command implements the full OAuth 2.0 Authorization Code flow with PKCE:
 * 1. Load environment configuration (client ID, secret, redirect URI)
 * 2. Check for existing valid tokens
 * 3. Launch browser window for Spotify OAuth authorization
 * 4. Start local server to receive the OAuth callback
 * 5. Exchange authorization code for access and refresh tokens
 * 6. Store tokens securely in ~/.spotify-cli/spotify.json
 * 7. Display success confirmation to user
 *
 * Usage: spotify-cli auth
 */
const authCommand = Command.make('auth', {}, () =>
  Effect.gen(function* () {
    const configService = yield* ConfigService
    const oauthService = yield* OAuthService

    yield* Console.log('🎵 Spotify CLI Authentication')
    yield* Console.log('')

    // Load environment configuration
    const spotifyConfig = yield* loadSpotifyConfig

    // Check for existing tokens
    const existingTokens = yield* configService.loadTokens()

    if (existingTokens._tag === 'Some') {
      // Check if existing tokens are still valid
      if (!isTokenExpired(existingTokens.value)) {
        yield* Console.log('✅ You are already authenticated with Spotify!')
        yield* Console.log('')
        yield* Console.log('Your access token is still valid.')
        yield* Console.log(
          'Use `spotify-cli me` to view your profile or `spotify-cli playlists` to see your playlists.'
        )
        return
      } else {
        yield* Console.log('⚠️  Your existing tokens have expired.')
        yield* Console.log('Starting fresh authentication...')
        yield* Console.log('')
      }
    } else {
      yield* Console.log('🔐 No existing authentication found.')
      yield* Console.log('Starting OAuth flow with Spotify...')
      yield* Console.log('')
    }

    // Start OAuth flow
    yield* Console.log(`🌐 Opening browser for Spotify authentication...`)
    yield* Console.log(
      `📡 Starting local server on http://localhost:${OAUTH_CONSTANTS.DEFAULT_PORT}${OAUTH_CONSTANTS.CALLBACK_PATH}`
    )
    yield* Console.log('')
    yield* Console.log('Please complete the authentication in your browser.')
    yield* Console.log(
      "If the browser doesn't open automatically, you'll see the URL to visit manually."
    )
    yield* Console.log('')

    // Execute complete OAuth flow
    const tokens = yield* oauthService.completeFlow(spotifyConfig).pipe(
      Effect.catchAll((error) =>
        Effect.gen(function* () {
          if (error._tag === 'OAuthError') {
            yield* Console.log('❌ Authentication failed:')
            yield* Console.log(`   ${error.message}`)
            yield* Console.log('')
            yield* Console.log('Please try running `spotify-cli auth` again.')
            if (error.cause !== undefined) {
              yield* Console.log(`   Debug info: ${String(error.cause)}`)
            }
          } else {
            yield* Console.log('❌ Configuration error:')
            yield* Console.log(`   ${String(error)}`)
          }
          return yield* Effect.fail(error)
        })
      )
    )

    // Calculate expiration time (tokens typically expire in 1 hour)
    const tokenResponse = tokens as {
      access_token: string
      refresh_token: string
      expires_in: number
    }
    const expiresAt = Date.now() + tokenResponse.expires_in * 1000

    // Create token data structure
    const tokenData = TokenDataSchema.make({
      accessToken: tokenResponse.access_token,
      refreshToken: tokenResponse.refresh_token,
      expiresAt
    })

    // Store tokens securely
    yield* configService.saveTokens(tokenData).pipe(
      Effect.catchTag('ConfigWriteError', (error) =>
        Effect.gen(function* () {
          yield* Console.log('❌ Failed to save authentication tokens:')
          yield* Console.log(`   ${error.message}`)
          yield* Console.log('')
          yield* Console.log('Authentication was successful, but tokens could not be saved.')
          yield* Console.log('Please check file permissions and try again.')
          return yield* Effect.fail(error)
        })
      )
    )

    // Success message
    yield* Console.log('✅ Authentication successful!')
    yield* Console.log('')
    yield* Console.log('🎉 You are now authenticated with Spotify.')
    yield* Console.log(`🔑 Tokens saved securely to: ~/.spotify-cli/spotify.json`)
    yield* Console.log(`⏰ Access token expires: ${new Date(expiresAt).toLocaleString()}`)
    yield* Console.log('')
    yield* Console.log('Next steps:')
    yield* Console.log('  • Run `spotify-cli me` to view your profile')
    yield* Console.log('  • Run `spotify-cli playlists` to see your playlists')
    yield* Console.log('')
    yield* Console.log('Happy listening! 🎶')
  })
)

/**
 * User Profile Command
 *
 * Retrieves and displays the authenticated user's Spotify profile information.
 * This command will:
 * 1. Load stored access token
 * 2. Refresh token if expired
 * 3. Call Spotify's /me endpoint
 * 4. Display user information (name, email, country, etc.)
 *
 * Usage: spotify-cli me
 *
 * Requires prior authentication via `spotify-cli auth`
 */
const meCommand = Command.make('me', {}, () =>
  Effect.gen(function* () {
    const configService = yield* ConfigService
    const httpService = yield* SpotifyApi

    yield* Console.log('User profile command - to be implemented')
    yield* Console.log('Dependencies (ConfigService and HttpService) are injected properly')

    // Example: Demonstrate service availability
    const existingTokens = yield* configService.loadTokens()
    yield* Console.log(
      `Config service working: ${existingTokens._tag === 'Some' ? 'Found tokens' : 'No tokens'}`
    )
    yield* Console.log(`HTTP service available: ${typeof httpService.call === 'function'}`)
  })
)

/**
 * Playlists Command
 *
 * Retrieves and displays the authenticated user's playlists.
 * This command will:
 * 1. Load stored access token
 * 2. Refresh token if expired
 * 3. Call Spotify's /me/playlists endpoint with pagination
 * 4. Display playlist information in a formatted table
 *
 * Usage: spotify-cli playlists
 *
 * Requires prior authentication via `spotify-cli auth`
 */
const playlistsCommand = Command.make('playlists', {}, () =>
  Effect.gen(function* () {
    const configService = yield* ConfigService
    const httpService = yield* SpotifyApi

    yield* Console.log('Playlists command - to be implemented')
    yield* Console.log('Dependencies (ConfigService and HttpService) are injected properly')

    // Example: Demonstrate service availability
    const existingTokens = yield* configService.loadTokens()
    yield* Console.log(
      `Config service working: ${existingTokens._tag === 'Some' ? 'Found tokens' : 'No tokens'}`
    )
    yield* Console.log(
      `HTTP service available: ${typeof httpService.exchangeCodeForTokens === 'function'}`
    )
  })
)

/**
 * Main CLI Command
 *
 * The root command that orchestrates all subcommands. It serves as the entry point
 * for the CLI application and provides help text and version information.
 *
 * The command structure follows a hierarchical pattern:
 * - spotify-cli (root)
 *   ├── auth (authentication)
 *   ├── me (user profile)
 *   └── playlists (user playlists)
 */
const mainCommand = Command.make('spotify-cli', {}, () => Effect.void).pipe(
  Command.withSubcommands([authCommand, meCommand, playlistsCommand])
)

/**
 * CLI Configuration and Runner
 *
 * Creates the CLI application with metadata (name, version) and command structure.
 * The Command.run function returns a function that takes process arguments and
 * returns an Effect that can be executed by the Node.js runtime.
 */
const cli = Command.run(mainCommand, {
  name: 'Spotify CLI',
  version: '1.0.0'
})

/**
 * Application Bootstrap
 *
 * Executes the CLI application with the following flow:
 * 1. Parse command line arguments (process.argv)
 * 2. Provide Effect Services with their .Default layers (as per coding guide)
 * 3. Provide Node platform layers
 * 4. Run the Effect-based application in the Node.js runtime
 *
 * Following the coding guide pattern: "Effect.Service provides a Default property"
 */
NodeRuntime.runMain(
  cli(process.argv).pipe(
    Effect.provide(ConfigService.Default),
    Effect.provide(SpotifyApi.Default),
    Effect.provide(OAuthService.Default),
    Effect.provide(CryptoService.Default),
    Effect.provide(BrowserService.Default),
    Effect.provide(FetchHttpClient.layer),
    Effect.provide(NodeContext.layer)
  )
)
