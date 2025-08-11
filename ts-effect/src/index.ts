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
import { Console, Effect, Layer } from 'effect'
import { NodeContext, NodeRuntime, NodeHttpServer } from '@effect/platform-node'
import { HttpServer, HttpRouter } from '@effect/platform'
import { createServer } from 'node:http'
import { ConfigService, TokenDataSchema, isTokenExpired } from './ConfigService'
import { TokenManager } from './TokenManager'
import { FetchHttpClient } from '@effect/platform'
import { SpotifyApi } from './SpotifyApi'
import { loadSpotifyConfig, OAUTH_CONSTANTS } from './environment'
import { OAuthService } from './OAuthService'
import { CryptoService } from './CryptoService'
import { BrowserService } from './BrowserService'
import { CallbackServerError } from './CallbackServer'

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
    yield* Console.log('⏳ Waiting for authorization (this may take up to 5 minutes)...')
    yield* Console.log('   Please authorize the application in your browser to continue.')
    yield* Console.log('')

    // Create server runner function that will manage the HTTP server lifecycle
    const serverRunner = (router: HttpRouter.HttpRouter<CallbackServerError>, port: number) =>
      Effect.gen(function* () {
        yield* Console.log(`📡 Starting local server on http://localhost:${port}/callback`)

        // Create server layer
        const ServerLive = NodeHttpServer.layer(() => createServer(), { port })

        // Create the server application with the router
        const app = router.pipe(HttpServer.serve(), HttpServer.withLogAddress)

        // Launch the server and run it
        yield* Layer.launch(Layer.provide(app, ServerLive)).pipe(
          Effect.provide(NodeContext.layer),
          Effect.catchAllDefect(() => Effect.void),
          Effect.catchAll(() => Effect.void)
        )
      })

    // Execute complete OAuth flow
    const tokens = yield* oauthService.completeFlow(spotifyConfig, serverRunner).pipe(
      Effect.tap(() =>
        Console.log('🔄 Processing authorization code and exchanging for tokens...')
      ),
      Effect.catchAll((error) =>
        Effect.gen(function* () {
          if (error._tag === 'OAuthError') {
            yield* Console.log('❌ Authentication failed:')
            yield* Console.log(`   ${error.message}`)
            yield* Console.log('')

            // Provide specific guidance based on error type
            if (error.message.includes('timeout')) {
              yield* Console.log('💡 Tip: The authentication timed out after 5 minutes.')
              yield* Console.log('   Please try again and complete the authorization more quickly.')
            } else if (
              error.message.includes('user denied') ||
              error.message.includes('access_denied')
            ) {
              yield* Console.log('💡 Tip: You need to authorize the application to continue.')
              yield* Console.log(
                '   Please try again and click "Agree" on the Spotify authorization page.'
              )
            } else if (error.message.includes('callback server')) {
              yield* Console.log('💡 Tip: There was an issue with the local callback server.')
              yield* Console.log(
                '   Make sure port 3000 is available or check your firewall settings.'
              )
            }

            yield* Console.log('')
            yield* Console.log('Please try running `spotify-cli auth` again.')

            if (error.cause !== undefined) {
              yield* Console.log(`   Debug info: ${String(error.cause)}`)
            }
          } else {
            yield* Console.log('❌ Configuration error:')
            yield* Console.log(`   ${String(error)}`)
            yield* Console.log('')
            yield* Console.log('💡 Tip: Please check your environment variables:')
            yield* Console.log('   - SPOTIFY_CLIENT_ID')
            yield* Console.log('   - SPOTIFY_CLIENT_SECRET')
            yield* Console.log('   - SPOTIFY_REDIRECT_URI')
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
    const tokenManager = yield* TokenManager
    const spotifyApi = yield* SpotifyApi

    yield* Console.log('👤 Spotify User Profile')
    yield* Console.log('')

    yield* Effect.gen(function* () {
      // Get a valid access token (handles refresh automatically)
      const accessToken = yield* tokenManager.getValidAccessToken()

      yield* Console.log('🔄 Fetching your profile information...')

      // Fetch user profile from Spotify API
      const userProfile = yield* spotifyApi.getCurrentUser(accessToken)

      yield* Console.log('✅ Successfully retrieved your profile information:')
      yield* Console.log('')

      // Display profile information in a formatted box
      const boxWidth = 47 // Total width including borders and spaces
      const labelWidth = 14 // Width for labels like "Display Name:"
      const valueWidth = boxWidth - labelWidth - 4 // 4 for "│ " and " │"

      yield* Console.log('┌─────────────────────────────────────────────┐')

      const displayName = (userProfile.display_name ?? 'N/A').substring(0, valueWidth)
      yield* Console.log(`│ Display Name: ${displayName.padEnd(valueWidth)} │`)

      if (userProfile.email !== undefined && userProfile.email !== '') {
        const email = userProfile.email.substring(0, valueWidth)
        yield* Console.log(`│ Email:        ${email.padEnd(valueWidth)} │`)
      }

      if (userProfile.country !== undefined && userProfile.country !== '') {
        const country = userProfile.country.substring(0, valueWidth)
        yield* Console.log(`│ Country:      ${country.padEnd(valueWidth)} │`)
      }

      if (userProfile.product !== undefined && userProfile.product !== '') {
        const product = userProfile.product.substring(0, valueWidth)
        yield* Console.log(`│ Subscription: ${product.padEnd(valueWidth)} │`)
      }

      const spotifyUri = userProfile.uri.substring(0, valueWidth)
      yield* Console.log(`│ Spotify URI:  ${spotifyUri.padEnd(valueWidth)} │`)

      const followers = userProfile.followers.total.toString().substring(0, valueWidth)
      yield* Console.log(`│ Followers:    ${followers.padEnd(valueWidth)} │`)

      yield* Console.log('└─────────────────────────────────────────────┘')
      yield* Console.log('')

      if (userProfile.external_urls.spotify !== '') {
        yield* Console.log(`🔗 Profile URL: ${userProfile.external_urls.spotify}`)
        yield* Console.log('')
      }
    }).pipe(
      Effect.catchTags({
        TokenManagerError: (error) =>
          Effect.gen(function* () {
            yield* Console.log('❌ Authentication Error')
            yield* Console.log('')
            yield* Console.log(error.message)
          }),
        Unauthorized: (_error) =>
          Effect.gen(function* () {
            yield* Console.log('❌ Authentication expired or invalid')
            yield* Console.log('')
            yield* Console.log('Your authentication has expired or is invalid.')
            yield* Console.log('Please run `spotify-cli auth` to re-authenticate.')
          }),
        NetworkError: (error) =>
          Effect.gen(function* () {
            yield* Console.log('❌ Network Error')
            yield* Console.log('')
            yield* Console.log(
              'Unable to connect to Spotify API. Please check your internet connection.'
            )
            yield* Console.log(`Details: ${error.message}`)
          }),
        RateLimited: (error) =>
          Effect.gen(function* () {
            yield* Console.log('❌ Rate Limited')
            yield* Console.log('')
            yield* Console.log('Spotify API rate limit exceeded. Please try again later.')
            if (error.retryAfter !== undefined && error.retryAfter !== null) {
              yield* Console.log(`Retry after: ${error.retryAfter} seconds`)
            }
          }),
        InvalidResponse: (error) =>
          Effect.gen(function* () {
            yield* Console.log('❌ Invalid Response')
            yield* Console.log('')
            yield* Console.log('Received unexpected response from Spotify API.')
            yield* Console.log(`Details: ${error.message}`)
          })
      }),
      Effect.catchAllDefect((error) =>
        Effect.gen(function* () {
          yield* Console.log('❌ Unexpected Error')
          yield* Console.log('')
          yield* Console.log('An unexpected error occurred while fetching your profile.')
          yield* Console.log(`Details: ${String(error)}`)
          yield* Console.log('')
          yield* Console.log('💡 Troubleshooting tips:')
          yield* Console.log('  • Check your internet connection')
          yield* Console.log('  • Try running `spotify-cli auth` to refresh authentication')
          yield* Console.log('  • Report this issue if the problem persists')
        })
      )
    )
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

    yield* Console.log('🎵 Your Spotify Playlists')
    yield* Console.log('')

    // Check for authentication
    const existingTokens = yield* configService.loadTokens()

    if (existingTokens._tag === 'None') {
      yield* Console.log('❌ Not authenticated')
      yield* Console.log('')
      yield* Console.log('You need to authenticate with Spotify first.')
      yield* Console.log('Run `spotify-cli auth` to get started.')
      return
    }

    // Check if tokens are expired
    if (isTokenExpired(existingTokens.value)) {
      yield* Console.log('⚠️  Authentication expired')
      yield* Console.log('')
      yield* Console.log('Your tokens have expired. Please re-authenticate.')
      yield* Console.log('Run `spotify-cli auth` to refresh your authentication.')
      return
    }

    yield* Console.log('🚧 Feature coming soon!')
    yield* Console.log('')
    yield* Console.log('The playlists feature is currently under development.')
    yield* Console.log('This will display your Spotify playlists with:')
    yield* Console.log('  • Playlist names')
    yield* Console.log('  • Number of tracks')
    yield* Console.log('  • Public/private status')
    yield* Console.log('  • Creation date')
    yield* Console.log('')
    yield* Console.log('Stay tuned for the next update! 🎶')
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
    Effect.provide(TokenManager.Default),
    Effect.provide(FetchHttpClient.layer),
    Effect.provide(NodeContext.layer)
  )
)
