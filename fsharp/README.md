# spotify-cli

F# CLI app for interacting with Spotify using functional programming principles.

- Refer to [SPEC.md](../SPEC.md) for user stories and acceptance criteria.
- Refer to [doc/coding-guide.md](../doc/coding-guide.md) and [fsharp-coding-guide.md](../doc/fsharp-coding-guide.md) for coding guides.

## Tasks

### Phase 1: Project Foundation & Infrastructure

- [x] Set up F# project with functional architecture and dependencies
- [x] Configure build system (dotnet CLI) and development tooling
- [x] Create CLI structure with commands (`auth`, `me`, `playlists`)
- [x] **Implement core domain types with constraints and validation**
- [x] **Implement comprehensive error modeling with discriminated unions**
- [x] **Create project directory structure following F# best practices**
- [x] **Implement functional service patterns following F# coding guide**
- [x] **Create ConfigService for token storage with Result-based error handling**
- [x] **Create HttpService for Spotify API calls with proper error modeling**
- [ ] ~~Set up test suite using property-based testing where appropriate~~ *(Test infrastructure designed but dependencies commented out)*
- [x] **Implement token storage functionality (`~/.spotify-cli/spotify.json`)**
- [x] **Create HTTP client utilities with domain-specific error types**
- [x] **Set up OAuth token exchange infrastructure using functional composition**

#### ✅ **Phase 1 Core Foundation - COMPLETED:**
- **Domain Types**: Complete constrained types (String50, EmailAddress, SpotifyUri, etc.) with validation
- **Error Hierarchy**: Comprehensive error modeling with ConfigError, HttpError, SpotifyError, etc.
- **CLI Framework**: Fully working Argu-based command parser with help system
- **Project Structure**: Professional organization with src/, tests/, proper .gitignore and .editorconfig
- **Build System**: Clean builds with .NET 9.0, proper warning configuration
- **Type Safety**: All domain types compile and work in F# Interactive

#### ✅ **Phase 1 Service Layer - COMPLETED:**
- **Service Architecture**: Well-designed following F# coding guide patterns with dependency injection
- **ConfigService**: Complete with FSharp.SystemTextJson integration, token storage, and file system abstraction
- **HttpService**: Complete with System.Net.Http implementation, domain error mapping, and Result-based error handling
- **CryptoService**: Complete with PKCE generation, secure random generation, and constrained types
- **CLI Integration**: Full service layer integration with placeholder command handlers
- **Build System**: All services compile and run successfully with integrated demonstration

**Current Status**: Complete functional foundation with working service layer integration

### Phase 2: Authentication Service Design

- [ ] **OAuth Module Implementation with Functional Design**
  - [ ] Create CryptoService using F# type-safe wrappers
  - [ ] Create BrowserService for cross-platform browser launching
  - [ ] Design OAuth workflow using function composition and Result types
  - [ ] Implement comprehensive error handling with domain-specific error types
  - [ ] Create OAuth service using dependency injection patterns from F# coding guide
  - [ ] Set up property-based and unit tests for crypto operations
  - [ ] Implement service composition for OAuth workflow
  - [ ] Focus on immutability and separation of pure/impure functions

---

### Phase 3: Auth with Spotify

- [ ] Register app in the [Spotify Developer Dashboard](https://developer.spotify.com/dashboard)
- [ ] Set up redirect URI for OAuth flow - `http://127.0.0.1:3000/`
- [ ] **Implement OAuth Infrastructure with Functional Architecture**
  - [ ] **CryptoService**: PKCE challenge generation with constrained types
  - [ ] **BrowserService**: Cross-platform browser launching with Result error handling
  - [ ] **OAuthService**: OAuth workflow using function composition and Reader pattern
  - [ ] **CallbackServer**: OAuth callback handling with functional HTTP server management
  - [ ] Generate authorization URL with domain-modeled scopes
  - [ ] Launch browser using platform-appropriate commands
  - [ ] **Run local HTTP server for redirect response** (functional approach to I/O boundary)
  - [ ] Parse authorization code using pattern matching and validation
  - [ ] Exchange authorization code for tokens using Result-based error handling
  - [ ] Store tokens securely using safe file I/O operations
  - [ ] **Comprehensive test coverage** using property-based testing where applicable
- [ ] **Show success or error messages** with user-friendly formatting
- [ ] **Enhanced Error Handling**: Domain-specific error types with helpful messages
- [ ] **User Experience**: Progress feedback and timeout handling
- [ ] **Architectural Design**: Maintain separation of pure domain logic from I/O effects

---

### Phase 4: Get User Profile

- [ ] Implement `spotify-cli me` command using functional command pattern
- [ ] Load access token from local storage with Result-based error handling
- [ ] Implement token refresh logic using function composition
- [ ] Call Spotify's `/me` API endpoint with proper error modeling
- [ ] Parse and display user information using:
  - [ ] Display Name (with Option handling for missing values)
  - [ ] Email (validated email type from coding guide)
  - [ ] Country (constrained string type)
  - [ ] Spotify URI (proper URI validation and formatting)
- [ ] Handle and display errors using domain-specific error types
- [ ] Format output using functional string composition

---

### Phase 5: Get User Playlists

- [ ] Implement `spotify-cli playlists` command using functional design
- [ ] Load access token from local storage with error handling
- [ ] Implement token refresh using existing composition patterns
- [ ] Call Spotify's `/me/playlists` API endpoint with pagination handling
- [ ] Parse and display playlist information using domain types:
  - [ ] Playlist name (constrained string type)
  - [ ] Number of tracks (validated positive integer)
  - [ ] Public/private status (discriminated union)
- [ ] Format output using functional table/list formatting
- [ ] Handle pagination using functional sequence operations
- [ ] Implement comprehensive error handling and display

---

### Phase 6: Token Management & Error Handling

- [ ] **Automatic Token Refresh**: Implement transparent token renewal
- [ ] **Error Recovery**: Handle network failures and API rate limits
- [ ] **Validation**: Add comprehensive input validation using F# type system
- [ ] **Performance**: Optimize API calls and response parsing
- [ ] **Testing**: Add integration tests for complete workflows
- [ ] **Documentation**: Add comprehensive inline documentation
- [ ] **Configuration**: Support environment-based configuration
