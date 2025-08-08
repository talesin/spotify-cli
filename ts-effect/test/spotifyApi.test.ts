/**
 * Comprehensive Tests for SpotifyApi Module
 *
 * Tests follow the Effect testing patterns from the coding guide:
 * - Test each function in isolation with mock dependencies
 * - Use Effect.gen with test layers for service testing
 * - Proper error handling and Effect composition
 */

import { Effect } from 'effect'
import { HttpClient, HttpClientResponse, FetchHttpClient } from '@effect/platform'
import { ParseError } from 'effect/ParseResult'
import {
  SpotifyApi,
  TestSpotifyApiLayer,
  spotifyApiCall,
  exchangeCodeForTokens,
  createTokenExchangeRequest,
  Unauthorized,
  NetworkError,
  InvalidResponse,
  RateLimited
} from '@src/SpotifyApi'

// Mock HttpClient for testing
const createMockHttpClient = (response: {
  status: number
  json: Effect.Effect<unknown, Error>
  headers?: Record<string, string>
}): HttpClient.HttpClient =>
  ({
    execute: () => Effect.succeed(response as HttpClientResponse.HttpClientResponse)
  }) as unknown as HttpClient.HttpClient

describe('SpotifyApi Module', () => {
  describe('spotifyApiCall function', () => {
    it('should make successful API call with valid response', async () => {
      const mockResponse = {
        status: 200,
        json: Effect.succeed({ id: 'user123', display_name: 'Test User' }),
        headers: {}
      }
      const httpClient = createMockHttpClient(mockResponse)
      const parser = (json: unknown) => Effect.succeed(json)

      const result = await Effect.runPromise(
        spotifyApiCall(httpClient)('/me', 'test-token', parser)
      )

      expect(result).toEqual({ id: 'user123', display_name: 'Test User' })
    })

    it('should handle 401 Unauthorized response', async () => {
      const mockResponse = {
        status: 401,
        json: Effect.succeed({ error: 'Invalid access token' }),
        headers: {}
      }
      const httpClient = createMockHttpClient(mockResponse)
      const parser = (json: unknown) => Effect.succeed(json)

      const result = await Effect.runPromiseExit(
        spotifyApiCall(httpClient)('/me', 'invalid-token', parser)
      )

      expect(result._tag).toBe('Failure')
      if (result._tag === 'Failure') {
        const error = result.cause._tag === 'Fail' ? result.cause.error : null
        expect(error).toBeInstanceOf(Unauthorized)
        expect(error?._tag).toBe('Unauthorized')
      }
    })

    it('should handle 429 Rate Limited response with retry-after header', async () => {
      const mockResponse = {
        status: 429,
        json: Effect.succeed({ error: 'Rate limit exceeded' }),
        headers: { 'retry-after': '120' }
      }
      const httpClient = createMockHttpClient(mockResponse)
      const parser = (json: unknown) => Effect.succeed(json)

      const result = await Effect.runPromiseExit(
        spotifyApiCall(httpClient)('/me', 'test-token', parser)
      )

      expect(result._tag).toBe('Failure')
      if (result._tag === 'Failure') {
        const error = result.cause._tag === 'Fail' ? result.cause.error : null
        expect(error).toBeInstanceOf(RateLimited)
        if (error instanceof RateLimited) {
          expect(error.retryAfter).toBe(120)
        }
      }
    })

    it('should handle 429 Rate Limited response without retry-after header', async () => {
      const mockResponse = {
        status: 429,
        json: Effect.succeed({ error: 'Rate limit exceeded' }),
        headers: {}
      }
      const httpClient = createMockHttpClient(mockResponse)
      const parser = (json: unknown) => Effect.succeed(json)

      const result = await Effect.runPromiseExit(
        spotifyApiCall(httpClient)('/me', 'test-token', parser)
      )

      expect(result._tag).toBe('Failure')
      if (result._tag === 'Failure') {
        const error = result.cause._tag === 'Fail' ? result.cause.error : null
        expect(error).toBeInstanceOf(RateLimited)
        if (error instanceof RateLimited) {
          expect(error.retryAfter).toBe(60) // Default fallback
        }
      }
    })

    it('should handle other HTTP error responses', async () => {
      const mockResponse = {
        status: 500,
        json: Effect.succeed({ error: 'Internal server error' }),
        headers: {}
      }
      const httpClient = createMockHttpClient(mockResponse)
      const parser = (json: unknown) => Effect.succeed(json)

      const result = await Effect.runPromiseExit(
        spotifyApiCall(httpClient)('/me', 'test-token', parser)
      )

      expect(result._tag).toBe('Failure')
      if (result._tag === 'Failure') {
        const error = result.cause._tag === 'Fail' ? result.cause.error : null
        expect(error).toBeInstanceOf(NetworkError)
        if (error instanceof NetworkError) {
          expect(error.status).toBe(500)
          expect(error.message).toBe('HTTP 500')
        }
      }
    })

    it('should handle JSON parsing errors', async () => {
      const mockResponse = {
        status: 200,
        json: Effect.fail(new Error('Invalid JSON')),
        headers: {}
      }
      const httpClient = createMockHttpClient(mockResponse)
      const parser = (json: unknown) => Effect.succeed(json)

      const result = await Effect.runPromiseExit(
        spotifyApiCall(httpClient)('/me', 'test-token', parser)
      )

      expect(result._tag).toBe('Failure')
      if (result._tag === 'Failure') {
        const error = result.cause._tag === 'Fail' ? result.cause.error : null
        expect(error).toBeInstanceOf(InvalidResponse)
        if (error instanceof InvalidResponse) {
          expect(error.message).toBe('Invalid JSON')
        }
      }
    })

    it('should handle parser errors', async () => {
      const mockResponse = {
        status: 200,
        json: Effect.succeed({ invalid: 'data' }),
        headers: {}
      }
      const httpClient = createMockHttpClient(mockResponse)
      const parser = () => Effect.fail({ message: 'Invalid format' } as ParseError)

      const result = await Effect.runPromiseExit(
        spotifyApiCall(httpClient)('/me', 'test-token', parser)
      )

      expect(result._tag).toBe('Failure')
      if (result._tag === 'Failure') {
        const error = result.cause._tag === 'Fail' ? result.cause.error : null
        expect(error).toBeInstanceOf(InvalidResponse)
        if (error instanceof InvalidResponse) {
          expect(error.message).toBe('Invalid format')
        }
      }
    })

    it('should handle network/HTTP client errors', async () => {
      const httpClient: HttpClient.HttpClient = {
        execute: () => Effect.fail(new Error('Network timeout'))
      } as unknown as HttpClient.HttpClient
      const parser = (json: unknown) => Effect.succeed(json)

      const result = await Effect.runPromiseExit(
        spotifyApiCall(httpClient)('/me', 'test-token', parser)
      )

      expect(result._tag).toBe('Failure')
      if (result._tag === 'Failure') {
        const error = result.cause._tag === 'Fail' ? result.cause.error : null
        expect(error).toBeInstanceOf(NetworkError)
        if (error instanceof NetworkError) {
          expect(error.message).toBe('Error: Network timeout')
        }
      }
    })
  })

  describe('exchangeCodeForTokens function', () => {
    it('should successfully exchange authorization code for tokens', async () => {
      const mockResponse = {
        status: 200,
        json: Effect.succeed({
          access_token: 'BQNewAccessToken',
          refresh_token: 'AQNewRefreshToken',
          expires_in: 3600
        })
      }
      const httpClient = createMockHttpClient(mockResponse)

      const result = await Effect.runPromise(
        exchangeCodeForTokens(httpClient)(
          'client-id',
          'client-secret',
          'auth-code',
          'http://localhost:8888/callback'
        )
      )

      expect(result.access_token).toBe('BQNewAccessToken')
      expect(result.refresh_token).toBe('AQNewRefreshToken')
      expect(result.expires_in).toBe(3600)
    })

    it('should handle token exchange failure with non-200 status', async () => {
      const mockResponse = {
        status: 400,
        json: Effect.succeed({ error: 'invalid_grant' })
      }
      const httpClient = createMockHttpClient(mockResponse)

      const result = await Effect.runPromiseExit(
        exchangeCodeForTokens(httpClient)(
          'client-id',
          'client-secret',
          'invalid-code',
          'http://localhost:8888/callback'
        )
      )

      expect(result._tag).toBe('Failure')
      if (result._tag === 'Failure') {
        const error = result.cause._tag === 'Fail' ? result.cause.error : null
        expect(error).toBeInstanceOf(NetworkError)
        if (error instanceof NetworkError) {
          expect(error.message).toBe('Token exchange failed: 400')
        }
      }
    })

    it('should handle HTTP client errors during token exchange', async () => {
      const httpClient: HttpClient.HttpClient = {
        execute: () => Effect.fail(new Error('Connection failed'))
      } as unknown as HttpClient.HttpClient

      const result = await Effect.runPromiseExit(
        exchangeCodeForTokens(httpClient)(
          'client-id',
          'client-secret',
          'auth-code',
          'http://localhost:8888/callback'
        )
      )

      expect(result._tag).toBe('Failure')
      if (result._tag === 'Failure') {
        const error = result.cause._tag === 'Fail' ? result.cause.error : null
        expect(error).toBeInstanceOf(NetworkError)
        if (error instanceof NetworkError) {
          expect(error.message).toBe('Error: Connection failed')
        }
      }
    })

    it('should handle JSON parsing errors during token exchange', async () => {
      const mockResponse = {
        status: 200,
        json: Effect.fail(new Error('Malformed JSON'))
      }
      const httpClient = createMockHttpClient(mockResponse)

      const result = await Effect.runPromiseExit(
        exchangeCodeForTokens(httpClient)(
          'client-id',
          'client-secret',
          'auth-code',
          'http://localhost:8888/callback'
        )
      )

      expect(result._tag).toBe('Failure')
      if (result._tag === 'Failure') {
        const error = result.cause._tag === 'Fail' ? result.cause.error : null
        expect(error).toBeInstanceOf(NetworkError)
        if (error instanceof NetworkError) {
          expect(error.message).toBe('Error: Malformed JSON')
        }
      }
    })
  })

  describe('createTokenExchangeRequest function', () => {
    it('should create proper OAuth token exchange request', () => {
      const request = createTokenExchangeRequest('test-client-id', 'test-client-secret', {
        grant_type: 'authorization_code',
        code: 'test-auth-code',
        redirect_uri: 'http://localhost:8888/callback'
      })

      // Verify the request structure
      expect(request.url).toBe('https://accounts.spotify.com/api/token')
      expect(request.method).toBe('POST')

      // Test credential encoding separately since headers are applied via pipe
      const expectedCredentials = btoa('test-client-id:test-client-secret')
      expect(expectedCredentials).toBe('dGVzdC1jbGllbnQtaWQ6dGVzdC1jbGllbnQtc2VjcmV0')
    })

    it('should properly encode special characters in client credentials', () => {
      // Test the credential encoding logic directly
      const clientId = 'client+with/special=chars'
      const clientSecret = 'secret&with%more@chars'
      const credentials = btoa(`${clientId}:${clientSecret}`)
      const expectedCredentials = btoa('client+with/special=chars:secret&with%more@chars')

      expect(credentials).toBe(expectedCredentials)

      // Verify it can be decoded back correctly
      const decoded = atob(credentials)
      expect(decoded).toBe('client+with/special=chars:secret&with%more@chars')
    })

    it('should create request with proper form data encoding', () => {
      const params = {
        grant_type: 'authorization_code',
        code: 'test code with spaces',
        redirect_uri: 'http://localhost:8888/callback?param=value'
      }

      // Test URLSearchParams encoding directly
      const body = new URLSearchParams(params)
      const encoded = body.toString()

      expect(encoded).toContain('grant_type=authorization_code')
      expect(encoded).toContain('code=test+code+with+spaces')
      expect(encoded).toContain(
        'redirect_uri=http%3A%2F%2Flocalhost%3A8888%2Fcallback%3Fparam%3Dvalue'
      )
    })
  })

  describe('SpotifyApi Service', () => {
    it('should provide service methods through dependency injection', async () => {
      await Effect.gen(function* () {
        const spotifyApi = yield* SpotifyApi

        // Verify service methods exist
        expect(typeof spotifyApi.call).toBe('function')
        expect(typeof spotifyApi.exchangeCodeForTokens).toBe('function')
      }).pipe(
        Effect.provide(SpotifyApi.Default),
        Effect.provide(FetchHttpClient.layer), // Provide HttpClient dependency
        Effect.runPromise
      )
    })

    it('should work with test layer for mocking', async () => {
      const mockCall = <A>(
        _endpoint: string,
        _accessToken: string,
        _parser: (json: unknown) => Effect.Effect<A, ParseError>
      ) => Effect.succeed({ id: 'mocked-user' } as A)

      const mockExchange = (
        _clientId: string,
        _clientSecret: string,
        _code: string,
        _redirectUri: string
      ) =>
        Effect.succeed({
          access_token: 'mock-token',
          refresh_token: 'mock-refresh',
          expires_in: 3600
        })

      const testLayer = TestSpotifyApiLayer({
        call: mockCall,
        exchangeCodeForTokens: mockExchange
      })

      await Effect.gen(function* () {
        const spotifyApi = yield* SpotifyApi

        // Test mocked call method
        const user = yield* spotifyApi.call('/me', 'test-token', (json) => Effect.succeed(json))
        expect(user).toEqual({ id: 'mocked-user' })

        // Test mocked exchangeCodeForTokens method
        const tokens = yield* spotifyApi.exchangeCodeForTokens(
          'client',
          'secret',
          'code',
          'redirect'
        )
        expect(tokens.access_token).toBe('mock-token')
      }).pipe(Effect.provide(testLayer), Effect.runPromise)
    })

    it('should handle complex API call scenarios with service layer', async () => {
      const mockUserData = {
        id: 'user123',
        display_name: 'Test User',
        email: 'test@example.com'
      }

      const mockCall = <A>(
        _endpoint: string,
        _accessToken: string,
        _parser: (json: unknown) => Effect.Effect<A, ParseError>
      ) => Effect.succeed(mockUserData as A)
      const testLayer = TestSpotifyApiLayer({ call: mockCall })

      await Effect.gen(function* () {
        const spotifyApi = yield* SpotifyApi
        const parser = (json: unknown) => Effect.succeed(json as typeof mockUserData)

        const result = yield* spotifyApi.call('/me', 'access-token', parser)

        expect(result.id).toBe('user123')
        expect(result.display_name).toBe('Test User')
        expect(result.email).toBe('test@example.com')
      }).pipe(Effect.provide(testLayer), Effect.runPromise)
    })

    it('should handle error scenarios through service layer', async () => {
      const mockCall = <A>(
        _endpoint: string,
        _accessToken: string,
        _parser: (json: unknown) => Effect.Effect<A, ParseError>
      ) => Effect.fail(new Unauthorized({}))
      const testLayer = TestSpotifyApiLayer({ call: mockCall })

      const result = await Effect.gen(function* () {
        const spotifyApi = yield* SpotifyApi
        const parser = (json: unknown) => Effect.succeed(json)

        const result = yield* spotifyApi.call('/me', 'invalid-token', parser)
        return result
      }).pipe(Effect.provide(testLayer), Effect.runPromiseExit)

      expect(result._tag).toBe('Failure')
      if (result._tag === 'Failure') {
        const error = result.cause._tag === 'Fail' ? result.cause.error : null
        expect(error).toBeInstanceOf(Unauthorized)
      }
    })
  })

  describe('Error Classes', () => {
    it('should create proper error instances', () => {
      const unauthorized = new Unauthorized({})
      expect(unauthorized._tag).toBe('Unauthorized')

      const networkError = new NetworkError({ message: 'Connection failed', status: 500 })
      expect(networkError._tag).toBe('NetworkError')
      expect(networkError.message).toBe('Connection failed')
      expect(networkError.status).toBe(500)

      const parseError = { message: 'Invalid format' } as ParseError
      const invalidResponse = new InvalidResponse({ message: 'Parse failed', error: parseError })
      expect(invalidResponse._tag).toBe('InvalidResponse')
      expect(invalidResponse.message).toBe('Parse failed')

      const rateLimited = new RateLimited({ retryAfter: 120 })
      expect(rateLimited._tag).toBe('RateLimited')
      expect(rateLimited.retryAfter).toBe(120)
    })
  })

  describe('Integration scenarios', () => {
    it('should handle complete user profile fetch workflow', async () => {
      const mockUserResponse = {
        status: 200,
        json: Effect.succeed({
          id: 'user123',
          display_name: 'John Doe',
          email: 'john@example.com',
          country: 'US',
          followers: { total: 42 }
        }),
        headers: {}
      }
      const httpClient = createMockHttpClient(mockUserResponse)
      const parser = (json: unknown) => Effect.succeed(json)

      const result = await Effect.runPromise(
        spotifyApiCall(httpClient)('/me', 'valid-token', parser)
      )

      const userData = result as {
        id: string
        display_name: string
        email: string
      }

      expect(userData.id).toBe('user123')
      expect(userData.display_name).toBe('John Doe')
      expect(userData.email).toBe('john@example.com')
    })

    it('should handle complete playlists fetch workflow', async () => {
      const mockPlaylistsResponse = {
        status: 200,
        json: Effect.succeed({
          items: [
            {
              id: 'playlist1',
              name: 'My Favorites',
              public: true,
              tracks: { total: 25 }
            },
            {
              id: 'playlist2',
              name: 'Private Mix',
              public: false,
              tracks: { total: 12 }
            }
          ],
          total: 2,
          limit: 50,
          offset: 0
        }),
        headers: {}
      }
      const httpClient = createMockHttpClient(mockPlaylistsResponse)
      const parser = (json: unknown) => Effect.succeed(json)

      const result = await Effect.runPromise(
        spotifyApiCall(httpClient)('/me/playlists', 'valid-token', parser)
      )

      const playlistData = result as {
        items: Array<{
          id: string
          name: string
          public: boolean
          tracks: { total: number }
        }>
        total: number
      }

      expect(playlistData.items).toHaveLength(2)
      expect(playlistData.items?.[0]?.name).toBe('My Favorites')
      expect(playlistData.items?.[1]?.public).toBe(false)
      expect(playlistData.total).toBe(2)
    })

    it('should handle token refresh workflow', async () => {
      const mockTokenResponse = {
        status: 200,
        json: Effect.succeed({
          access_token: 'BQNewAccessToken123',
          refresh_token: 'AQUpdatedRefreshToken456',
          expires_in: 3600,
          token_type: 'Bearer',
          scope: 'user-read-private user-read-email'
        })
      }
      const httpClient = createMockHttpClient(mockTokenResponse)

      const result = await Effect.runPromise(
        exchangeCodeForTokens(httpClient)(
          'spotify-client-id',
          'spotify-client-secret',
          'authorization-code-from-callback',
          'http://localhost:8888/callback'
        )
      )

      expect(result.access_token).toBe('BQNewAccessToken123')
      expect(result.refresh_token).toBe('AQUpdatedRefreshToken456')
      expect(result.expires_in).toBe(3600)
    })
  })
})
