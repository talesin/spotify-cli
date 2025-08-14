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

### Phase 2: Authentication Service Design - ✅ **COMPLETED**

- [x] **OAuth Module Implementation with Functional Design**
  - [x] **Create CryptoService using F# type-safe wrappers** - PKCE generation, secure state, URL-safe base64
  - [x] **Create BrowserService for cross-platform browser launching** - Windows/macOS/Linux support with fallback
  - [x] **Design OAuth workflow using function composition and Result types** - Full Result-based error chaining
  - [x] **Implement comprehensive error handling with domain-specific error types** - BrowserError, OAuthError, CallbackServerError
  - [x] **Create OAuth service using dependency injection patterns from F# coding guide** - Clean service interfaces
  - [ ] ~~Set up property-based and unit tests for crypto operations~~ *(Deferred to testing phase)*
  - [x] **Implement service composition for OAuth workflow** - AuthenticationWorkflow orchestration service
  - [x] **Focus on immutability and separation of pure/impure functions** - Pure domain logic, IO boundary separation

#### ✅ **Phase 2 Core Services - COMPLETED:**
- **BrowserService**: Cross-platform browser launching (Windows/macOS/Linux) with fallback manual instructions
- **CallbackServerService**: HTTP server for OAuth callbacks with timeout handling and HTML responses  
- **OAuthService**: Complete Spotify OAuth integration with PKCE, token exchange, and refresh
- **AuthenticationWorkflow**: Service orchestration with full error handling and user feedback
- **CLI Integration**: Working OAuth demo with PKCE generation, authorization URL creation, and browser launch

**Current Status**: Complete OAuth authentication architecture ready for live Spotify integration

---

### Phase 3: Auth with Spotify - ✅ **COMPLETED**

- [x] **Real Spotify OAuth Integration** using environment-loaded credentials from `.envrc`
- [x] **Complete OAuth Infrastructure with Functional Architecture**
  - [x] **CryptoService**: PKCE challenge generation with constrained types - Full PKCE/S256 implementation
  - [x] **BrowserService**: Cross-platform browser launching with Result error handling - Windows/macOS/Linux support
  - [x] **OAuthService**: OAuth workflow using function composition and environment configuration
  - [x] **CallbackServer**: OAuth callback handling with functional HTTP server management and timeout
  - [x] Generate authorization URL with domain-modeled scopes and real Spotify endpoints
  - [x] Launch browser using platform-appropriate commands with fallback instructions
  - [x] **Run local HTTP server for redirect response** with proper callback processing
  - [x] Parse authorization code using pattern matching and state validation
  - [x] Exchange authorization code for tokens using Result-based error handling
  - [x] Store tokens securely using safe file I/O operations in `~/.spotify-cli/spotify.json`
  - [ ] ~~Comprehensive test coverage~~ *(Deferred to testing phase)*
- [x] **Show success or error messages** with user-friendly formatting and emoji indicators
- [x] **Enhanced Error Handling**: Domain-specific error types with helpful environment setup guidance
- [x] **User Experience**: Progress feedback, timeout handling, and detailed error instructions
- [x] **Architectural Design**: Complete separation of pure domain logic from I/O effects

#### ✅ **Phase 3 OAuth Implementation - COMPLETED:**
- **Environment Integration**: Automatically loads Spotify credentials from `.envrc` file
- **Real OAuth Flow**: Complete PKCE-secured OAuth 2.0 flow with Spotify's live endpoints
- **AuthenticationWorkflow**: Orchestrates entire flow from URL generation to token storage
- **CLI Integration**: Seamless integration with enhanced error handling and user guidance
- **Security**: Proper PKCE implementation, state validation, and secure token storage
- **Smart Authentication**: Checks existing tokens before starting OAuth (no unnecessary re-auth)

**Current Status**: Production-ready OAuth authentication with intelligent token management

---

### Phase 4: Get User Profile - ✅ **COMPLETED**

- [x] **Implement `spotify-cli me` command using functional command pattern**
- [x] **Load access token from local storage with Result-based error handling**
- [x] **Implement token refresh logic using function composition**
- [x] **Call Spotify's `/me` API endpoint with proper error modeling**
- [x] **Parse and display user information using:**
  - [x] **Display Name (with Option handling for missing values)**
  - [x] **Email (validated email type from coding guide)**
  - [x] **Country (constrained string type)**
  - [x] **Spotify URI (proper URI validation and formatting)**
- [x] **Handle and display errors using domain-specific error types**
- [x] **Format output using functional string composition**

#### ✅ **Phase 4 Achievements:**
- **UserProfileService**: Complete with automatic token refresh
- **SpotifyApiClient**: Real API integration with JSON parsing
- **Bordered Display**: Professional user profile formatting
- **Error Handling**: Domain-specific errors with helpful tips
- **Token Management**: Transparent refresh when near expiry
- **Service Integration**: All components working together

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

- [x] **Automatic Token Refresh**: Implement transparent token renewal
- [x] **Error Recovery**: Handle network failures and API rate limits
- [x] **Validation**: Add comprehensive input validation using F# type system
- [ ] **Performance**: Optimize API calls and response parsing
- [ ] **Testing**: Add integration tests for complete workflows
- [ ] **Documentation**: Add comprehensive inline documentation
- [x] **Configuration**: Support environment-based configuration

#### ✅ **Phase 6 Achievements (Partial):**
- **Token Refresh**: Automatic renewal when tokens near expiry (5 minutes)
- **Error Recovery**: Network timeouts, API errors, authentication failures
- **Type Validation**: Constrained types for all domain objects (String50, EmailAddress, etc.)
- **Configuration**: Environment variable support with `.envrc` integration
