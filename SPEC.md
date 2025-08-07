# 🎟 User Story 1: Authenticate with Spotify

**As a** CLI user
**I want to** authenticate with my Spotify account
**So that** the app can access my profile and playlists

### ✅ Acceptance Criteria

```Given I have installed the CLI app
And I have a valid Spotify account
When I run`spotify-cli auth`
Then the app should open a browser window for Spotify login
And after I log in and approve the app, I should see a success message in the CLI
And my access and refresh tokens should be securely stored locally
```

---

# 👤 User Story 2: Retrieve User Profile

**As a** CLI user
**I want to** view my Spotify profile details
**So that** I can confirm that I’m authenticated and see my account info

### ✅ Acceptance Criteria

```Given I have already authenticated with Spotify
When I run`spotify-cli me`
Then the app should fetch my Spotify user profile
And display my username, email, country, and Spotify URI in the terminal
```

---

# 🎶 User Story 3: Retrieve My Playlists

**As a** CLI user
**I want to** list my Spotify playlists
**So that** I can view the playlists I’ve created or follow

### ✅ Acceptance Criteria

```Given I have already authenticated with Spotify
When I run`spotify-cli playlists`
Then the app should fetch my playlists from the Spotify API
And display each playlist's name, number of tracks, and public/private status in a clean list format
```

---

# 🔄 User Story 4: Refresh Expired Token

**As a** CLI user
**I want to** automatically refresh my token when expired
**So that** I don’t have to re-authenticate manually each time

### ✅ Acceptance Criteria

```Given my stored access token has expired
And I have a valid refresh token
When I run any authenticated command like`spotify-cli me`
Then the app should silently refresh the access token using the refresh token
And proceed with the original command
```

---

# 🚫 User Story 5: Handle Unauthorized Errors

**As a** CLI user
**I want to** be notified if my authentication fails
**So that** I can re-authenticate if needed

### ✅ Acceptance Criteria

```Given my access and refresh tokens are invalid or expired
When I run an authenticated command like`spotify-cli playlists`
Then the app should return an error message stating that re-authentication is required
And prompt me to run`spotify-cli auth` again
```
