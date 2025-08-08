/**
 * Browser Service Module
 *
 * This module provides a minimal Effect service wrapper around browser
 * launching functionality. It abstracts the browser opening logic to
 * enable better testability and dependency injection patterns.
 *
 * Key Functions:
 * - Cross-platform browser launching for OAuth URLs
 * - Error handling for browser launch failures
 * - Testable interface for mocking browser operations
 *
 * @module BrowserService
 */

import { Data, Effect, Layer } from 'effect'
import open from 'open'
import { ChildProcess } from 'child_process'

/**
 * Browser Service Error
 *
 * Represents errors that occur during browser operations.
 * This includes browser launch failures, missing browsers,
 * and other browser-related issues.
 */
export class BrowserError extends Data.TaggedError('BrowserError')<{
  readonly message: string
  readonly cause?: unknown
}> {}

/**
 * Launch Browser Implementation
 *
 * Opens the user's default browser to the specified URL. This is used
 * to initiate the OAuth flow by directing users to Spotify's authorization page.
 * Handles cross-platform browser launching and provides error information.
 *
 * @param url - URL to open in the browser
 * @returns Effect that succeeds when browser launches or fails with BrowserError
 */
export const launchBrowserImpl = (url: string) =>
  Effect.tryPromise({
    try: () => open(url),
    catch: (error) =>
      new BrowserError({
        message: `Failed to launch browser for URL: ${url}`,
        cause: error
      })
  })

/**
 * Browser Service
 *
 * Effect Service that provides browser operations for OAuth flow.
 * Abstracts browser launching behind a testable service interface.
 */
export class BrowserService extends Effect.Service<BrowserService>()('BrowserService', {
  effect: Effect.succeed({
    /**
     * Launch the user's default browser to the specified URL
     */
    launch: launchBrowserImpl
  })
}) {}

/**
 * Test Browser Service Layer
 *
 * Creates a test layer for BrowserService that allows mocking of browser
 * operations during testing. Prevents actual browser launches during tests
 * while maintaining the same interface.
 */
export const TestBrowserServiceLayer = (mockBehavior?: {
  shouldFail?: boolean
  errorMessage?: string
}) =>
  Layer.succeed(
    BrowserService,
    BrowserService.of({
      _tag: 'BrowserService',
      launch: (url: string) =>
        mockBehavior?.shouldFail === true
          ? Effect.fail(
              new BrowserError({
                message: mockBehavior.errorMessage ?? `Mock browser launch failed for ${url}`
              })
            )
          : Effect.succeed(undefined as unknown as ChildProcess)
    })
  )
