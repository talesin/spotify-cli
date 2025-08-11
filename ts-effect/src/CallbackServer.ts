/**
 * OAuth Callback Server Module
 *
 * This module implements the HTTP server logic for handling OAuth 2.0
 * authorization code callbacks from Spotify. It manages the temporary
 * server lifecycle, processes incoming callback requests, validates
 * OAuth parameters, and provides user-friendly responses.
 *
 * Key responsibilities:
 * - Start temporary HTTP server for OAuth callback
 * - Parse and validate OAuth callback parameters
 * - Handle success and error scenarios
 * - Return appropriate HTML responses to user's browser
 * - Clean server shutdown after callback processing
 *
 * @module CallbackServer
 */

import { Effect, Data, Ref } from 'effect'
import { HttpServerRequest, HttpServerResponse } from '@effect/platform'
import { HttpServerService, ServerConfig } from './HttpServerService'
import { OAUTH_CONSTANTS } from './environment'

/**
 * Callback Server Error
 *
 * Represents errors specific to OAuth callback server operations
 * including server lifecycle, request processing, and validation failures.
 */
export class CallbackServerError extends Data.TaggedError('CallbackServerError')<{
  readonly message: string
  readonly cause?: unknown
}> {}

/**
 * OAuth Callback Result
 *
 * Contains the authorization code and state received from Spotify's
 * OAuth callback, along with validation status.
 */
export interface OAuthCallback {
  readonly code: string
  readonly state: string
}

/**
 * Callback Server Configuration
 *
 * Configuration for starting the OAuth callback server including
 * timeout settings and expected validation parameters.
 */
export interface CallbackServerConfig extends ServerConfig {
  readonly timeoutMs?: number
  readonly expectedState: string
}

/**
 * Callback Server Result
 *
 * Result from running the OAuth callback server, containing either
 * the successful callback data or error information.
 */
export interface CallbackServerResult {
  readonly success: boolean
  readonly callback?: OAuthCallback
  readonly error?: string
}

/**
 * Parse OAuth Callback Request
 *
 * Extracts and validates OAuth parameters from the callback request URL.
 * Handles both success and error scenarios from Spotify's OAuth response.
 *
 * @param request - HTTP request from OAuth callback
 * @returns Effect that yields OAuthCallback or fails with CallbackServerError
 */
export const parseOAuthCallback = (request: HttpServerRequest.HttpServerRequest) =>
  Effect.gen(function* () {
    const url = yield* Effect.try(() => new URL(request.url, 'http://localhost')).pipe(
      Effect.mapError(
        (error) =>
          new CallbackServerError({
            message: 'Invalid callback URL format',
            cause: error
          })
      )
    )

    // Check for OAuth error response first
    const error = url.searchParams.get('error')
    if (error !== null) {
      const errorDescription = url.searchParams.get('error_description') ?? 'Unknown OAuth error'
      return yield* Effect.fail(
        new CallbackServerError({
          message: `OAuth error: ${error} - ${errorDescription}`
        })
      )
    }

    // Extract and validate required parameters
    const code = url.searchParams.get('code')
    if (code === null) {
      return yield* Effect.fail(
        new CallbackServerError({
          message: 'Missing authorization code in callback'
        })
      )
    }

    const state = url.searchParams.get('state')
    if (state === null) {
      return yield* Effect.fail(
        new CallbackServerError({
          message: 'Missing state parameter in callback'
        })
      )
    }

    return { code, state }
  })

/**
 * Validate OAuth State
 *
 * Validates that the state parameter from the callback matches the
 * expected state to prevent CSRF attacks.
 *
 * @param receivedState - State parameter from OAuth callback
 * @param expectedState - Expected state parameter
 * @returns Effect that succeeds for valid state or fails with error
 */
export const validateOAuthState = (receivedState: string, expectedState: string) =>
  receivedState === expectedState
    ? Effect.succeed(void 0)
    : Effect.fail(
        new CallbackServerError({
          message: `Invalid state parameter. Expected: ${expectedState}, received: ${receivedState}`
        })
      )

/**
 * Create Success Response HTML
 *
 * Creates an HTML response page to display in the user's browser
 * after successful OAuth authorization.
 *
 * @returns HTML content for success response
 */
export const createSuccessResponseHtml = (): string => `
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Spotify CLI - Authorization Successful</title>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            max-width: 600px;
            margin: 100px auto;
            padding: 20px;
            text-align: center;
            background: #f8f9fa;
        }
        .success {
            background: #d4edda;
            border: 1px solid #c3e6cb;
            color: #155724;
            padding: 20px;
            border-radius: 8px;
            margin-bottom: 20px;
        }
        .icon { font-size: 48px; margin-bottom: 20px; }
        h1 { color: #1db954; margin-bottom: 20px; }
        p { margin-bottom: 15px; line-height: 1.5; }
        .note { 
            background: #e2e3e5; 
            padding: 15px; 
            border-radius: 6px; 
            color: #383d41; 
            font-size: 14px; 
        }
    </style>
</head>
<body>
    <div class="success">
        <div class="icon">✅</div>
        <h1>Authorization Successful!</h1>
        <p>You have successfully authorized Spotify CLI to access your Spotify account.</p>
        <p>You can now close this browser window and return to your terminal.</p>
    </div>
    <div class="note">
        <strong>Next Steps:</strong><br>
        Return to your terminal and run commands like:<br>
        • <code>spotify-cli me</code> - View your profile<br>
        • <code>spotify-cli playlists</code> - List your playlists
    </div>
</body>
</html>
`

/**
 * Create Error Response HTML
 *
 * Creates an HTML response page to display in the user's browser
 * when OAuth authorization encounters an error.
 *
 * @param errorMessage - Error message to display
 * @returns HTML content for error response
 */
export const createErrorResponseHtml = (errorMessage: string): string => `
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Spotify CLI - Authorization Error</title>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            max-width: 600px;
            margin: 100px auto;
            padding: 20px;
            text-align: center;
            background: #f8f9fa;
        }
        .error {
            background: #f8d7da;
            border: 1px solid #f5c6cb;
            color: #721c24;
            padding: 20px;
            border-radius: 8px;
            margin-bottom: 20px;
        }
        .icon { font-size: 48px; margin-bottom: 20px; }
        h1 { color: #dc3545; margin-bottom: 20px; }
        p { margin-bottom: 15px; line-height: 1.5; }
        .note { 
            background: #e2e3e5; 
            padding: 15px; 
            border-radius: 6px; 
            color: #383d41; 
            font-size: 14px; 
        }
        .error-details {
            background: #fff;
            border: 1px solid #dee2e6;
            padding: 15px;
            border-radius: 6px;
            margin-top: 20px;
            text-align: left;
            font-family: monospace;
            font-size: 13px;
            color: #495057;
        }
    </style>
</head>
<body>
    <div class="error">
        <div class="icon">❌</div>
        <h1>Authorization Failed</h1>
        <p>There was an error during the Spotify authorization process.</p>
        <p>You can close this browser window and try again from your terminal.</p>
    </div>
    <div class="error-details">
        <strong>Error Details:</strong><br>
        ${errorMessage}
    </div>
    <div class="note">
        <strong>What to try:</strong><br>
        • Run <code>spotify-cli auth</code> again<br>
        • Check your internet connection<br>
        • Verify your Spotify app credentials<br>
        • Make sure the redirect URI is configured correctly
    </div>
</body>
</html>
`

/**
 * Start OAuth Callback Server
 *
 * Starts a temporary HTTP server to handle the OAuth callback from Spotify.
 * The server listens for the authorization code response, validates the
 * parameters, and provides user-friendly HTML responses.
 *
 * @param httpServer - HttpServerService for server operations
 * @param config - Callback server configuration
 * @returns Effect that yields OAuthCallback or fails with CallbackServerError
 */
export const startOAuthCallbackServer =
  (httpServer: HttpServerService) => (config: CallbackServerConfig) =>
    Effect.gen(function* () {
      // Ref to store the callback result
      const resultRef = yield* Ref.make<CallbackServerResult | null>(null)

      // Create request handler
      const callbackHandler = (request: HttpServerRequest.HttpServerRequest) =>
        Effect.gen(function* () {
          console.log(`📨 Received callback request: ${request.method} ${request.url}`)

          // Only handle GET requests to the callback path
          if (request.method !== 'GET') {
            console.log(`❌ Wrong method: ${request.method}`)
            return HttpServerResponse.text('Method not allowed', { status: 405 })
          }

          // Parse the callback URL path
          const url = new URL(request.url, 'http://localhost')
          console.log(
            `🔍 Parsed URL path: ${url.pathname}, expected: ${OAUTH_CONSTANTS.CALLBACK_PATH}`
          )
          if (url.pathname !== OAUTH_CONSTANTS.CALLBACK_PATH) {
            console.log(`❌ Wrong path: ${url.pathname}`)
            return HttpServerResponse.text('Not found', { status: 404 })
          }

          try {
            console.log(`✅ Processing OAuth callback...`)
            // Parse and validate callback parameters
            const callback = yield* parseOAuthCallback(request)
            console.log(`🔑 Parsed callback with code: ${callback.code.substring(0, 20)}...`)

            yield* validateOAuthState(callback.state, config.expectedState)
            console.log(`✅ State validation successful`)

            // Store successful result
            yield* Ref.set(resultRef, {
              success: true,
              callback
            })
            console.log(`✅ Callback result stored successfully`)

            // Return success HTML response
            return HttpServerResponse.html(createSuccessResponseHtml())
          } catch (error) {
            const errorMessage =
              error instanceof CallbackServerError ? error.message : 'Unknown authorization error'

            // Store error result
            yield* Ref.set(resultRef, {
              success: false,
              error: errorMessage
            })

            // Return error HTML response
            return HttpServerResponse.html(createErrorResponseHtml(errorMessage))
          }
        }).pipe(
          Effect.catchAll((error) =>
            Effect.succeed(
              HttpServerResponse.html(
                createErrorResponseHtml(
                  error instanceof CallbackServerError ? error.message : 'Internal server error'
                )
              )
            )
          )
        )

      // Start the server
      const server = yield* httpServer
        .startServer({ port: config.port, host: config.host }, callbackHandler)
        .pipe(
          Effect.mapError((error) => {
            console.log(`❌ Failed to start callback server: ${error}`)
            return new CallbackServerError({
              message: 'Failed to start OAuth callback server',
              cause: error
            })
          })
        )

      // Wait for callback result with timeout - poll every second for up to 5 minutes
      const pollForResult = Effect.gen(function* () {
        let attempts = 0
        const maxAttempts = 300 // 5 minutes at 1 second intervals

        while (attempts < maxAttempts) {
          const result = yield* Ref.get(resultRef)
          if (result) {
            return result
          }
          yield* Effect.sleep('1 seconds')
          attempts++
        }

        return yield* Effect.fail(
          new CallbackServerError({
            message: 'OAuth callback timeout - no response received within the time limit'
          })
        )
      })

      const result = yield* pollForResult.pipe(
        Effect.ensuring(
          // Always stop the server
          server
            .stop()
            .pipe(
              Effect.catchAll((stopError) =>
                Effect.logWarning(`Failed to stop callback server: ${stopError}`)
              )
            )
        )
      )

      // Return the callback data or fail with the error
      if (result.success && result.callback) {
        return result.callback
      } else {
        return yield* Effect.fail(
          new CallbackServerError({
            message: result.error ?? 'Unknown callback server error'
          })
        )
      }
    })

/**
 * Extract Port from Redirect URI
 *
 * Extracts the port number from a redirect URI string.
 * Useful for determining which port to start the callback server on.
 *
 * @param redirectUri - Full redirect URI (e.g., "http://127.0.0.1:3000/callback")
 * @returns Port number or default port if parsing fails
 */
export const extractPortFromRedirectUri = (redirectUri: string): number => {
  try {
    const url = new URL(redirectUri)
    return url.port ? parseInt(url.port, 10) : OAUTH_CONSTANTS.DEFAULT_PORT
  } catch {
    return OAUTH_CONSTANTS.DEFAULT_PORT
  }
}
