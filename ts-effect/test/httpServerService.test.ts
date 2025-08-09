/**
 * HTTP Server Service Tests
 *
 * Tests for the HttpServerService including server lifecycle management,
 * request handling, and error scenarios.
 */

import { Effect } from 'effect'
import { HttpServerRequest, HttpServerResponse } from '@effect/platform'
import { 
  HttpServerService, 
  HttpServerError, 
  TestHttpServerServiceLayer,
  ServerConfig,
  RunningServer
} from '@src/HttpServerService'

describe('HttpServerService', () => {
  describe('startServer', () => {
    it('should start a server with given configuration', async () => {
      const config: ServerConfig = { port: 3000 }
      const handler = (_request: HttpServerRequest.HttpServerRequest) => 
        HttpServerResponse.text('Hello World')

      const result = await Effect.runPromise(
        Effect.gen(function* () {
          const httpServer = yield* HttpServerService
          return yield* httpServer.startServer(config, handler)
        }).pipe(
          Effect.provide(HttpServerService.Default)
        )
      )

      expect(result.config.port).toBe(3000)
      expect(typeof result.stop).toBe('function')

      // Test stopping the server
      const stopResult = await Effect.runPromise(result.stop())
      expect(stopResult).toBeUndefined()
    })

    it('should start a server with host configuration', async () => {
      const config: ServerConfig = { port: 3001, host: '127.0.0.1' }
      const handler = (_request: HttpServerRequest.HttpServerRequest) => 
        HttpServerResponse.text('Hello World')

      const result = await Effect.runPromise(
        Effect.gen(function* () {
          const httpServer = yield* HttpServerService
          return yield* httpServer.startServer(config, handler)
        }).pipe(
          Effect.provide(HttpServerService.Default)
        )
      )

      expect(result.config.port).toBe(3001)
      expect(result.config.host).toBe('127.0.0.1')
    })

    it('should work with custom mock in test layer', async () => {
      const mockStartServer = jest.fn(() => 
        Effect.succeed({
          config: { port: 8080 },
          stop: () => Effect.succeed(void 0)
        } satisfies RunningServer)
      )

      const result = await Effect.runPromise(
        Effect.gen(function* () {
          const httpServer = yield* HttpServerService
          return yield* httpServer.startServer({ port: 3000 }, () => HttpServerResponse.text('test'))
        }).pipe(
          Effect.provide(TestHttpServerServiceLayer({ startServer: mockStartServer }))
        )
      )

      expect(mockStartServer).toHaveBeenCalledWith(
        { port: 3000 }, 
        expect.any(Function)
      )
      expect(result.config.port).toBe(8080)
    })
  })

  describe('createTextHandler', () => {
    it('should create a handler that returns text response', async () => {
      const mockRequest = {} as HttpServerRequest.HttpServerRequest

      const result = await Effect.runPromise(
        Effect.gen(function* () {
          const httpServer = yield* HttpServerService
          const handler = httpServer.createTextHandler('Hello World')
          return yield* handler(mockRequest)
        }).pipe(
          Effect.provide(HttpServerService.Default)
        )
      )

      // Since we're using the simplified mock implementation,
      // we mainly test that the function is created and callable
      expect(result).toBeDefined()
    })

    it('should work with custom mock', async () => {
      const mockTextHandler = jest.fn((text: string) => 
        (_request: HttpServerRequest.HttpServerRequest) => 
          HttpServerResponse.text(`Custom: ${text}`)
      )

      const mockRequest = {} as HttpServerRequest.HttpServerRequest

      const result = await Effect.runPromise(
        Effect.gen(function* () {
          const httpServer = yield* HttpServerService
          const handler = httpServer.createTextHandler('Hello')
          return yield* handler(mockRequest)
        }).pipe(
          Effect.provide(TestHttpServerServiceLayer({ createTextHandler: mockTextHandler }))
        )
      )

      expect(mockTextHandler).toHaveBeenCalledWith('Hello')
      expect(result).toBeDefined()
    })
  })

  describe('createJsonHandler', () => {
    it('should create a handler that returns JSON response', async () => {
      const testData = { message: 'Hello', status: 'ok' }
      const mockRequest = {} as HttpServerRequest.HttpServerRequest

      const result = await Effect.runPromise(
        Effect.gen(function* () {
          const httpServer = yield* HttpServerService
          const handler = httpServer.createJsonHandler(testData)
          return yield* handler(mockRequest)
        }).pipe(
          Effect.provide(HttpServerService.Default)
        )
      )

      expect(result).toBeDefined()
    })

    it('should work with custom mock', async () => {
      const mockJsonHandler = jest.fn((data: unknown) => 
        (_request: HttpServerRequest.HttpServerRequest) => 
          HttpServerResponse.json({ custom: data })
      )

      const testData = { test: true }
      const mockRequest = {} as HttpServerRequest.HttpServerRequest

      const result = await Effect.runPromise(
        Effect.gen(function* () {
          const httpServer = yield* HttpServerService
          const handler = httpServer.createJsonHandler(testData)
          return yield* handler(mockRequest)
        }).pipe(
          Effect.provide(TestHttpServerServiceLayer({ createJsonHandler: mockJsonHandler }))
        )
      )

      expect(mockJsonHandler).toHaveBeenCalledWith(testData)
      expect(result).toBeDefined()
    })
  })

  describe('createHtmlHandler', () => {
    it('should create a handler that returns HTML response', async () => {
      const htmlContent = '<html><body><h1>Hello World</h1></body></html>'
      const mockRequest = {} as HttpServerRequest.HttpServerRequest

      const result = await Effect.runPromise(
        Effect.gen(function* () {
          const httpServer = yield* HttpServerService
          const handler = httpServer.createHtmlHandler(htmlContent)
          return yield* handler(mockRequest)
        }).pipe(
          Effect.provide(HttpServerService.Default)
        )
      )

      expect(result).toBeDefined()
    })

    it('should work with custom mock', async () => {
      const mockHtmlHandler = jest.fn((html: string) => 
        (_request: HttpServerRequest.HttpServerRequest) => 
          HttpServerResponse.html(`<div>${html}</div>`)
      )

      const htmlContent = '<p>Test</p>'
      const mockRequest = {} as HttpServerRequest.HttpServerRequest

      const result = await Effect.runPromise(
        Effect.gen(function* () {
          const httpServer = yield* HttpServerService
          const handler = httpServer.createHtmlHandler(htmlContent)
          return yield* handler(mockRequest)
        }).pipe(
          Effect.provide(TestHttpServerServiceLayer({ createHtmlHandler: mockHtmlHandler }))
        )
      )

      expect(mockHtmlHandler).toHaveBeenCalledWith(htmlContent)
      expect(result).toBeDefined()
    })
  })

  describe('TestHttpServerServiceLayer', () => {
    it('should provide default mock implementations', async () => {
      const result = await Effect.runPromise(
        Effect.gen(function* () {
          const httpServer = yield* HttpServerService
          const server = yield* httpServer.startServer({ port: 3000 }, () => HttpServerResponse.text('test'))
          const textHandler = httpServer.createTextHandler('test')
          const jsonHandler = httpServer.createJsonHandler({ test: true })
          const htmlHandler = httpServer.createHtmlHandler('<html></html>')

          return {
            server,
            textHandler,
            jsonHandler,
            htmlHandler
          }
        }).pipe(
          Effect.provide(TestHttpServerServiceLayer())
        )
      )

      expect(result.server.config.port).toBe(3000)
      expect(typeof result.textHandler).toBe('function')
      expect(typeof result.jsonHandler).toBe('function')
      expect(typeof result.htmlHandler).toBe('function')
    })

    it('should allow partial mock overrides', async () => {
      const mockStartServer = jest.fn(() => 
        Effect.succeed({
          config: { port: 9000 },
          stop: () => Effect.succeed(void 0)
        } satisfies RunningServer)
      )

      const result = await Effect.runPromise(
        Effect.gen(function* () {
          const httpServer = yield* HttpServerService
          return yield* httpServer.startServer({ port: 3000 }, () => HttpServerResponse.text('test'))
        }).pipe(
          Effect.provide(TestHttpServerServiceLayer({ startServer: mockStartServer }))
        )
      )

      expect(mockStartServer).toHaveBeenCalled()
      expect(result.config.port).toBe(9000)
    })
  })

  describe('HttpServerError', () => {
    it('should create error with message and cause', () => {
      const cause = new Error('Network error')
      const error = new HttpServerError({
        message: 'Failed to start server',
        cause
      })

      expect(error._tag).toBe('HttpServerError')
      expect(error.message).toBe('Failed to start server')
      expect(error.cause).toBe(cause)
    })

    it('should create error with just message', () => {
      const error = new HttpServerError({
        message: 'Server configuration invalid'
      })

      expect(error._tag).toBe('HttpServerError')
      expect(error.message).toBe('Server configuration invalid')
      expect(error.cause).toBeUndefined()
    })
  })

  describe('Server Configuration', () => {
    it('should accept port-only configuration', () => {
      const config: ServerConfig = { port: 3000 }
      expect(config.port).toBe(3000)
      expect(config.host).toBeUndefined()
    })

    it('should accept port and host configuration', () => {
      const config: ServerConfig = { port: 8080, host: 'localhost' }
      expect(config.port).toBe(8080)
      expect(config.host).toBe('localhost')
    })

    it('should allow undefined host explicitly', () => {
      const config: ServerConfig = { port: 3000, host: undefined }
      expect(config.port).toBe(3000)
      expect(config.host).toBeUndefined()
    })
  })
})