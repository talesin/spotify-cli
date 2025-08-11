/**
 * OAuth Service Simple Tests
 *
 * Basic tests for OAuth service functionality after refactoring.
 */

import { Effect } from 'effect'
import { OAuthService, TestOAuthServiceLayer } from '@src/OAuthService'

describe('OAuth Service - Simple Tests', () => {
  describe('OAuthService structure', () => {
    it('should provide all required methods', async () => {
      const testLayer = TestOAuthServiceLayer()

      await Effect.gen(function* () {
        const oauthService = yield* OAuthService

        expect(typeof oauthService.generatePKCEChallenge).toBe('function')
        expect(typeof oauthService.generateState).toBe('function')
        expect(typeof oauthService.buildAuthorizationUrl).toBe('function')
        expect(typeof oauthService.launchBrowser).toBe('function')
        expect(typeof oauthService.parseOAuthCallback).toBe('function')
        expect(typeof oauthService.createCallbackRouterAndWaiter).toBe('function')
        expect(typeof oauthService.completeFlow).toBe('function')
      }).pipe(Effect.provide(testLayer), Effect.runPromise)
    })

    it('should generate PKCE challenge', async () => {
      const testLayer = TestOAuthServiceLayer()

      await Effect.gen(function* () {
        const oauthService = yield* OAuthService
        const pkce = yield* oauthService.generatePKCEChallenge()

        expect(pkce.codeVerifier).toBeDefined()
        expect(pkce.codeChallenge).toBeDefined()
        expect(pkce.codeChallengeMethod).toBe('S256')
      }).pipe(Effect.provide(testLayer), Effect.runPromise)
    })

    it('should generate state', async () => {
      const testLayer = TestOAuthServiceLayer()

      await Effect.gen(function* () {
        const oauthService = yield* OAuthService
        const state = yield* oauthService.generateState()

        expect(typeof state).toBe('string')
        expect(state.length).toBeGreaterThan(0)
      }).pipe(Effect.provide(testLayer), Effect.runPromise)
    })
  })
})
