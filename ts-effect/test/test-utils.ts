/**
 * Test Utilities for Spotify CLI Application
 * 
 * This module provides common testing utilities, mock data, and helper functions
 * for testing the Spotify CLI application. It includes Effect-TS testing utilities
 * and Spotify API mock responses.
 * 
 * @module test-utils
 */

import { Effect, Option } from 'effect'
import { TokenDataSchema } from '../src/config'
import * as Os from 'os'
import * as Path from 'path'

/**
 * Mock Spotify Token Data
 * 
 * Provides realistic test data for Spotify OAuth tokens.
 */
export const mockTokens = {
  valid: TokenDataSchema.make({
    accessToken: 'BQC4TJWdRHO1vz6Gq2bUtZ9xF8HgN5kLaR3mPqE7',
    refreshToken: 'AQDTy8xNQ2LmJ4R9vK8sF3nH7pL6qE9tY2wI0oP5',
    expiresAt: Date.now() + 3600000 // Expires in 1 hour
  }),
  
  expired: TokenDataSchema.make({
    accessToken: 'BQExpiredTokenHere123456789',
    refreshToken: 'AQRefreshTokenForExpiredAccess',
    expiresAt: Date.now() - 1000 // Expired 1 second ago
  }),
  
  soonToExpire: TokenDataSchema.make({
    accessToken: 'BQSoonToExpireToken987654321',
    refreshToken: 'AQRefreshTokenSoonExpire',
    expiresAt: Date.now() + 30000 // Expires in 30 seconds
  })
}

/**
 * Mock Spotify API Responses
 * 
 * Contains realistic mock responses from Spotify Web API endpoints.
 */
export const mockSpotifyResponses = {
  user: {
    id: 'testuser123',
    display_name: 'Test User',
    email: 'test@example.com',
    country: 'US',
    external_urls: {
      spotify: 'https://open.spotify.com/user/testuser123'
    },
    followers: {
      total: 42
    },
    images: [
      {
        url: 'https://i.scdn.co/image/ab67616d0000b273...',
        height: 64,
        width: 64
      }
    ]
  },
  
  playlists: {
    items: [
      {
        id: 'playlist1',
        name: 'My Awesome Playlist',
        public: true,
        tracks: {
          total: 25
        },
        external_urls: {
          spotify: 'https://open.spotify.com/playlist/playlist1'
        }
      },
      {
        id: 'playlist2', 
        name: 'Private Mix',
        public: false,
        tracks: {
          total: 12
        },
        external_urls: {
          spotify: 'https://open.spotify.com/playlist/playlist2'
        }
      }
    ],
    total: 2,
    limit: 50,
    offset: 0
  },
  
  tokenResponse: {
    access_token: 'BQNewAccessTokenFromRefresh',
    refresh_token: 'AQNewRefreshTokenFromRefresh',
    expires_in: 3600,
    token_type: 'Bearer',
    scope: 'user-read-private user-read-email playlist-read-private'
  }
}

/**
 * Test Configuration Paths
 * 
 * Provides test-specific file paths that don't interfere with actual user config.
 */
export const testPaths = {
  configDir: Path.join(Os.tmpdir(), 'spotify-cli-test'),
  configFile: Path.join(Os.tmpdir(), 'spotify-cli-test', 'spotify.json')
}

/**
 * Effect Test Helpers
 * 
 * Utilities for testing Effect-TS code.
 */
export const effectHelpers = {
  /**
   * Run an Effect and return the result or throw the error
   */
  unsafeRun: <E, A>(effect: Effect.Effect<A, E>): Promise<A> =>
    Effect.runPromise(effect),
  
  /**
   * Run an Effect expecting it to fail and return the error
   */
  unsafeRunExpectFailure: <E, A>(effect: Effect.Effect<A, E>): Promise<E> =>
    Effect.runPromise(Effect.flip(effect)),
  
  /**
   * Create a successful Effect with the given value
   */
  succeed: <A>(value: A) => Effect.succeed(value),
  
  /**
   * Create a failing Effect with the given error
   */
  fail: <E>(error: E) => Effect.fail(error),
  
  /**
   * Create an Effect that returns None
   */
  none: <A>() => Effect.succeed(Option.none<A>()),
  
  /**
   * Create an Effect that returns Some(value)
   */
  some: <A>(value: A) => Effect.succeed(Option.some(value))
}

/**
 * HTTP Mock Helpers
 * 
 * Utilities for mocking HTTP responses in tests.
 */
export const httpMocks = {
  /**
   * Create a successful HTTP response mock
   */
  successResponse: (body: unknown, status = 200) => ({
    status,
    json: Effect.succeed(body),
    headers: {}
  }),
  
  /**
   * Create an unauthorized HTTP response mock
   */
  unauthorizedResponse: () => ({
    status: 401,
    json: Effect.succeed({ error: 'Invalid access token' }),
    headers: {}
  }),
  
  /**
   * Create a rate limited HTTP response mock
   */
  rateLimitedResponse: (retryAfter = 60) => ({
    status: 429,
    json: Effect.succeed({ error: 'Rate limit exceeded' }),
    headers: { 'retry-after': retryAfter.toString() }
  }),
  
  /**
   * Create a network error HTTP response mock
   */
  networkErrorResponse: (status = 500) => ({
    status,
    json: Effect.succeed({ error: 'Internal server error' }),
    headers: {}
  })
}

/**
 * Async Test Helpers
 * 
 * Utilities for testing asynchronous code with proper cleanup.
 */
export const asyncHelpers = {
  /**
   * Wait for a specified number of milliseconds
   */
  delay: (ms: number) => new Promise(resolve => setTimeout(resolve, ms)),
  
  /**
   * Create a timeout promise that rejects after the specified time
   */
  timeout: (ms: number, message = 'Test timeout') =>
    new Promise<never>((_, reject) => 
      setTimeout(() => reject(new Error(message)), ms)
    ),
  
  /**
   * Race a promise against a timeout
   */
  withTimeout: <T>(promise: Promise<T>, ms: number) =>
    Promise.race([promise, asyncHelpers.timeout(ms)])
}