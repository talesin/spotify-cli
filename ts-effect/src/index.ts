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
import { ConfigService } from './config'
import { FetchHttpClient } from '@effect/platform'
import { SpotifyApi } from './SpotifyApi'

/**
 * Authentication Command
 *
 * Handles the OAuth flow with Spotify to obtain access and refresh tokens.
 * This command will:
 * 1. Launch a browser window for Spotify OAuth
 * 2. Start a local server to receive the callback
 * 3. Exchange authorization code for tokens
 * 4. Store tokens securely in ~/.spotify-cli/spotify.json
 *
 * Usage: spotify-cli auth
 */
const authCommand = Command.make('auth', {}, () =>
  Effect.gen(function* () {
    const configService = yield* ConfigService

    yield* Console.log('Authentication command - to be implemented')
    yield* Console.log('Dependencies (ConfigService) are injected properly')

    // Example: Load tokens to demonstrate dependency injection works
    const existingTokens = yield* configService.loadTokens()

    // Use Effect-TS pattern matching with Option
    yield* existingTokens._tag === 'Some'
      ? Console.log('Found existing tokens - user is already authenticated')
      : Console.log('No tokens found - user needs to authenticate')
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
cli(process.argv).pipe(
  Effect.provide(ConfigService.Default),
  Effect.provide(SpotifyApi.Default),
  Effect.provide(FetchHttpClient.layer),
  Effect.provide(NodeContext.layer),
  NodeRuntime.runMain
)
