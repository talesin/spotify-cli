# spotify-cli

TypeScript/Effect CLI app for interacting with Spotify.

- Refer to [SPEC.md](../SPEC.md) for user stories and acceptance criteria.
- Refer to [doc/coding-guide.md](../doc/coding-guide.md) and [typescript-coding-guide.md](../doc/typescript-coding-guide.md) for coding guides.

## Tasks

### ✅ Phase 1: Project Foundation & Infrastructure - COMPLETED

- [x] ✅ Set up TypeScript project with Effect-TS dependencies
- [x] ✅ Configure build system (tsup) and development tooling
- [x] ✅ Create CLI structure using @effect/cli with commands (`auth`, `me`, `playlists`)
- [x] ✅ Implement proper Effect-TS service patterns following coding guide
- [x] ✅ Create ConfigService for token storage with error handling
- [x] ✅ Create HttpService for Spotify API calls with error handling
- [x] ✅ Set up comprehensive test suite (25 passing tests)
- [x] ✅ Implement token storage functionality (`~/.spotify-cli/spotify.json`)
- [x] ✅ Create HTTP client utilities with proper error types
- [x] ✅ Set up OAuth token exchange infrastructure
- [x] ✅ Add comprehensive JSDoc documentation
- [x] ✅ Configure ESLint/Prettier and ensure clean builds

### ✅ Phase 2: OAuth Service Refactoring - COMPLETED

- [x] ✅ **OAuth Module Refactoring for Better Testability**
  - [x] ✅ Created CryptoService wrapper around Node.js crypto module
  - [x] ✅ Created BrowserService wrapper for browser launching
  - [x] ✅ Refactored OAuth module into Effect service with dependency injection
  - [x] ✅ Created comprehensive tests for CryptoService (32 tests)
  - [x] ✅ Created comprehensive tests for BrowserService (33 tests)
  - [x] ✅ Updated OAuth tests to use service-based testing patterns
  - [x] ✅ Updated index.ts to use new OAuth service architecture
  - [x] ✅ Improved separation of concerns and testability

---

### ✅ Phase 3: Auth with Spotify - COMPLETED

- [x] ✅ Register your app in the [Spotify Developer Dashboard](https://developer.spotify.com/dashboard)
- [x] ✅ Set up redirect URI for OAuth flow - `http://127.0.0.1:3000/`
- [x] ✅ **Implement OAuth Infrastructure with Effect-TS Services**
  - [x] ✅ **CryptoService**: PKCE challenge generation with secure random values
  - [x] ✅ **BrowserService**: Cross-platform browser launching for OAuth flow
  - [x] ✅ **OAuthService**: Complete OAuth service with dependency injection
  - [x] ✅ **CallbackServer**: OAuth callback handling with HTML responses (HTTP server management at execution boundary)
  - [x] ✅ Generate authorization URL with correct scopes (`user-read-email`, `playlist-read-private`, etc.)
  - [x] ✅ Launch browser to start the OAuth flow
  - [x] ✅ **Run a local HTTP server for redirect response** (full implementation complete)
  - [x] ✅ Parse authorization code from the redirect URI
  - [x] ✅ Exchange authorization code for access + refresh tokens
  - [x] ✅ Store tokens securely in local storage (`~/.spotify-cli/spotify.json`)
  - [x] ✅ **Comprehensive test coverage** (99 passing tests)
- [x] ✅ **Show success or error message after auth completes** (comprehensive UX implemented)
- [x] ✅ **Enhanced Error Handling**: Context-sensitive error messages and troubleshooting tips
- [x] ✅ **User Experience**: Progress indicators, timeout handling, and helpful guidance
- [x] ✅ **Architectural Refinement**: Removed HttpServerService anti-pattern, moved HTTP server creation to execution boundary following proper Effect-TS patterns

---

### Phase 4: Get User Profile - COMPLETED

- [x] ✅ Implement `spotify-cli me` command
- [x] ✅ Load access token from local storage
- [x] ✅ If token expired, refresh it using the refresh token
- [x] ✅ Call Spotify's `/me` API endpoint
- [x] ✅ Parse and display:
  - [x] ✅ Display Name
  - [x] ✅ Email
  - [x] ✅ Country
  - [x] ✅ Spotify URI (or Profile URL)
- [x] ✅ Handle and display errors (e.g. unauthorized, network issue)

---

### Phase 5: Get User Playlists

- [ ] Implement `spotify-cli playlists` command
- [ ] Load access token from local storage
- [ ] If token expired, refresh it using the refresh token
- [ ] Call Spotify's `/me/playlists` API endpoint (handle pagination)
- [ ] Parse and display for each playlist:
  - [ ] Playlist name
  - [ ] Number of tracks
  - [ ] Public/private status
- [ ] Format output for readability (e.g. table or list with padding)
- [ ] Handle and display errors
