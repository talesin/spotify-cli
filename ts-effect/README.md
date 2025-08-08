# spotify-cli

TypeScript/Effect CLI app for interacting with Spotify.

## Functionality

Refer to [SPEC.md](../SPEC.md) for user stories and acceptance criteria.

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

---

### 🔒 Auth with Spotify

- [ ] Register your app in the [Spotify Developer Dashboard](https://developer.spotify.com/dashboard)
- [ ] Set up redirect URI for OAuth flow
- [ ] Implement `spotify-cli auth` command
  - [ ] Generate authorization URL with correct scopes (`user-read-email`, `playlist-read-private`, etc.)
  - [ ] Launch browser to start the OAuth flow
  - [ ] Run a local HTTP server or poll for redirect response (e.g. `localhost:8888/callback`)
  - [ ] Parse authorization code from the redirect URI
  - [ ] Exchange authorization code for access + refresh tokens
  - [ ] Store tokens securely in local storage (e.g. `~/.spotify-cli/spotify.json`)
- [ ] Show success or error message after auth completes

---

### 👤 Get User Profile

- [ ] Implement `spotify-cli me` command
- [ ] Load access token from local storage
- [ ] If token expired, refresh it using the refresh token
- [ ] Call Spotify's `/me` API endpoint
- [ ] Parse and display:
  - [ ] Display Name
  - [ ] Email
  - [ ] Country
  - [ ] Spotify URI (or Profile URL)
- [ ] Handle and display errors (e.g. unauthorized, network issue)

---

### 🎶 Get User Playlists

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
