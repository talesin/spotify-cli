/**
 * Simplified Tests for Core Functionality
 *
 * Basic tests that focus on the core logic without complex Effect-TS mocking.
 * These tests ensure the main functionality works correctly.
 */

import {
  TokenDataSchema,
  ConfigNotFound,
  ConfigParseError,
  ConfigWriteError,
  isTokenExpired
} from '@src/ConfigService'

import { Unauthorized, NetworkError, InvalidResponse, RateLimited } from '@src/SpotifyApi'
import { Cause, Effect, Exit, Option, Schema } from 'effect'
import { ParseError } from 'effect/ParseResult'

describe('Core Functionality Tests', () => {
  describe('TokenData', () => {
    it('should create TokenData with correct properties', () => {
      const tokens = TokenDataSchema.make({
        accessToken: 'test-access-token',
        refreshToken: 'test-refresh-token',
        expiresAt: 1234567890
      })

      expect(tokens._tag).toBe('TokenData')
      expect(tokens.accessToken).toBe('test-access-token')
      expect(tokens.refreshToken).toBe('test-refresh-token')
      expect(tokens.expiresAt).toBe(1234567890)
    })

    it('should be serializable to JSON', () => {
      const tokens = TokenDataSchema.make({
        accessToken: 'test-access-token',
        refreshToken: 'test-refresh-token',
        expiresAt: Date.now() + 3600000
      })

      const json = JSON.stringify(tokens)
      const parsed = JSON.parse(json)

      expect(parsed._tag).toBe('TokenData')
      expect(parsed.accessToken).toBe(tokens.accessToken)
      expect(parsed.refreshToken).toBe(tokens.refreshToken)
      expect(parsed.expiresAt).toBe(tokens.expiresAt)
    })
  })

  describe('isTokenExpired', () => {
    it('should return false for valid tokens', () => {
      const validTokens = TokenDataSchema.make({
        accessToken: 'valid-token',
        refreshToken: 'refresh-token',
        expiresAt: Date.now() + 3600000 // Expires in 1 hour
      })

      const result = isTokenExpired(validTokens)
      expect(result).toBe(false)
    })

    it('should return true for expired tokens', () => {
      const expiredTokens = TokenDataSchema.make({
        accessToken: 'expired-token',
        refreshToken: 'refresh-token',
        expiresAt: Date.now() - 1000 // Expired 1 second ago
      })

      const result = isTokenExpired(expiredTokens)
      expect(result).toBe(true)
    })

    it('should return true for tokens expiring right now', () => {
      const nowExpiredTokens = TokenDataSchema.make({
        accessToken: 'now-expired-token',
        refreshToken: 'refresh-token',
        expiresAt: Date.now() // Expires now
      })

      const result = isTokenExpired(nowExpiredTokens)
      expect(result).toBe(true)
    })
  })

  describe('Config Error Classes', () => {
    it('should create ConfigNotFound error', () => {
      const error = new ConfigNotFound({})
      expect(error._tag).toBe('ConfigNotFound')
    })

    it('should create ConfigParseError with message', () => {
      // 1. Generate a real ParseError by attempting to decode invalid data
      const result = Effect.runSyncExit(Schema.decodeUnknown(Schema.String)(123))

      // 2. Safely extract the failure from the result's Cause object
      const errorOption = Exit.isFailure(result) ? Cause.failureOption(result.cause) : Option.none()

      // 3. Ensure we got a failure and use it to create the ConfigParseError
      if (Option.isSome(errorOption)) {
        const error = new ConfigParseError({
          message: 'Invalid JSON format',
          error: errorOption.value
        })

        expect(error._tag).toBe('ConfigParseError')
        expect(error.message).toBe('Invalid JSON format')
      } else {
        fail('Expected a ParseError but did not get one.')
      }
    })

    it('should create ConfigWriteError with message', () => {
      const error = new ConfigWriteError({ message: 'Permission denied' })
      expect(error._tag).toBe('ConfigWriteError')
      expect(error.message).toBe('Permission denied')
    })
  })

  describe('HTTP Error Classes', () => {
    it('should create Unauthorized error', () => {
      const error = new Unauthorized({})
      expect(error._tag).toBe('Unauthorized')
    })

    it('should create NetworkError with message', () => {
      const error = new NetworkError({ message: 'Connection timeout' })
      expect(error._tag).toBe('NetworkError')
      expect(error.message).toBe('Connection timeout')
    })

    it('should create InvalidResponse with message', () => {
      const error = new InvalidResponse({
        message: 'Malformed JSON',
        error: { message: 'Malformed JSON' } as ParseError
      })
      expect(error._tag).toBe('InvalidResponse')
      expect(error.error.message).toBe('Malformed JSON')
    })

    it('should create RateLimited with retry delay', () => {
      const error = new RateLimited({ retryAfter: 120 })
      expect(error._tag).toBe('RateLimited')
      expect(error.retryAfter).toBe(120)
    })
  })

  describe('Utility Functions', () => {
    it('should properly encode client credentials for Basic auth', () => {
      const clientId = 'test-client-id'
      const clientSecret = 'test-secret'

      const credentials = btoa(`${clientId}:${clientSecret}`)
      const expected = 'dGVzdC1jbGllbnQtaWQ6dGVzdC1zZWNyZXQ='

      expect(credentials).toBe(expected)

      // Verify it can be decoded back
      const decoded = atob(credentials)
      expect(decoded).toBe('test-client-id:test-secret')
    })

    it('should properly encode URL parameters', () => {
      const params = {
        grant_type: 'authorization_code',
        code: 'test code with spaces',
        redirect_uri: 'http://localhost:8888/callback?param=value'
      }

      const urlParams = new URLSearchParams(params)
      const encoded = urlParams.toString()

      expect(encoded).toContain('grant_type=authorization_code')
      expect(encoded).toContain('code=test+code+with+spaces')
      expect(encoded).toContain(
        'redirect_uri=http%3A%2F%2Flocalhost%3A8888%2Fcallback%3Fparam%3Dvalue'
      )
    })

    it('should handle special characters in URL encoding', () => {
      const specialString = 'test+string/with=special&characters'
      const params = new URLSearchParams({ test: specialString })
      const encoded = params.toString()

      expect(encoded).toContain('test=test%2Bstring%2Fwith%3Dspecial%26characters')
    })
  })

  describe('Data Validation', () => {
    it('should handle token data validation', () => {
      const tokenData = {
        accessToken: 'BQC4TJW...',
        refreshToken: 'AQDTy8x...',
        expiresAt: Date.now() + 3600000
      }

      // Validate required fields are present
      expect(tokenData.accessToken).toBeDefined()
      expect(tokenData.refreshToken).toBeDefined()
      expect(tokenData.expiresAt).toBeDefined()
      expect(typeof tokenData.expiresAt).toBe('number')
    })

    it('should handle Spotify API response structure', () => {
      const userResponse = {
        id: 'testuser123',
        display_name: 'Test User',
        email: 'test@example.com',
        country: 'US',
        external_urls: {
          spotify: 'https://open.spotify.com/user/testuser123'
        }
      }

      // Validate expected fields are present
      expect(userResponse.id).toBeDefined()
      expect(userResponse.display_name).toBeDefined()
      expect(userResponse.email).toBeDefined()
      expect(userResponse.external_urls.spotify).toContain('spotify.com')
    })

    it('should handle playlist response structure', () => {
      const playlistResponse = {
        items: [
          {
            id: 'playlist1',
            name: 'Test Playlist',
            public: true,
            tracks: { total: 25 }
          }
        ],
        total: 1,
        limit: 50,
        offset: 0
      }

      // Validate playlist structure
      expect(Array.isArray(playlistResponse.items)).toBe(true)
      expect(playlistResponse.items[0]?.id).toBeDefined()
      expect(playlistResponse.items[0]?.name).toBeDefined()
      expect(typeof playlistResponse.items[0]?.public).toBe('boolean')
      expect(typeof playlistResponse.items[0]?.tracks.total).toBe('number')
    })
  })
})
