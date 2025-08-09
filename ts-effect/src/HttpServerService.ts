/**
 * HTTP Server Service Module
 *
 * This module provides an Effect service wrapper around HTTP server
 * capabilities for handling OAuth callbacks. It uses a simplified
 * approach that creates a basic Node.js HTTP server with Effect integration.
 *
 * @module HttpServerService
 */

import { Data, Effect, Layer } from 'effect'
import { HttpServerRequest, HttpServerResponse } from '@effect/platform'
import { HttpBodyError } from '@effect/platform/HttpBody'
import * as http from 'node:http'

/**
 * HTTP Server Error
 *
 * Represents errors that occur during HTTP server operations.
 */
export class HttpServerError extends Data.TaggedError('HttpServerError')<{
  readonly message: string
  readonly cause?: unknown
}> {}

/**
 * HTTP Request Handler
 *
 * Function type for handling HTTP requests.
 */
export type HttpRequestHandler = (
  request: HttpServerRequest.HttpServerRequest
) => Effect.Effect<HttpServerResponse.HttpServerResponse, HttpServerError | HttpBodyError>

/**
 * Server Configuration
 */
export interface ServerConfig {
  readonly port: number
  readonly host?: string | undefined
}

/**
 * Running Server Instance
 */
export interface RunningServer {
  readonly config: ServerConfig
  readonly stop: () => Effect.Effect<void, HttpServerError>
}

/**
 * Create a minimal HttpServerRequest from Node.js request
 */
const createServerRequest = (req: http.IncomingMessage): HttpServerRequest.HttpServerRequest => {
  // Create a minimal implementation that satisfies the interface
  // This is a simplified version for OAuth callback handling only
  const request = {
    url: req.url || '/',
    method: (req.method || 'GET') as any,
    headers: req.headers as Record<string, string | string[] | undefined>
  } as HttpServerRequest.HttpServerRequest
  
  return request
}

/**
 * Convert HttpServerResponse to Node.js response
 */
const sendResponse = (effectResponse: HttpServerResponse.HttpServerResponse, res: http.ServerResponse) => {
  // Set status
  if (effectResponse.status) {
    res.statusCode = effectResponse.status
  }
  
  // Set headers
  if (effectResponse.headers) {
    Object.entries(effectResponse.headers).forEach(([key, value]) => {
      if (value !== undefined) {
        res.setHeader(key, Array.isArray(value) ? value : String(value))
      }
    })
  }
  
  // Send body
  if (effectResponse.body) {
    // Convert body to string if it's a Uint8Array
    if (effectResponse.body instanceof Uint8Array) {
      res.end(Buffer.from(effectResponse.body))
    } else if (typeof effectResponse.body === 'string') {
      res.end(effectResponse.body)
    } else {
      res.end(String(effectResponse.body))
    }
  } else {
    res.end()
  }
}

/**
 * HTTP Server Service
 *
 * Effect Service that provides HTTP server operations for OAuth callback handling.
 */
export class HttpServerService extends Effect.Service<HttpServerService>()('HttpServerService', {
  effect: Effect.succeed({
    /**
     * Start HTTP Server
     *
     * Creates a Node.js HTTP server that handles requests using Effect-based handlers.
     */
    startServer: (config: ServerConfig, handler: HttpRequestHandler) =>
      Effect.gen(function* () {
        const server = http.createServer((req, res) => {
          const effectRequest = createServerRequest(req)
          
          // Run the Effect handler
          Effect.runPromise(
            handler(effectRequest).pipe(
              Effect.catchAll((error) => {
                console.error('Request handler error:', error)
                return Effect.succeed(
                  HttpServerResponse.text('Internal Server Error', { status: 500 })
                )
              })
            )
          ).then((effectResponse) => {
            sendResponse(effectResponse, res)
          }).catch((error) => {
            console.error('Effect execution error:', error)
            res.statusCode = 500
            res.end('Internal Server Error')
          })
        })
        
        // Start the server
        yield* Effect.async<void, HttpServerError>((resume) => {
          server.listen(config.port, config.host, () => {
            resume(Effect.succeed(void 0))
          })
          
          server.on('error', (error) => {
            resume(Effect.fail(new HttpServerError({
              message: `Failed to start server on port ${config.port}`,
              cause: error
            })))
          })
        })
        
        return {
          config,
          stop: () => Effect.async<void, HttpServerError>((resume) => {
            server.close((error) => {
              if (error) {
                resume(Effect.fail(new HttpServerError({
                  message: 'Failed to stop server',
                  cause: error
                })))
              } else {
                resume(Effect.succeed(void 0))
              }
            })
          })
        } satisfies RunningServer
      }),

    /**
     * Create Simple Text Handler
     */
    createTextHandler:
      (text: string): HttpRequestHandler =>
      (_request) =>
        HttpServerResponse.text(text),

    /**
     * Create JSON Handler
     */
    createJsonHandler:
      (data: unknown): HttpRequestHandler =>
      (_request) =>
        HttpServerResponse.json(data),

    /**
     * Create HTML Handler
     */
    createHtmlHandler:
      (html: string): HttpRequestHandler =>
      (_request) =>
        HttpServerResponse.html(html)
  }),
  dependencies: []
}) {}

/**
 * Test HTTP Server Service Layer
 */
export const TestHttpServerServiceLayer = (mockFunctions?: {
  startServer?: HttpServerService['startServer']
  createTextHandler?: HttpServerService['createTextHandler']
  createJsonHandler?: HttpServerService['createJsonHandler']
  createHtmlHandler?: HttpServerService['createHtmlHandler']
}) =>
  Layer.succeed(
    HttpServerService,
    HttpServerService.of({
      _tag: 'HttpServerService',
      startServer:
        mockFunctions?.startServer ??
        ((_config, _handler) =>
          Effect.succeed({
            config: { port: 3000 },
            stop: () => Effect.succeed(void 0)
          } satisfies RunningServer)),
      createTextHandler:
        mockFunctions?.createTextHandler ?? ((text) => (_request) => HttpServerResponse.text(text)),
      createJsonHandler:
        mockFunctions?.createJsonHandler ?? ((data) => (_request) => HttpServerResponse.json(data)),
      createHtmlHandler:
        mockFunctions?.createHtmlHandler ?? ((html) => (_request) => HttpServerResponse.html(html))
    })
  )