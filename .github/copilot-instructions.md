# Copilot Instructions for Spotify CLI

## Project Architecture

This is a **dual-language proving ground** with complete implementations in **F#** (`fsharp/`) and **TypeScript with Effect-TS** (`ts-effect/`). Both implement identical Spotify OAuth CLI functionality following language-specific functional programming patterns.

### Key Architectural Patterns

**F# Implementation (`fsharp/`)**:
- **Domain-Driven Design**: Domain types in `Domain/Types.fs` with constrained types (`String50`, `EmailAddress`, `SpotifyUri`) following type-driven design
- **Service Layer**: Interface-based dependency injection (`IConfigService`, `IBrowserService`, etc.) with service records pattern
- **Error Modeling**: Comprehensive discriminated unions in `Domain/Errors.fs` (avoid exceptions for domain errors)
- **Functional Composition**: `AuthenticationWorkflow.fs` orchestrates services using `taskResult` computation expressions from `FsToolkit.ErrorHandling`

**TypeScript Implementation (`ts-effect/`)**:
- **Effect-TS Services**: All services extend `Effect.Service` class with automatic dependency injection (`ConfigService`, `SpotifyApi`, etc.)
- **Schema-First**: Uses `@effect/schema` for all JSON validation and type derivation
- **Functional Error Handling**: Domain errors via `Data.TaggedError`, never throw exceptions
- **Dependency Injection**: Currying pattern with `Effect.fn` for pure functions, `Effect.gen` for orchestration

## Critical Development Workflows

### F# Project (`fsharp/`)
```bash
cd fsharp/
dotnet build                    # Build project
dotnet run -- auth             # Run auth command
dotnet run -- me               # Run user profile command
```

### TypeScript Project (`ts-effect/`)
```bash
cd ts-effect/
npm run build                   # Build with tsup
npm run dev -- auth            # Run with tsx
npm test                       # Jest with 99+ tests
npm run typecheck             # TypeScript validation
```

## Service Architecture Patterns

### F# Service Pattern
Services use interface-based dependency injection with service records:
```fsharp
type IAuthenticationWorkflowService =
    abstract StartAuthenticationFlow: AuthWorkflowConfig -> Task<Result<AuthWorkflowResult, AuthWorkflowError>>

type AuthWorkflowServices = {
    BrowserService: IBrowserService
    CallbackServerService: ICallbackServerService
    OAuthService: IOAuthService
    // ... other services
}
```

### TypeScript Effect Service Pattern
All services follow `Effect.Service` with curried dependency injection:
```typescript
export class ConfigService extends Effect.Service<ConfigService>()('ConfigService', {
  effect: Effect.gen(function* () {
    const fs = yield* FileSystem.FileSystem
    return {
      loadTokens: loadTokens(fs),
      saveTokens: saveTokens(fs)
    }
  })
}) {}
```

## Project-Specific Conventions

### F# Domain Modeling
- **Constrained Types**: Use `ConstrainedTypes.createString50` for validation, `TypeExtraction.getString50` for unwrapping
- **Error Handling**: Use `Result` for domain errors, exceptions only for panics/diagnostics
- **Service Composition**: Services passed as parameters, compose using `result` and `taskResult` CEs

### TypeScript Effect Patterns  
- **Services**: Always extend `Effect.Service`, use `TestXxxServiceLayer` for testing
- **Error Mapping**: Use `Effect.mapError` to transform to domain errors, never `throw`
- **Function Structure**: `Effect.fn` for pure functions with parameters, `Effect.gen` for workflows
- **Schema Validation**: All external data through `Schema.decodeUnknown` with error mapping

## Key Integration Points

### OAuth Flow Architecture
Both implementations follow identical OAuth PKCE flow:
1. **PKCE Generation** → **Authorization URL** → **Browser Launch** → **Callback Server** → **Code Exchange** → **Token Storage**
2. F# uses `AuthenticationWorkflow.fs` orchestration, TypeScript uses `OAuthService` composition
3. Token storage: `~/.spotify-cli/spotify.json` (both implementations compatible)

### Cross-Component Communication
- **Domain Types**: F# uses constrained types, TypeScript uses `Schema.TaggedStruct`
- **Service Boundaries**: Clear separation between pure domain logic and I/O effects
- **Error Propagation**: F# uses `Result` chains, TypeScript uses `Effect` pipelines

## Essential Commands & Debugging

### F# Debugging
```bash
cd fsharp/
dotnet fsi                      # F# Interactive for domain types testing
dotnet build --configuration Debug
```

### TypeScript Debugging  
```bash
cd ts-effect/
npm run dev -- --help         # CLI help
npm run test:watch            # Watch mode testing
npm run test:coverage         # Coverage report
```

### Shared Specifications
- **User Stories**: `SPEC.md` defines 5 core user stories for both implementations
- **Coding Standards**: Language-specific guides in `doc/` directory
- **OAuth Setup**: Requires Spotify Developer Dashboard app registration

Both implementations should maintain **functional equivalence** while showcasing language-specific best practices and patterns.
