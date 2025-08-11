/**
 * Crypto Service Module
 *
 * This module provides a minimal Effect service wrapper around Node.js crypto
 * operations used in the OAuth flow. It abstracts cryptographic functions
 * to enable better testability and dependency injection patterns.
 *
 * Key Functions:
 * - Secure random byte generation for PKCE code verifiers
 * - SHA256 hashing for PKCE code challenges
 * - Hex-encoded random values for state parameters
 *
 * @module CryptoService
 */

import { Data, Effect, Layer } from 'effect'
import * as crypto from 'crypto'

/**
 * Crypto Service Error
 *
 * Represents errors that occur during cryptographic operations.
 * This includes random generation failures, hashing errors, and
 * other crypto-related issues.
 */
export class CryptoError extends Data.TaggedError('CryptoError')<{
  readonly message: string
  readonly cause?: unknown
}> {}

/**
 * Generate Random Bytes
 *
 * Creates cryptographically secure random bytes using Node.js crypto.
 * This is used for generating PKCE code verifiers and other secure
 * random values in the OAuth flow.
 *
 * @param size - Number of bytes to generate
 * @returns Effect that yields Buffer with random bytes or fails with CryptoError
 */
export const generateRandomBytes = (size: number) =>
  Effect.try({
    try: () => crypto.randomBytes(size),
    catch: (error) =>
      new CryptoError({
        message: `Failed to generate ${size} random bytes`,
        cause: error
      })
  })

/**
 * Create Hash
 *
 * Computes a cryptographic hash of the provided data using the specified
 * algorithm. Used for creating PKCE code challenges from code verifiers.
 *
 * @param algorithm - Hash algorithm (e.g., 'sha256')
 * @param data - Data to hash
 * @param encoding - Output encoding (e.g., 'base64url', 'hex')
 * @returns Effect that yields hashed string or fails with CryptoError
 */
export const createHash = (
  algorithm: string,
  data: string,
  encoding: crypto.BinaryToTextEncoding
) =>
  Effect.try({
    try: () => crypto.createHash(algorithm).update(data).digest(encoding),
    catch: (error) =>
      new CryptoError({
        message: `Failed to create ${algorithm} hash`,
        cause: error
      })
  })

/**
 * Generate Random Hex String
 *
 * Creates a hex-encoded random string of the specified byte length.
 * Commonly used for generating OAuth state parameters.
 *
 * @param bytes - Number of bytes to generate (output will be bytes * 2 hex chars)
 * @returns Effect that yields hex string or fails with CryptoError
 */
export const generateRandomHex = (bytes: number) =>
  Effect.gen(function* () {
    const buffer = yield* generateRandomBytes(bytes)
    return buffer.toString('hex')
  })

/**
 * Generate Base64URL String
 *
 * Creates a base64url-encoded random string of the specified byte length.
 * Used for PKCE code verifiers which must be base64url-encoded.
 *
 * @param bytes - Number of bytes to generate
 * @returns Effect that yields base64url string or fails with CryptoError
 */
export const generateBase64Url = (bytes: number) =>
  Effect.gen(function* () {
    const buffer = yield* generateRandomBytes(bytes)
    return buffer.toString('base64url')
  })

/**
 * Crypto Service
 *
 * Effect Service that provides cryptographic operations for OAuth flow.
 * Abstracts Node.js crypto module behind a testable service interface.
 */
export class CryptoService extends Effect.Service<CryptoService>()('CryptoService', {
  effect: Effect.succeed({
    /**
     * Generate cryptographically secure random bytes
     */
    generateRandomBytes,

    /**
     * Create cryptographic hash with specified algorithm and encoding
     */
    createHash,

    /**
     * Generate hex-encoded random string for state parameters
     */
    generateRandomHex,

    /**
     * Generate base64url-encoded random string for PKCE verifiers
     */
    generateBase64Url
  })
}) {}

/**
 * Test Crypto Service Layer
 *
 * Creates a test layer for CryptoService that allows mocking of crypto
 * operations during testing. Provides predictable values for deterministic tests.
 */
export const TestCryptoServiceLayer = (mockValues?: {
  randomBytes?: Buffer
  hashedValue?: string
  randomHex?: string
  base64Url?: string
}) =>
  Layer.succeed(
    CryptoService,
    CryptoService.of({
      _tag: 'CryptoService',
      generateRandomBytes: (_size: number) =>
        Effect.succeed(mockValues?.randomBytes ?? Buffer.from('mock-random-bytes')),
      createHash: (_algorithm: string, _data: string, _encoding: crypto.BinaryToTextEncoding) =>
        Effect.succeed(mockValues?.hashedValue ?? 'mock-hash-value'),
      generateRandomHex: (_bytes: number) =>
        Effect.succeed(mockValues?.randomHex ?? 'mock-hex-value'),
      generateBase64Url: (_bytes: number) =>
        Effect.succeed(mockValues?.base64Url ?? 'mock-base64url-value')
    })
  )
