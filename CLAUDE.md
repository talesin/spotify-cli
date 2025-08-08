# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Spotify CLI application serving as a "proving ground to test different languages and frameworks by connecting to Spotify." The project provides a structured implementation approach with detailed specifications and coding standards.

## Project Structure

- `ts-effect/`: TypeScript Effect-TS implementation with full development setup
  - `src/`: Source code directory (empty, ready for implementation)
  - `test/`: Test directory (empty, ready for tests)
  - `package.json`: Effect-TS dependencies and development tooling
  - `tsconfig.json`: Strict TypeScript configuration with path mapping
  - `eslint.config.mjs`: Modern ESLint configuration with TypeScript rules
  - `jest.config.ts`: Jest testing configuration
- `doc/`: Documentation and coding standards
  - `coding-guide.md`: General coding standards and best practices
  - `typescript-coding-guide.md`: TypeScript-specific coding standards
  - `react-coding-guide.md`: React-specific coding standards
- `SPEC.md`: Detailed user stories and acceptance criteria
- `README.md`: Project description with reference to specifications
- `.envrc`: Environment configuration (do not overwrite)

## Commands

### TypeScript/Effect-TS Implementation (ts-effect/)
- `npm test`: Run Jest tests (currently placeholder)
- `npm run lint`: Run ESLint (inferred from eslint.config.mjs)
- `npm run typecheck`: Run TypeScript compiler (inferred from tsconfig.json)

## Specifications

The project follows a clear specification-driven development approach with 5 main user stories:

1. **Authentication**: `spotify-cli auth` - OAuth flow with Spotify
2. **User Profile**: `spotify-cli me` - Display authenticated user info
3. **Playlists**: `spotify-cli playlists` - List user's playlists
4. **Token Refresh**: Automatic token renewal
5. **Error Handling**: Unauthorized error management

See `SPEC.md` for detailed acceptance criteria and `ts-effect/README.md` for implementation tasks.

## Architecture Notes

- **Effect-TS Focus**: Primary implementation uses Effect-TS for functional programming patterns
- **Strict TypeScript**: Comprehensive type checking with modern configuration
- **CLI Framework**: Uses @effect/cli for command-line interface
- **Testing**: Jest with ts-jest for TypeScript testing
- **Code Quality**: ESLint + Prettier with strict rules

## Coding Standards

- Use descriptive names (no abbreviations)
- Check for existing code before writing new implementations
- Prefer simple solutions and avoid code duplication
- Write thorough tests for all code
- Follow patterns in `doc/coding-guide.md` and language-specific guides
- **Important**: Never overwrite the `.envrc` file

## Claude Code Configuration

- `.claude/settings.local.json`: Allows `find` commands for file searching
- `.gitignore`: Updated to include `.claude` and `.envrc`