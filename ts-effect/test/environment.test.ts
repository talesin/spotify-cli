/**
 * Environment Configuration Tests
 *
 * Tests for environment variable loading and validation functionality.
 * Covers both success and failure scenarios for Spotify OAuth configuration.
 */

import { Effect } from 'effect'
import { 
  loadSpotifyConfig, 
  EnvironmentError,
  SpotifyConfigSchema,
  SPOTIFY_SCOPES,
  OAUTH_CONSTANTS 
} from '@src/environment'

describe('Environment Configuration', () => {
  // Store original environment variables
  const originalEnv = process.env

  beforeEach(() => {
    // Clear environment variables for clean test state
    delete process.env['SPOTIFY_CLIENT_ID']
    delete process.env['SPOTIFY_CLIENT_SECRET']
    delete process.env['SPOTIFY_REDIRECT_URI']
  })

  afterEach(() => {
    // Restore original environment
    process.env = originalEnv
  })

  describe('loadSpotifyConfig', () => {
    it('should load valid configuration from environment variables', async () => {
      process.env['SPOTIFY_CLIENT_ID'] = 'test-client-id'
      process.env['SPOTIFY_CLIENT_SECRET'] = 'test-client-secret'
      process.env['SPOTIFY_REDIRECT_URI'] = 'http://localhost:3000/callback'

      const result = await Effect.runPromise(loadSpotifyConfig)

      expect(result.clientId).toBe('test-client-id')
      expect(result.clientSecret).toBe('test-client-secret')
      expect(result.redirectUri).toBe('http://localhost:3000/callback')
    })

    it('should fail when SPOTIFY_CLIENT_ID is missing', async () => {
      process.env['SPOTIFY_CLIENT_SECRET'] = 'test-client-secret'
      process.env['SPOTIFY_REDIRECT_URI'] = 'http://localhost:3000/callback'

      const result = await Effect.runPromiseExit(loadSpotifyConfig)

      expect(result._tag).toBe('Failure')
      if (result._tag === 'Failure') {
        const error = result.cause._tag === 'Fail' ? result.cause.error : null
        expect(error).toBeInstanceOf(EnvironmentError)
        if (error instanceof EnvironmentError) {
          expect(error.message).toContain('SPOTIFY_CLIENT_ID')
          expect(error.missingVars).toContain('SPOTIFY_CLIENT_ID')
        }
      }
    })

    it('should fail when SPOTIFY_CLIENT_SECRET is missing', async () => {
      process.env['SPOTIFY_CLIENT_ID'] = 'test-client-id'
      process.env['SPOTIFY_REDIRECT_URI'] = 'http://localhost:3000/callback'

      const result = await Effect.runPromiseExit(loadSpotifyConfig)

      expect(result._tag).toBe('Failure')
      if (result._tag === 'Failure') {
        const error = result.cause._tag === 'Fail' ? result.cause.error : null
        expect(error).toBeInstanceOf(EnvironmentError)
        if (error instanceof EnvironmentError) {
          expect(error.message).toContain('SPOTIFY_CLIENT_SECRET')
          expect(error.missingVars).toContain('SPOTIFY_CLIENT_SECRET')
        }
      }
    })

    it('should fail when SPOTIFY_REDIRECT_URI is missing', async () => {
      process.env['SPOTIFY_CLIENT_ID'] = 'test-client-id'
      process.env['SPOTIFY_CLIENT_SECRET'] = 'test-client-secret'

      const result = await Effect.runPromiseExit(loadSpotifyConfig)

      expect(result._tag).toBe('Failure')
      if (result._tag === 'Failure') {
        const error = result.cause._tag === 'Fail' ? result.cause.error : null
        expect(error).toBeInstanceOf(EnvironmentError)
        if (error instanceof EnvironmentError) {
          expect(error.message).toContain('SPOTIFY_REDIRECT_URI')
          expect(error.missingVars).toContain('SPOTIFY_REDIRECT_URI')
        }
      }
    })

    it('should fail when multiple environment variables are missing', async () => {
      process.env['SPOTIFY_CLIENT_ID'] = 'test-client-id'
      // Missing SPOTIFY_CLIENT_SECRET and SPOTIFY_REDIRECT_URI

      const result = await Effect.runPromiseExit(loadSpotifyConfig)

      expect(result._tag).toBe('Failure')
      if (result._tag === 'Failure') {
        const error = result.cause._tag === 'Fail' ? result.cause.error : null
        expect(error).toBeInstanceOf(EnvironmentError)
        if (error instanceof EnvironmentError) {
          expect(error.missingVars).toHaveLength(2)
          expect(error.missingVars).toContain('SPOTIFY_CLIENT_SECRET')
          expect(error.missingVars).toContain('SPOTIFY_REDIRECT_URI')
        }
      }
    })

    it('should fail when environment variables are empty strings', async () => {
      process.env['SPOTIFY_CLIENT_ID'] = ''
      process.env['SPOTIFY_CLIENT_SECRET'] = 'test-client-secret'
      process.env['SPOTIFY_REDIRECT_URI'] = 'http://localhost:3000/callback'

      const result = await Effect.runPromiseExit(loadSpotifyConfig)

      expect(result._tag).toBe('Failure')
      if (result._tag === 'Failure') {
        const error = result.cause._tag === 'Fail' ? result.cause.error : null
        expect(error).toBeInstanceOf(EnvironmentError)
        if (error instanceof EnvironmentError) {
          // Empty string is treated as missing variable
          expect(error.message).toContain('SPOTIFY_CLIENT_ID')
          expect(error.missingVars).toContain('SPOTIFY_CLIENT_ID')
        }
      }
    })

    it('should provide helpful error message with dashboard URL', async () => {
      // No environment variables set

      const result = await Effect.runPromiseExit(loadSpotifyConfig)

      expect(result._tag).toBe('Failure')
      if (result._tag === 'Failure') {
        const error = result.cause._tag === 'Fail' ? result.cause.error : null
        expect(error).toBeInstanceOf(EnvironmentError)
        if (error instanceof EnvironmentError) {
          expect(error.message).toContain('https://developer.spotify.com/dashboard')
          expect(error.message).toContain('SPOTIFY_CLIENT_ID')
          expect(error.message).toContain('SPOTIFY_CLIENT_SECRET')
          expect(error.message).toContain('SPOTIFY_REDIRECT_URI')
        }
      }
    })
  })

  describe('SpotifyConfigSchema', () => {
    it('should validate correct configuration structure', () => {
      const validConfig = {
        clientId: 'test-client-id',
        clientSecret: 'test-client-secret',
        redirectUri: 'http://localhost:3000/callback'
      }

      const result = SpotifyConfigSchema.make(validConfig)

      expect(result.clientId).toBe('test-client-id')
      expect(result.clientSecret).toBe('test-client-secret')
      expect(result.redirectUri).toBe('http://localhost:3000/callback')
    })

    it('should reject configuration with empty strings', () => {
      const invalidConfig = {
        clientId: '',
        clientSecret: 'test-client-secret',
        redirectUri: 'http://localhost:3000/callback'
      }

      expect(() => SpotifyConfigSchema.make(invalidConfig)).toThrow()
    })
  })

  describe('SPOTIFY_SCOPES', () => {
    it('should contain required OAuth scopes', () => {
      expect(SPOTIFY_SCOPES).toContain('user-read-private')
      expect(SPOTIFY_SCOPES).toContain('user-read-email')
      expect(SPOTIFY_SCOPES).toContain('playlist-read-private')
      expect(SPOTIFY_SCOPES).toContain('playlist-read-collaborative')
    })

    it('should have the correct number of scopes', () => {
      expect(SPOTIFY_SCOPES).toHaveLength(4)
    })
  })

  describe('OAUTH_CONSTANTS', () => {
    it('should have correct Spotify OAuth URLs', () => {
      expect(OAUTH_CONSTANTS.AUTHORIZATION_URL).toBe('https://accounts.spotify.com/authorize')
      expect(OAUTH_CONSTANTS.TOKEN_URL).toBe('https://accounts.spotify.com/api/token')
    })

    it('should have correct OAuth parameters', () => {
      expect(OAUTH_CONSTANTS.RESPONSE_TYPE).toBe('code')
      expect(OAUTH_CONSTANTS.DEFAULT_PORT).toBe(3000)
      expect(OAUTH_CONSTANTS.CALLBACK_PATH).toBe('/callback')
    })
  })

  describe('EnvironmentError', () => {
    it('should create error with message and missing variables', () => {
      const error = new EnvironmentError({
        message: 'Test error message',
        missingVars: ['SPOTIFY_CLIENT_ID', 'SPOTIFY_CLIENT_SECRET']
      })

      expect(error._tag).toBe('EnvironmentError')
      expect(error.message).toBe('Test error message')
      expect(error.missingVars).toEqual(['SPOTIFY_CLIENT_ID', 'SPOTIFY_CLIENT_SECRET'])
    })

    it('should create error with just message', () => {
      const error = new EnvironmentError({
        message: 'Configuration validation failed'
      })

      expect(error._tag).toBe('EnvironmentError')
      expect(error.message).toBe('Configuration validation failed')
      expect(error.missingVars).toBeUndefined()
    })
  })
})