/**
 * OAuth 2.0 Authentication Service Module
 *
 * This module implements the complete OAuth 2.0 Authorization Code flow
 * with PKCE (Proof Key for Code Exchange) as an Effect service. It handles
 * URL generation, browser launching, callback server management, and
 * authorization code exchange with proper dependency injection.
 *
 * The implementation follows OAuth 2.0 security best practices:
 * - PKCE to prevent authorization code interception attacks
 * - State parameter to prevent CSRF attacks
 * - Secure random generation for cryptographic values
 * - Proper error handling for all failure scenarios
 *
 * @module OAuthService
 */

import { Data, Effect, Layer } from 'effect'
import { HttpServerRequest } from '@effect/platform'
import { SpotifyConfig, SPOTIFY_SCOPES, OAUTH_CONSTANTS } from './environment'
import { SpotifyApi } from './SpotifyApi'
import { CryptoService } from './CryptoService'
import { BrowserService } from './BrowserService'
import { HttpServerService } from './HttpServerService'
import { startOAuthCallbackServer, extractPortFromRedirectUri } from './CallbackServer'
import { ChildProcess } from 'child_process'

/**
 * OAuth Flow Error
 *
 * Represents errors that occur during the OAuth authentication flow.
 * This includes browser launch failures, server startup issues, user
 * cancellation, and invalid callback responses.
 */
export class OAuthError extends Data.TaggedError('OAuthError')<{
  readonly message: string
  readonly cause?: unknown
}> {}

/**
 * PKCE Challenge Data
 *
 * Contains the code verifier and code challenge used in the PKCE flow.
 * The verifier is kept secret and used later to exchange the authorization
 * code, while the challenge is sent in the initial authorization request.
 */
export interface PKCEChallenge {
  readonly codeVerifier: string
  readonly codeChallenge: string
  readonly codeChallengeMethod: 'S256'
}

/**
 * OAuth State Data
 *
 * Maintains the state of an ongoing OAuth flow, including PKCE parameters
 * and the state parameter used for CSRF protection.
 */
export interface OAuthState {
  readonly pkce: PKCEChallenge
  readonly state: string
}

/**
 * OAuth Callback Result
 *
 * Contains the authorization code and state received from Spotify's
 * OAuth callback, ready for token exchange.
 */
export interface OAuthCallback {
  readonly code: string
  readonly state: string
}

/**
 * Generate PKCE Challenge Implementation
 *
 * Creates a cryptographically secure PKCE challenge pair following RFC 7636.
 * The code verifier is a random 128-character string, and the challenge is
 * the base64url-encoded SHA256 hash of the verifier.
 *
 * @param crypto - CryptoService instance for secure operations
 * @returns Effect that yields PKCEChallenge or fails with OAuthError
 */
const generatePKCEChallenge = (crypto: CryptoService) =>
  Effect.gen(function* () {
    try {
      // Generate 128-character base64url string for code verifier
      const base64UrlString = yield* crypto.generateBase64Url(96)
      const codeVerifier = base64UrlString.slice(0, 128)

      // Create SHA256 hash and encode as base64url for challenge
      const codeChallenge = yield* crypto.createHash('sha256', codeVerifier, 'base64url')

      return {
        codeVerifier,
        codeChallenge,
        codeChallengeMethod: 'S256' as const
      }
    } catch (error) {
      return yield* Effect.fail(
        new OAuthError({
          message: 'Failed to generate PKCE challenge',
          cause: error
        })
      )
    }
  })

/**
 * Generate Secure Random State Implementation
 *
 * Creates a cryptographically secure random state parameter for CSRF protection.
 * This state is included in the authorization request and must match the
 * state returned in the callback to ensure the response is authentic.
 *
 * @param crypto - CryptoService instance for secure operations
 * @returns Effect that yields random state string or fails with OAuthError
 */
const generateState = (crypto: CryptoService) =>
  Effect.gen(function* () {
    try {
      return yield* crypto.generateRandomHex(16)
    } catch (error) {
      return yield* Effect.fail(
        new OAuthError({
          message: 'Failed to generate state parameter',
          cause: error
        })
      )
    }
  })

/**
 * Build Authorization URL Implementation
 *
 * Constructs the Spotify authorization URL with all required parameters
 * including PKCE challenge, scopes, and state for security. The user will
 * be redirected to this URL to grant permission to the application.
 *
 * @param config - Spotify OAuth configuration
 * @param oauthState - OAuth state with PKCE and state parameters
 * @returns Complete authorization URL ready for browser redirect
 */
const buildAuthorizationUrl = (config: SpotifyConfig, oauthState: OAuthState): string => {
  const params = new URLSearchParams({
    response_type: OAUTH_CONSTANTS.RESPONSE_TYPE,
    client_id: config.clientId,
    scope: SPOTIFY_SCOPES.join(' '),
    redirect_uri: config.redirectUri,
    state: oauthState.state,
    code_challenge: oauthState.pkce.codeChallenge,
    code_challenge_method: oauthState.pkce.codeChallengeMethod
  })

  return `${OAUTH_CONSTANTS.AUTHORIZATION_URL}?${params.toString()}`
}

/**
 * Launch Browser Implementation
 *
 * Opens the user's default browser to the authorization URL. This initiates
 * the OAuth flow by directing the user to Spotify's login page.
 *
 * @param browser - BrowserService instance for launching browsers
 * @param url - Authorization URL to open
 * @returns Effect that attempts to launch browser or fails with OAuthError
 */
const launchBrowser = (browser: BrowserService) => (url: string) =>
  browser.launch(url).pipe(
    Effect.mapError(
      (browserError) =>
        new OAuthError({
          message: `Failed to launch browser. Please manually open: ${url}`,
          cause: browserError
        })
    )
  )

/**
 * Parse OAuth Callback Implementation
 *
 * Extracts the authorization code and state from the OAuth callback request.
 * Validates that required parameters are present and returns them in a
 * structured format for further processing.
 *
 * @param request - HTTP request from OAuth callback
 * @returns Effect that yields OAuthCallback or fails with OAuthError
 */
const parseOAuthCallback = (request: HttpServerRequest.HttpServerRequest) =>
  Effect.gen(function* () {
    const url = yield* Effect.try(() => new URL(request.url, 'http://localhost'))

    // Check for OAuth errors first
    yield* Effect.fromNullable(url.searchParams.get('error')).pipe(
      Effect.map(
        (error) =>
          new OAuthError({
            message: `OAuth error: ${error} - ${url.searchParams.get('error_description') ?? 'Unknown error'}`
          })
      ),
      Effect.flip
    )

    // Extract and validate required parameters
    const code = yield* Effect.fromNullable(url.searchParams.get('code')).pipe(
      Effect.mapError(() => new OAuthError({ message: 'Missing authorization code in callback' }))
    )

    const state = yield* Effect.fromNullable(url.searchParams.get('state')).pipe(
      Effect.mapError(() => new OAuthError({ message: 'Missing state parameter in callback' }))
    )

    return { code, state }
  })

/**
 * Start OAuth Callback Server Implementation
 *
 * Starts a real HTTP server to handle OAuth callbacks from Spotify.
 * Extracts the port from the redirect URI and starts a temporary server
 * that waits for the authorization code response.
 *
 * @param httpServer - HttpServerService for server operations
 * @param config - Spotify OAuth configuration
 * @param expectedState - Expected state parameter for validation
 * @returns Effect that yields OAuthCallback or fails with OAuthError
 */
const startCallbackServer =
  (httpServer: HttpServerService) => (config: SpotifyConfig, expectedState: string) =>
    Effect.gen(function* () {
      const port = extractPortFromRedirectUri(config.redirectUri)

      const callback = yield* startOAuthCallbackServer(httpServer)({
        port,
        expectedState,
        timeoutMs: 300_000 // 5 minutes
      }).pipe(
        Effect.mapError(
          (error) =>
            new OAuthError({
              message: `OAuth callback server failed: ${error.message}`,
              cause: error
            })
        )
      )

      return callback
    })

/**
 * Complete OAuth Flow Implementation
 *
 * Orchestrates the complete OAuth authentication flow from start to finish.
 * This is the main function that applications should call to authenticate
 * users with Spotify. It handles all steps: URL generation, browser launch,
 * callback handling, and token exchange.
 *
 * @param crypto - CryptoService for secure operations
 * @param browser - BrowserService for launching browsers
 * @param httpServer - HttpServerService for callback server
 * @param spotifyApi - SpotifyApi for token exchange
 * @param config - Spotify OAuth configuration
 * @returns Effect that yields access/refresh tokens or fails with OAuthError
 */
const completeOAuthFlow =
  (
    crypto: CryptoService,
    browser: BrowserService,
    httpServer: HttpServerService,
    spotifyApi: SpotifyApi
  ) =>
  (config: SpotifyConfig) =>
    Effect.gen(function* () {
      // Generate PKCE challenge and state
      const pkce = yield* generatePKCEChallenge(crypto)
      const state = yield* generateState(crypto)
      const oauthState: OAuthState = { pkce, state }

      // Build authorization URL and launch browser
      const authUrl = buildAuthorizationUrl(config, oauthState)
      yield* launchBrowser(browser)(authUrl)

      // Start callback server and wait for authorization code
      const callback = yield* startCallbackServer(httpServer)(config, state)

      // Exchange authorization code for tokens using SpotifyApi (including PKCE code_verifier)
      const tokens = yield* spotifyApi.exchangeCodeForTokensWithPKCE(
        config.clientId,
        config.clientSecret,
        callback.code,
        config.redirectUri,
        oauthState.pkce.codeVerifier
      )

      return tokens
    })

/**
 * OAuth Service
 *
 * Effect Service that provides OAuth 2.0 authentication operations.
 * Depends on CryptoService for secure operations, BrowserService for
 * launching browsers, HttpServerService for callback handling, and
 * SpotifyApi for token exchange.
 */
export class OAuthService extends Effect.Service<OAuthService>()('OAuthService', {
  effect: Effect.gen(function* () {
    const crypto = yield* CryptoService
    const browser = yield* BrowserService
    const httpServer = yield* HttpServerService
    const spotifyApi = yield* SpotifyApi

    return {
      /**
       * Generate PKCE challenge pair for secure OAuth flow
       */
      generatePKCEChallenge: () => generatePKCEChallenge(crypto),

      /**
       * Generate random state parameter for CSRF protection
       */
      generateState: () => generateState(crypto),

      /**
       * Build authorization URL with all required OAuth parameters
       */
      buildAuthorizationUrl: (config: SpotifyConfig, oauthState: OAuthState) =>
        Effect.succeed(buildAuthorizationUrl(config, oauthState)),

      /**
       * Launch browser to authorization URL
       */
      launchBrowser: launchBrowser(browser),

      /**
       * Parse OAuth callback request for authorization code and state
       */
      parseOAuthCallback: parseOAuthCallback,

      /**
       * Start callback server to handle OAuth redirect (real HTTP server)
       */
      startCallbackServer: startCallbackServer(httpServer),

      /**
       * Complete full OAuth authentication flow
       */
      completeFlow: completeOAuthFlow(crypto, browser, httpServer, spotifyApi)
    }
  }),
  dependencies: [
    CryptoService.Default,
    BrowserService.Default,
    HttpServerService.Default,
    SpotifyApi.Default
  ]
}) {}

/**
 * Test OAuth Service Layer
 *
 * Creates a test layer for OAuthService that allows mocking of OAuth operations
 * during testing. This enables predictable testing of OAuth flows.
 */
export const TestOAuthServiceLayer = (mockFunctions?: {
  generatePKCEChallenge?: OAuthService['generatePKCEChallenge']
  generateState?: OAuthService['generateState']
  buildAuthorizationUrl?: OAuthService['buildAuthorizationUrl']
  launchBrowser?: OAuthService['launchBrowser']
  parseOAuthCallback?: OAuthService['parseOAuthCallback']
  startCallbackServer?: OAuthService['startCallbackServer']
  completeFlow?: OAuthService['completeFlow']
}) =>
  Layer.succeed(
    OAuthService,
    OAuthService.of({
      _tag: 'OAuthService',
      generatePKCEChallenge:
        mockFunctions?.generatePKCEChallenge ??
        (() =>
          Effect.succeed({
            codeVerifier: 'mock-code-verifier',
            codeChallenge: 'mock-code-challenge',
            codeChallengeMethod: 'S256'
          })),
      generateState: mockFunctions?.generateState ?? (() => Effect.succeed('mock-state')),
      buildAuthorizationUrl:
        mockFunctions?.buildAuthorizationUrl ??
        ((_config, _state) => Effect.succeed('https://accounts.spotify.com/authorize?mock=true')),
      launchBrowser:
        mockFunctions?.launchBrowser ??
        ((_url) => Effect.succeed(undefined as unknown as ChildProcess)),
      parseOAuthCallback:
        mockFunctions?.parseOAuthCallback ??
        ((_request) => Effect.succeed({ code: 'mock-code', state: 'mock-state' })),
      startCallbackServer:
        mockFunctions?.startCallbackServer ??
        ((_config, state) => Effect.succeed({ code: 'mock-code', state })),
      completeFlow:
        mockFunctions?.completeFlow ??
        ((_config) =>
          Effect.succeed({
            access_token: 'mock-access-token',
            refresh_token: 'mock-refresh-token',
            expires_in: 3600
          }))
    })
  )
