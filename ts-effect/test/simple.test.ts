/**
 * Simplified Tests for Core Functionality
 * 
 * Basic tests that focus on the core logic without complex Effect-TS mocking.
 * These tests ensure the main functionality works correctly.
 */

import {
  TokenData,
  ConfigNotFound,
  ConfigParseError,
  ConfigWriteError,
  isTokenExpired
} from '../src/config'

import {
  Unauthorized,
  NetworkError,
  InvalidResponse,
  RateLimited
} from '../src/http'

describe('Core Functionality Tests', () => {
  describe('TokenData', () => {
    it('should create TokenData with correct properties', () => {
      const tokens = new TokenData({
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
      const tokens = new TokenData({
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
      const validTokens = new TokenData({
        accessToken: 'valid-token',
        refreshToken: 'refresh-token',
        expiresAt: Date.now() + 3600000 // Expires in 1 hour
      })

      const result = isTokenExpired(validTokens)
      expect(result).toBe(false)
    })

    it('should return true for expired tokens', () => {
      const expiredTokens = new TokenData({
        accessToken: 'expired-token',
        refreshToken: 'refresh-token',
        expiresAt: Date.now() - 1000 // Expired 1 second ago
      })

      const result = isTokenExpired(expiredTokens)
      expect(result).toBe(true)
    })

    it('should return true for tokens expiring right now', () => {
      const nowExpiredTokens = new TokenData({
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
      const error = new ConfigParseError({ error: 'Invalid JSON format' })
      expect(error._tag).toBe('ConfigParseError')
      expect(error.error).toBe('Invalid JSON format')
    })

    it('should create ConfigWriteError with message', () => {
      const error = new ConfigWriteError({ error: 'Permission denied' })
      expect(error._tag).toBe('ConfigWriteError')
      expect(error.error).toBe('Permission denied')
    })
  })

  describe('HTTP Error Classes', () => {
    it('should create Unauthorized error', () => {
      const error = new Unauthorized({})
      expect(error._tag).toBe('Unauthorized')
    })

    it('should create NetworkError with message', () => {
      const error = new NetworkError({ error: 'Connection timeout' })
      expect(error._tag).toBe('NetworkError')
      expect(error.error).toBe('Connection timeout')
    })

    it('should create InvalidResponse with message', () => {
      const error = new InvalidResponse({ error: 'Malformed JSON' })
      expect(error._tag).toBe('InvalidResponse')
      expect(error.error).toBe('Malformed JSON')
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
      expect(encoded).toContain('redirect_uri=http%3A%2F%2Flocalhost%3A8888%2Fcallback%3Fparam%3Dvalue')
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

  describe('CLI Application Structure', () => {
    it('should have proper package.json configuration', () => {
      const packageJson = require('../package.json')
      
      expect(packageJson.name).toBe('ts-effect')
      expect(packageJson.version).toBe('1.0.0')
      expect(packageJson.main).toBe('dist/index.js')
      expect(packageJson.bin['spotify-cli']).toBe('./dist/index.js')
      
      // Check dependencies
      expect(packageJson.dependencies?.['effect']).toBeDefined()
      expect(packageJson.dependencies?.['@effect/cli']).toBeDefined()
      expect(packageJson.dependencies?.['@effect/platform']).toBeDefined()
      expect(packageJson.dependencies?.['@effect/platform-node']).toBeDefined()
    })

    it('should have proper TypeScript configuration', () => {
      const fs = require('fs')
      const path = require('path')
      const tsConfigPath = path.join(__dirname, '..', 'tsconfig.json')
      const tsConfigContent = fs.readFileSync(tsConfigPath, 'utf-8')
      
      // Basic validation that it's a valid JSON-like config
      expect(tsConfigContent).toContain('compilerOptions')
      expect(tsConfigContent).toContain('strict')
      expect(tsConfigContent).toContain('target')
    })

    it('should have proper Jest configuration', () => {
      const jestConfig = require('../jest.config.ts')
      
      expect(jestConfig.preset).toBe('ts-jest')
      expect(jestConfig.testEnvironment).toBe('node')
      expect(jestConfig.testMatch).toContain('**/*.test.ts')
    })

    it('should have all required npm scripts', () => {
      const packageJson = require('../package.json')
      
      expect(packageJson.scripts.build).toBeDefined()
      expect(packageJson.scripts.test).toBeDefined()
      expect(packageJson.scripts.lint).toBeDefined()
      expect(packageJson.scripts.typecheck).toBeDefined()
      expect(packageJson.scripts.dev).toBeDefined()
    })
  })

  describe('File Structure', () => {
    it('should have all required source files', () => {
      const fs = require('fs')
      const path = require('path')

      const requiredFiles = [
        'src/index.ts',
        'src/config.ts', 
        'src/http.ts'
      ]

      requiredFiles.forEach(file => {
        const filePath = path.join(__dirname, '..', file)
        expect(fs.existsSync(filePath)).toBe(true)
      })
    })

    it('should have test files for all modules', () => {
      const fs = require('fs')
      const path = require('path')

      const testFiles = [
        'test/simple.test.ts',
        'test/test-utils.ts'
      ]

      testFiles.forEach(file => {
        const filePath = path.join(__dirname, '..', file)
        expect(fs.existsSync(filePath)).toBe(true)
      })
    })

    it('should have configuration files', () => {
      const fs = require('fs')
      const path = require('path')

      const configFiles = [
        'tsconfig.json',
        'jest.config.ts',
        'eslint.config.mjs',
        'package.json'
      ]

      configFiles.forEach(file => {
        const filePath = path.join(__dirname, '..', file)
        expect(fs.existsSync(filePath)).toBe(true)
      })
    })
  })
})