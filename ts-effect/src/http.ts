/**
 * HTTP Client Module for Spotify Web API
 *
 * This module provides a functional, type-safe HTTP client specifically designed
 * for interacting with the Spotify Web API. It handles authentication, error cases,
 * rate limiting, and response parsing using Effect-TS patterns.
 *
 * Key Features:
 * - Automatic bearer token authentication
 * - Comprehensive error handling (401, 429, network errors)
 * - Type-safe response parsing
 * - OAuth token exchange functionality
 * - Functional composition with Effect-TS
 *
 * The module follows REST API best practices and Spotify's API guidelines:
 * - https://developer.spotify.com/documentation/web-api/
 *
 * @module http
 */

import { Data, Effect, Layer, Option } from 'effect'
import { HttpClient, HttpClientRequest, HttpClientResponse, HttpBody } from '@effect/platform'

/**
 * Unauthorized Error (HTTP 401)
 *
 * Indicates that the provided access token is invalid, expired, or missing.
 * When this error occurs, the application should attempt to refresh the token
 * using the refresh token, or prompt the user to re-authenticate.
 *
 * Common causes:
 * - Access token has expired (tokens expire after 1 hour)
 * - Token was revoked by the user
 * - Invalid or malformed token
 */
export class Unauthorized extends Data.TaggedClass('Unauthorized')<Record<string, never>> {}

/**
 * Network Error
 *
 * Represents various network-related and HTTP errors that don't fall into
 * other specific categories. This includes connection failures, DNS resolution
 * errors, timeouts, and HTTP status codes >= 400 (except 401 and 429).
 */
export class NetworkError extends Data.TaggedClass('NetworkError')<{
  readonly error: string
}> {}

/**
 * Invalid Response Error
 *
 * Thrown when the Spotify API returns a response that cannot be parsed as
 * expected JSON or doesn't match the expected schema. This may indicate:
 * - API changes or inconsistencies
 * - Corrupted response data
 * - Unexpected response format
 */
export class InvalidResponse extends Data.TaggedClass('InvalidResponse')<{
  readonly error: string
}> {}

/**
 * Rate Limited Error (HTTP 429)
 *
 * Spotify enforces rate limits on API requests. When exceeded, the API returns
 * HTTP 429 with a Retry-After header indicating how long to wait before
 * making another request.
 *
 * Spotify's rate limits vary by endpoint but are generally generous for
 * normal usage patterns. The client should respect the retryAfter value.
 */
export class RateLimited extends Data.TaggedClass('RateLimited')<{
  readonly retryAfter: number
}> {}

/**
 * Spotify API Error Union Type
 *
 * Represents all possible errors that can occur when making requests to
 * the Spotify Web API. Using a union type enables exhaustive pattern
 * matching and type-safe error handling throughout the application.
 */
export type SpotifyApiError = Unauthorized | NetworkError | InvalidResponse | RateLimited

/**
 * Spotify Web API Base URL
 *
 * All Spotify Web API endpoints are prefixed with this base URL.
 * Version 1 is the current stable version of the API.
 */
const SPOTIFY_BASE_URL = 'https://api.spotify.com/v1'

/**
 * Create Authenticated HTTP Request
 *
 * Creates a properly configured HTTP request for the Spotify Web API with
 * the necessary authentication headers. All API requests require a valid
 * Bearer token in the Authorization header.
 *
 * @param endpoint - The API endpoint path (e.g., '/me', '/me/playlists')
 * @param accessToken - Valid Spotify access token
 * @returns Configured HttpClientRequest ready for execution
 *
 * @example
 * ```typescript
 * const request = createAuthenticatedRequest('/me', 'BQC4TJW...')
 * // Creates: GET https://api.spotify.com/v1/me
 * // Headers: Authorization: Bearer BQC4TJW...
 * ```
 */
const createAuthenticatedRequest = (endpoint: string, accessToken: string) =>
  HttpClientRequest.get(`${SPOTIFY_BASE_URL}${endpoint}`).pipe(
    HttpClientRequest.setHeader('Authorization', `Bearer ${accessToken}`),
    HttpClientRequest.setHeader('Content-Type', 'application/json')
  )

/**
 * Handle HTTP Response with Error Cases
 *
 * Higher-order function that processes HTTP responses from the Spotify API,
 * handling various error conditions and parsing successful responses.
 *
 * Error handling follows Spotify API conventions:
 * - 401: Unauthorized (token invalid/expired)
 * - 429: Rate limited (respect Retry-After header)
 * - 4xx/5xx: Other HTTP errors
 * - Parse errors: Invalid JSON or unexpected format
 *
 * @param parser - Function to parse successful response JSON
 * @returns Function that takes HttpClientResponse and returns Effect with parsed result
 *
 * @example
 * ```typescript
 * const parseUser = (json: unknown) => json as SpotifyUser
 * const handler = handleHttpResponse(parseUser)
 * const result = yield* handler(response)
 * ```
 */
const handleHttpResponse =
  <A>(parser: (json: unknown) => A) =>
  (response: HttpClientResponse.HttpClientResponse) =>
    Effect.gen(function* () {
      // Handle unauthorized - token is invalid or expired
      if (response.status === 401) {
        return yield* Effect.fail(new Unauthorized({}))
      }

      // Handle rate limiting - respect the Retry-After header
      if (response.status === 429) {
        const retryAfter = Option.getOrElse(
          Option.fromNullable(response.headers['retry-after']),
          () => '60' // Default to 60 seconds if header is missing
        )
        return yield* Effect.fail(
          new RateLimited({
            retryAfter: parseInt(retryAfter, 10)
          })
        )
      }

      // Handle other HTTP errors (4xx, 5xx)
      if (response.status >= 400) {
        return yield* Effect.fail(
          new NetworkError({
            error: `HTTP ${response.status}`
          })
        )
      }

      // Parse successful response
      try {
        const json = yield* response.json
        return parser(json)
      } catch (error) {
        return yield* Effect.fail(
          new InvalidResponse({
            error: String(error)
          })
        )
      }
    })

/**
 * Make Authenticated Spotify API Call
 *
 * Generic function for making authenticated requests to the Spotify Web API.
 * Handles the complete request lifecycle including authentication, error handling,
 * and response parsing.
 *
 * This is the primary interface for interacting with Spotify endpoints. It:
 * 1. Creates an authenticated HTTP request
 * 2. Executes the request using the HTTP client
 * 3. Handles various error cases (401, 429, network errors)
 * 4. Parses and returns the typed response
 *
 * @param endpoint - Spotify API endpoint (e.g., '/me', '/me/playlists')
 * @param accessToken - Valid Spotify access token
 * @param parser - Function to parse the JSON response into the expected type
 * @returns Effect that yields the parsed response or fails with SpotifyApiError
 *
 * @example
 * ```typescript
 * interface SpotifyUser {
 *   id: string
 *   display_name: string
 *   email: string
 * }
 *
 * const parseUser = (json: unknown) => json as SpotifyUser
 * const user = yield* spotifyApiCall('/me', accessToken, parseUser)
 * console.log(`Welcome, ${user.display_name}!`)
 * ```
 */
export const spotifyApiCall =
  (httpClient: HttpClient.HttpClient) =>
  <A>(endpoint: string, accessToken: string, parser: (json: unknown) => A) =>
    Effect.gen(function* () {
      const request = createAuthenticatedRequest(endpoint, accessToken)
      const response = yield* httpClient.execute(request)
      const result = yield* handleHttpResponse(parser)(response)
      return result
    }).pipe(
      Effect.mapError((error): SpotifyApiError => {
        // Handle HttpClient errors and map to our SpotifyApiError union
        if (
          error instanceof Unauthorized ||
          error instanceof NetworkError ||
          error instanceof InvalidResponse ||
          error instanceof RateLimited
        ) {
          return error
        }
        return new NetworkError({ error: String(error) })
      })
    )

/**
 * Create OAuth Token Exchange Request
 *
 * Creates an HTTP request for exchanging authorization codes or refresh tokens
 * with Spotify's OAuth token endpoint. This follows OAuth 2.0 specifications
 * for the Authorization Code flow.
 *
 * The request uses Basic authentication with the client credentials and
 * form-encoded body parameters as required by Spotify's OAuth implementation.
 *
 * @param clientId - Spotify application client ID
 * @param clientSecret - Spotify application client secret
 * @param params - OAuth parameters (grant_type, code, refresh_token, etc.)
 * @returns Configured HTTP request ready for token exchange
 *
 * @see https://developer.spotify.com/documentation/web-api/tutorials/code-flow
 */
export const createTokenExchangeRequest = (
  clientId: string,
  clientSecret: string,
  params: Record<string, string>
) => {
  const body = new URLSearchParams(params)
  const credentials = btoa(`${clientId}:${clientSecret}`)

  return HttpClientRequest.post('https://accounts.spotify.com/api/token').pipe(
    HttpClientRequest.setHeader('Authorization', `Basic ${credentials}`),
    HttpClientRequest.setHeader('Content-Type', 'application/x-www-form-urlencoded'),
    HttpClientRequest.setBody(HttpBody.text(body.toString()))
  )
}

/**
 * Exchange Authorization Code for Tokens
 *
 * Exchanges an authorization code received from Spotify's OAuth callback
 * for access and refresh tokens. This is the final step in the OAuth
 * Authorization Code flow.
 *
 * The authorization code is obtained when the user completes the OAuth
 * flow in their browser and is redirected back to the application's
 * redirect URI with the code as a query parameter.
 *
 * @param clientId - Spotify application client ID
 * @param clientSecret - Spotify application client secret
 * @param code - Authorization code from OAuth callback
 * @param redirectUri - The redirect URI used in the initial OAuth request
 * @returns Effect that yields token response or fails with NetworkError
 *
 * @example
 * ```typescript
 * const tokens = yield* exchangeCodeForTokens(
 *   'your-client-id',
 *   'your-client-secret',
 *   'received-auth-code',
 *   'http://localhost:8888/callback'
 * )
 *
 * console.log(`Access token: ${tokens.access_token}`)
 * console.log(`Expires in: ${tokens.expires_in} seconds`)
 * ```
 */
export const exchangeCodeForTokens =
  (httpClient: HttpClient.HttpClient) =>
  (clientId: string, clientSecret: string, code: string, redirectUri: string) =>
    Effect.gen(function* () {
      const request = createTokenExchangeRequest(clientId, clientSecret, {
        grant_type: 'authorization_code',
        code,
        redirect_uri: redirectUri
      })

      const response = yield* httpClient.execute(request)

      if (response.status !== 200) {
        return yield* Effect.fail(
          new NetworkError({
            error: `Token exchange failed: ${response.status}`
          })
        )
      }

      const json = yield* response.json
      return json as {
        access_token: string
        refresh_token: string
        expires_in: number
      }
    }).pipe(
      Effect.mapError((error): SpotifyApiError => {
        if (error instanceof NetworkError) {
          return error
        }
        return new NetworkError({ error: String(error) })
      })
    )

/**
 * HTTP Service
 *
 * Effect Service that provides HTTP operations for Spotify API.
 * Follows the coding guide pattern with Effect.Service class and automatic
 * Default layer generation for dependency injection.
 */
export class HttpService extends Effect.Service<HttpService>()('HttpService', {
  effect: Effect.gen(function* () {
    const httpClient = yield* HttpClient.HttpClient
    return {
      spotifyApiCall: spotifyApiCall(httpClient),
      exchangeCodeForTokens: exchangeCodeForTokens(httpClient)
    }
  })
}) {}

/**
 * Test HTTP Service Layer
 *
 * Creates a test layer for HttpService that allows mocking of HTTP operations
 * during testing. This follows the pattern shown in the coding guide.
 */
export const TestHttpServiceLayer = (fn?: {
  spotifyApiCall?: HttpService['spotifyApiCall'] | undefined
  exchangeCodeForTokens?: HttpService['exchangeCodeForTokens'] | undefined
}) =>
  Layer.succeed(
    HttpService,
    HttpService.of({
      _tag: 'HttpService',
      spotifyApiCall: fn?.spotifyApiCall ?? (() => Effect.succeed({} as any)), // eslint-disable-line
      exchangeCodeForTokens:
        fn?.exchangeCodeForTokens ??
        (() =>
          Effect.succeed({
            access_token: 'test-token',
            refresh_token: 'test-refresh',
            expires_in: 3600
          }))
    })
  )
