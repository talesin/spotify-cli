/**
 * Crypto Service Tests
 *
 * Comprehensive tests for the CryptoService module covering secure random
 * generation, hashing operations, and error handling. Tests follow Effect-TS
 * patterns and ensure cryptographic operations work correctly.
 */

import { Effect } from 'effect'
import * as crypto from 'crypto'
import { CryptoService, CryptoError, TestCryptoServiceLayer } from '@src/CryptoService'

describe('CryptoService', () => {
  describe('CryptoService.Default (Real Implementation)', () => {
    describe('generateRandomBytes', () => {
      it('should generate random bytes of specified size', async () => {
        await Effect.gen(function* () {
          const cryptoService = yield* CryptoService
          const result = yield* cryptoService.generateRandomBytes(16)

          expect(Buffer.isBuffer(result)).toBe(true)
          expect(result.length).toBe(16)
        }).pipe(Effect.provide(CryptoService.Default), Effect.runPromise)
      })

      it('should generate different values on each call', async () => {
        await Effect.gen(function* () {
          const cryptoService = yield* CryptoService

          const result1 = yield* cryptoService.generateRandomBytes(16)
          const result2 = yield* cryptoService.generateRandomBytes(16)

          expect(result1).not.toEqual(result2)
        }).pipe(Effect.provide(CryptoService.Default), Effect.runPromise)
      })

      it('should generate different sizes correctly', async () => {
        await Effect.gen(function* () {
          const cryptoService = yield* CryptoService

          const small = yield* cryptoService.generateRandomBytes(8)
          const large = yield* cryptoService.generateRandomBytes(64)

          expect(small.length).toBe(8)
          expect(large.length).toBe(64)
        }).pipe(Effect.provide(CryptoService.Default), Effect.runPromise)
      })
    })

    describe('createHash', () => {
      it('should create SHA256 hash with base64url encoding', async () => {
        await Effect.gen(function* () {
          const cryptoService = yield* CryptoService
          const testData = 'test-data-for-hashing'

          const result = yield* cryptoService.createHash('sha256', testData, 'base64url')

          // Verify against Node.js crypto directly
          const expected = crypto.createHash('sha256').update(testData).digest('base64url')
          expect(result).toBe(expected)
          expect(result).toMatch(/^[A-Za-z0-9_-]+$/) // base64url pattern
        }).pipe(Effect.provide(CryptoService.Default), Effect.runPromise)
      })

      it('should create SHA256 hash with hex encoding', async () => {
        await Effect.gen(function* () {
          const cryptoService = yield* CryptoService
          const testData = 'another-test-string'

          const result = yield* cryptoService.createHash('sha256', testData, 'hex')

          // Verify against Node.js crypto directly
          const expected = crypto.createHash('sha256').update(testData).digest('hex')
          expect(result).toBe(expected)
          expect(result).toMatch(/^[a-f0-9]+$/) // hex pattern
          expect(result.length).toBe(64) // SHA256 hex is 64 characters
        }).pipe(Effect.provide(CryptoService.Default), Effect.runPromise)
      })

      it('should handle different algorithms', async () => {
        await Effect.gen(function* () {
          const cryptoService = yield* CryptoService
          const testData = 'test-data'

          const sha1Result = yield* cryptoService.createHash('sha1', testData, 'hex')
          const sha256Result = yield* cryptoService.createHash('sha256', testData, 'hex')

          expect(sha1Result.length).toBe(40) // SHA1 hex is 40 characters
          expect(sha256Result.length).toBe(64) // SHA256 hex is 64 characters
          expect(sha1Result).not.toBe(sha256Result)
        }).pipe(Effect.provide(CryptoService.Default), Effect.runPromise)
      })

      it('should produce consistent results for same input', async () => {
        await Effect.gen(function* () {
          const cryptoService = yield* CryptoService
          const testData = 'consistent-input'

          const result1 = yield* cryptoService.createHash('sha256', testData, 'base64url')
          const result2 = yield* cryptoService.createHash('sha256', testData, 'base64url')

          expect(result1).toBe(result2)
        }).pipe(Effect.provide(CryptoService.Default), Effect.runPromise)
      })
    })

    describe('generateRandomHex', () => {
      it('should generate hex string of correct length', async () => {
        await Effect.gen(function* () {
          const cryptoService = yield* CryptoService
          const result = yield* cryptoService.generateRandomHex(16)

          expect(typeof result).toBe('string')
          expect(result.length).toBe(32) // 16 bytes = 32 hex characters
          expect(result).toMatch(/^[a-f0-9]+$/) // hex pattern
        }).pipe(Effect.provide(CryptoService.Default), Effect.runPromise)
      })

      it('should generate different values on each call', async () => {
        await Effect.gen(function* () {
          const cryptoService = yield* CryptoService

          const result1 = yield* cryptoService.generateRandomHex(8)
          const result2 = yield* cryptoService.generateRandomHex(8)

          expect(result1).not.toBe(result2)
          expect(result1.length).toBe(16) // 8 bytes = 16 hex chars
          expect(result2.length).toBe(16)
        }).pipe(Effect.provide(CryptoService.Default), Effect.runPromise)
      })

      it('should handle different byte sizes', async () => {
        await Effect.gen(function* () {
          const cryptoService = yield* CryptoService

          const small = yield* cryptoService.generateRandomHex(4)
          const large = yield* cryptoService.generateRandomHex(32)

          expect(small.length).toBe(8) // 4 bytes = 8 hex chars
          expect(large.length).toBe(64) // 32 bytes = 64 hex chars
        }).pipe(Effect.provide(CryptoService.Default), Effect.runPromise)
      })
    })

    describe('generateBase64Url', () => {
      it('should generate base64url string', async () => {
        await Effect.gen(function* () {
          const cryptoService = yield* CryptoService
          const result = yield* cryptoService.generateBase64Url(16)

          expect(typeof result).toBe('string')
          expect(result).toMatch(/^[A-Za-z0-9_-]+$/) // base64url pattern (no +/=)
          expect(result.length).toBeGreaterThan(0)
        }).pipe(Effect.provide(CryptoService.Default), Effect.runPromise)
      })

      it('should generate different values on each call', async () => {
        await Effect.gen(function* () {
          const cryptoService = yield* CryptoService

          const result1 = yield* cryptoService.generateBase64Url(24)
          const result2 = yield* cryptoService.generateBase64Url(24)

          expect(result1).not.toBe(result2)
          expect(result1).toMatch(/^[A-Za-z0-9_-]+$/)
          expect(result2).toMatch(/^[A-Za-z0-9_-]+$/)
        }).pipe(Effect.provide(CryptoService.Default), Effect.runPromise)
      })

      it('should handle different byte sizes', async () => {
        await Effect.gen(function* () {
          const cryptoService = yield* CryptoService

          const small = yield* cryptoService.generateBase64Url(8)
          const large = yield* cryptoService.generateBase64Url(48)

          expect(small.length).toBeGreaterThan(0)
          expect(large.length).toBeGreaterThan(small.length)
        }).pipe(Effect.provide(CryptoService.Default), Effect.runPromise)
      })

      it('should be suitable for PKCE code verifiers', async () => {
        await Effect.gen(function* () {
          const cryptoService = yield* CryptoService
          // Generate 96 bytes for 128-character code verifier (as used in OAuth)
          const result = yield* cryptoService.generateBase64Url(96)

          // Should be long enough to slice to 128 characters for PKCE
          expect(result.length).toBeGreaterThanOrEqual(128)
          const codeVerifier = result.slice(0, 128)
          expect(codeVerifier.length).toBe(128)
          expect(codeVerifier).toMatch(/^[A-Za-z0-9_-]+$/)
        }).pipe(Effect.provide(CryptoService.Default), Effect.runPromise)
      })
    })
  })

  describe('TestCryptoServiceLayer (Mock Implementation)', () => {
    it('should use default mock values when no custom values provided', async () => {
      const testLayer = TestCryptoServiceLayer()

      await Effect.gen(function* () {
        const cryptoService = yield* CryptoService

        const randomBytes = yield* cryptoService.generateRandomBytes(16)
        const hash = yield* cryptoService.createHash('sha256', 'test', 'base64url')
        const hex = yield* cryptoService.generateRandomHex(8)
        const base64url = yield* cryptoService.generateBase64Url(12)

        expect(randomBytes).toEqual(Buffer.from('mock-random-bytes'))
        expect(hash).toBe('mock-hash-value')
        expect(hex).toBe('mock-hex-value')
        expect(base64url).toBe('mock-base64url-value')
      }).pipe(Effect.provide(testLayer), Effect.runPromise)
    })

    it('should use custom mock values when provided', async () => {
      const customMockValues = {
        randomBytes: Buffer.from('custom-bytes'),
        hashedValue: 'custom-hash',
        randomHex: 'custom-hex',
        base64Url: 'custom-base64url'
      }
      const testLayer = TestCryptoServiceLayer(customMockValues)

      await Effect.gen(function* () {
        const cryptoService = yield* CryptoService

        const randomBytes = yield* cryptoService.generateRandomBytes(20)
        const hash = yield* cryptoService.createHash('sha1', 'input', 'hex')
        const hex = yield* cryptoService.generateRandomHex(4)
        const base64url = yield* cryptoService.generateBase64Url(8)

        expect(randomBytes).toEqual(Buffer.from('custom-bytes'))
        expect(hash).toBe('custom-hash')
        expect(hex).toBe('custom-hex')
        expect(base64url).toBe('custom-base64url')
      }).pipe(Effect.provide(testLayer), Effect.runPromise)
    })

    it('should ignore input parameters and return mock values', async () => {
      const testLayer = TestCryptoServiceLayer({
        randomBytes: Buffer.from('fixed-mock-bytes'),
        hashedValue: 'fixed-mock-hash'
      })

      await Effect.gen(function* () {
        const cryptoService = yield* CryptoService

        // Different inputs should still return same mock values
        const bytes1 = yield* cryptoService.generateRandomBytes(1)
        const bytes2 = yield* cryptoService.generateRandomBytes(100)
        const hash1 = yield* cryptoService.createHash('sha256', 'input1', 'hex')
        const hash2 = yield* cryptoService.createHash('md5', 'input2', 'base64')

        expect(bytes1).toEqual(Buffer.from('fixed-mock-bytes'))
        expect(bytes2).toEqual(Buffer.from('fixed-mock-bytes'))
        expect(hash1).toBe('fixed-mock-hash')
        expect(hash2).toBe('fixed-mock-hash')
      }).pipe(Effect.provide(testLayer), Effect.runPromise)
    })
  })

  describe('CryptoError', () => {
    it('should create error with message and cause', () => {
      const originalError = new Error('Original crypto error')
      const cryptoError = new CryptoError({
        message: 'Crypto operation failed',
        cause: originalError
      })

      expect(cryptoError._tag).toBe('CryptoError')
      expect(cryptoError.message).toBe('Crypto operation failed')
      expect(cryptoError.cause).toBe(originalError)
    })

    it('should create error with just message', () => {
      const cryptoError = new CryptoError({
        message: 'Simple crypto error'
      })

      expect(cryptoError._tag).toBe('CryptoError')
      expect(cryptoError.message).toBe('Simple crypto error')
      expect(cryptoError.cause).toBeUndefined()
    })
  })

  describe('Integration with OAuth PKCE Flow', () => {
    it('should support complete PKCE challenge generation', async () => {
      await Effect.gen(function* () {
        const cryptoService = yield* CryptoService

        // Generate code verifier (128 characters from 96 bytes base64url)
        const base64UrlString = yield* cryptoService.generateBase64Url(96)
        const codeVerifier = base64UrlString.slice(0, 128)

        // Generate code challenge (SHA256 hash of verifier as base64url)
        const codeChallenge = yield* cryptoService.createHash('sha256', codeVerifier, 'base64url')

        // Verify PKCE requirements
        expect(codeVerifier.length).toBe(128)
        expect(codeVerifier).toMatch(/^[A-Za-z0-9._~-]+$/) // PKCE allowed chars
        expect(codeChallenge).toMatch(/^[A-Za-z0-9_-]+$/) // base64url encoded
        expect(codeChallenge.length).toBeGreaterThan(0)

        // Verify challenge is correct hash of verifier
        const expectedChallenge = crypto
          .createHash('sha256')
          .update(codeVerifier)
          .digest('base64url')
        expect(codeChallenge).toBe(expectedChallenge)
      }).pipe(Effect.provide(CryptoService.Default), Effect.runPromise)
    })

    it('should support OAuth state parameter generation', async () => {
      await Effect.gen(function* () {
        const cryptoService = yield* CryptoService

        // Generate 16-byte hex string for OAuth state parameter
        const state = yield* cryptoService.generateRandomHex(16)

        expect(state.length).toBe(32) // 16 bytes = 32 hex chars
        expect(state).toMatch(/^[a-f0-9]+$/)

        // Should be suitable for OAuth state parameter
        const urlParams = new URLSearchParams({ state })
        expect(urlParams.get('state')).toBe(state)
      }).pipe(Effect.provide(CryptoService.Default), Effect.runPromise)
    })
  })
})
