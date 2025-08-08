# TypeScript Coding Guide

Use Effect schemas for all JSON validation.
Prefer pure functions and immutability in TypeScript.
Avoid returning null prefer undefined and, where possible, use tagged union types.
Avoid the use of the any type, prefer explicitly typed or unknown
Wrap all non-local or unsafe code in Effect.try or Effect.tryPromise.
Use Effect.try or Effect.tryPromise instead of try/catch
Keep Effect.try and Effect.tryPromise to the specific line of code that may throw an error
Do not throw errors use Effect patterns
Avoid external state libraries (e.g., no Redux or Zustand).
Use ?? instead of || when checking for null or undefined.
Do not use implicit boolean expressions
Prefer the use of types over interfaces unless there's precedence (ie. if there are specific examples or patterns to follow)

## Error Handling

Use `Data.TaggedError` for domain-specific error classes instead of `Data.TaggedClass`. This provides better error semantics and integrates properly with Effect's error handling.

```typescript
export class ConfigParseError extends Data.TaggedError("ConfigParseError")<{
  readonly message: string;
  readonly error: ParseError | PlatformError;
}> {}
```

Use `Effect.mapError` to transform errors into domain-specific types:

```typescript
const result =
  yield *
  someOperation.pipe(
    Effect.mapError(
      (error) =>
        new ConfigParseError({
          message: error.message,
          error,
        })
    )
  );
```

## Effect Services

Entry into functions should be via an Effect.Service class. This will allow for dependency injection and testing. Do not create a a Live layer, Effect.Service provides a Default property for that.

```typescript
export class ExampleService extends Effect.Service<ExampleService>()(
  "ExampleService",
  {
    effect: Effect.gen(function* () {
      const dependency1 = yield* Dependency1;
      const dependency2 = yield* Dependency2;
      return {
        executeSomething: executeSomething(dependency1),
        executeAnother: executeAnother(dependency2),
      };
    }),
  }
) {}

export const TestExampleServiceLayer = (fn?: {
  executeSomething?: () => ExampleService["executeSomething"];
  executeAnother?: () => ExampleService["executeAnother"];
}) =>
  Layer.succeed(
    ExampleService,
    ExampleService.of({
      _tag: "ExampleService",
      executeSomething: fn?.executeSomething ?? (() => Effect.succeed("")),
      executeAnother: fn?.executeAnother ?? (() => Effect.succeed([])),
    })
  );
```

## HTTP Requests with Effect

Setup the function to fetch a URL with currying to inject HttpClient at runtime. Always handle errors with proper error mapping.

```typescript
import { HttpClient } from "@effect/platform";
import { HttpClientError } from "@effect/platform/HttpClientError";

export const fetchUrl = (httpClient: HttpClient.HttpClient) =>
  Effect.fn(function* (url: string) {
    const response = yield* httpClient
      .get(url)
      .pipe(
        Effect.mapError((error) => new NetworkError({ message: String(error) }))
      );

    const text = yield* response.text.pipe(
      Effect.mapError(
        (error) => new InvalidResponse({ message: error.message, error })
      )
    );

    return text;
  });
```

For API calls with authentication and response parsing:

```typescript
export const spotifyApiCall =
  (httpClient: HttpClient.HttpClient) =>
  <A>(
    endpoint: string,
    accessToken: string,
    parser: (json: unknown) => Effect.Effect<A, ParseError>
  ) =>
    Effect.gen(function* () {
      const request = createAuthenticatedRequest(endpoint, accessToken);
      const response = yield* httpClient
        .execute(request)
        .pipe(
          Effect.mapError(
            (error) => new NetworkError({ message: String(error) })
          )
        );

      const result = yield* handleHttpResponse(parser)(response);
      return result;
    });
```

## Effect.gen vs Effect.fn

Use `Effect.fn` for pure, reusable functions that accept parameters and return Effects. Use `Effect.gen` for workflow orchestration and complex operations.

```typescript
// Use Effect.fn for pure functions with parameters
const loadTokens = (fs: FileSystem.FileSystem) =>
  Effect.fn(function* () {
    const configPath = yield* getConfigPath;
    const exists = yield* fs
      .exists(configPath)
      .pipe(Effect.catchAll(() => Effect.succeed(false)));
    // ... rest of implementation
  });

// Use Effect.gen for workflow orchestration
const authWorkflow = Effect.gen(function* () {
  const configService = yield* ConfigService;
  const tokens = yield* configService.loadTokens();
  // ... complex workflow
});
```

## Testing with Effect

When testing with Effect, you will need to do something similar to the following. Prefer testing each function in isolation passing in mock dependencies.

```typescript
// function to test
const doTheThing = (service: Service) => Effect.fn(function* (arg: string) {
  const result = yield* service.doTheOtherThing()
  return result + arg
})


it('should do the thing', async () => {
  // use test layer to mock dependencies
  const testLayer = TestLayer({
    doTheOtherThing: () => Effect.succeed([])
  })

  // run effect with test layer
  await Effect.gen(function* () {
    const service = yield* MyService // get dependencies
    const result = yield* doTheThing(service)('arg) // call function with test data
    expect(result).toEqual({ ... })
  }).pipe(
    Effect.provide(testLayer), // provide test layer
    Effect.runPromise
  )
})
```

## Dependency Injection

The dependency injection pattern is used to provide dependencies to services. This allows for testing and dependency injection. This is using a currying pattern to provide the dependencies to the service. The first function should contain the dependencies arguments and return an Effect.fn that contains the arguments for the actual function.

Keep the service class minimal with just the code to define and configure it. All exported or supporting functions should be declared out side the class.

```typescript
export const executeSomething = (dependency1: Dependency1) =>
  Effect.fn(function* (arg1: string) {
    const dependency2 = yield* Dependency2;
    const result = yield* dependency1.executeSomething(arg1);
    return result;
  });

export class ExampleService extends Effect.Service<ExampleService>()(
  "ExampleService",
  {
    effect: Effect.gen(function* () {
      const dependency1 = yield* Dependency1;
      const dependency2 = yield* Dependency2;
      return {
        executeSomething: executeSomething(dependency1),
        executeAnother: executeAnother(dependency2),
      };
    }),
  }
) {}
```

## Tagged Data

```typescript
type Person = {
  readonly _tag: "Person"; // the tag
  readonly name: string;
};

const Person = Data.tagged<Person>("Person");
```

## Date and Time

```typescript
const currentTime = yield * Clock.currentTimeMillis;
const date = new Date(currentTime);
```

## Random

Prefer using Effect.random or Random over Math.random

```typescript
const n1 = yield * Random.nextIntBetween(1, 10);

// or

const random = yield * Effect.random;
const n2 = yield * random.next;
```

## Schema

Prefer using tagged schemas with derived types over interfaces, unless there is a specific need for an interface.
Keep names the same unless there is a conflict. Use proper error handling with `Effect.mapError` when decoding.

```typescript
// define the schema
export const SampleResponse = Schema.TaggedStruct("SampleResponse", {
  message: Schema.String,
  data: Schema.Array(Schema.String),
});

// define the type
export type SampleResponse = typeof SampleResponse.Type;

// decode JSON string to unknown first
const json =
  yield *
  Schema.decodeUnknown(Schema.parseJson())(
    '{"message": "Test message", "data": []}'
  ).pipe(
    Effect.mapError(
      (error) =>
        new InvalidResponse({
          message: `Invalid JSON: ${error.message}`,
          error,
        })
    )
  );

// decode the JSON using schema with error handling
const response: SampleResponse =
  yield *
  Schema.decodeUnknown(SampleResponse)(json).pipe(
    Effect.mapError(
      (error) =>
        new InvalidResponse({
          message: `Invalid format: ${error.message}`,
          error,
        })
    )
  );

// make the response directly using .make()
const response = SampleResponse.make({
  message: "Test message",
  data: [],
});
```

## Environment Variables

Prefer using Config over process.env

```typescript
const myEnvVar =
  yield * Config.string("MY_ENV_VAR").pipe(Config.withDefault("default value"));
```

## Logging

Prefer using Effect.log over console.log

```typescript
Effect.log("Hello World");
```
