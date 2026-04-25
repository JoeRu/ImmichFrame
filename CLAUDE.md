# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ImmichFrame is a full-stack digital photo frame application that displays photos from [Immich](https://immich.app/) as a slideshow with weather, calendar, and clock overlays.

- **Backend**: C# / .NET 8.0 (ASP.NET Core Web API)
- **Frontend**: Svelte 5 / TypeScript / SvelteKit + Tailwind CSS
- **Docs site**: Docusaurus (in `docs/`)

## Commands

### Backend (.NET)

```bash
# Run locally (serves API + SPA)
make dev
# equivalent: dotnet run --project ./ImmichFrame.WebApi

# Run tests
make test-core      # dotnet test ./ImmichFrame.Core.Tests/...
make test-webapi    # dotnet test ./ImmichFrame.WebApi.Tests/...

# Run a single test
dotnet test ./ImmichFrame.Core.Tests/ImmichFrame.Core.Tests.csproj --filter "FullyQualifiedName~TestName"
```

### Frontend (Svelte)

```bash
cd immichFrame.Web
npm run dev       # Vite dev server (proxies API to localhost:5217)
npm run build     # Production build
npm run lint      # prettier --check + eslint
npm run format    # prettier --write
npm run check     # svelte-check (type checking)
```

### API client regeneration

Run the backend first (`make dev`), then:

```bash
make api
# Fetches swagger.json from localhost:5217, regenerates immichFrame.Web/src/lib/immichFrameApi.ts
```

### Docker

```bash
make docker-prod    # docker compose up with rebuild (uses docker/docker-compose.yml)
make docker-prune   # docker system prune -a
```

## Installing or assuming new tools

- If a task appears to require a new tool, package manager, or CLI utility that is not already installed or declared in the project's configuration (for example Docker, `package.json`, `requirements.txt`, and similar), **do not**:
  - execute or suggest installation commands directly without explicit user approval, or
  - work around the missing tool by fabricating scripts, hacks, or substitutes that would normally be provided by the proper tool.

- Instead:
  1. Clearly state which tool or package is missing and why it is needed.
  2. Ask the user for explicit confirmation before suggesting installation steps or assuming the tool is installed.
  3. Only proceed with installation instructions or dependent changes after the user explicitly agrees.

## Architecture

### Solution layout

```
ImmichFrame.Core          # Business logic library (no ASP.NET dependency)
ImmichFrame.WebApi        # ASP.NET Core host – API controllers + serves SPA
ImmichFrame.Core.Tests    # Unit tests for Core
ImmichFrame.WebApi.Tests  # Integration tests for WebApi
immichFrame.Web/          # Svelte SPA (built output copied into WebApi/wwwroot)
docs/                     # Docusaurus documentation site
```

### Backend data flow

1. **Startup** (`ImmichFrame.WebApi/Program.cs`): reads config via `ConfigLoader` → adapts to `IServerSettings` → registers services in DI.
2. **Multi-account routing**: `MultiImmichFrameLogicDelegate` distributes asset requests across multiple `PooledImmichFrameLogic` instances (one per configured Immich server).
3. **Asset pools** (`ImmichFrame.Core/Logic/Pool/`): modular filters — Favorites, Albums, People, Tags, Memories, etc. Each implements `IAssetPool` and returns filtered asset lists from the Immich API.
4. **Serving**: `AssetController` calls the logic layer, downloads/caches images, streams them to the frontend.
5. **Immich API client** (`ImmichFrame.Core/Api/`): generated from `ImmichFrame.Core/OpenAPIs/immich-openapi-specs.json` via NSwag at build time.

### Frontend structure

```
immichFrame.Web/src/
  routes/
    +page.svelte          # Main slideshow page
    +layout.svelte / .ts  # Root layout + data loading
  lib/
    components/elements/  # UI overlays: Asset, Clock, Weather, Calendar, …
    stores/               # Svelte stores (settings, current asset, timers)
    hooks/                # Reusable reactive hooks
    immichFrameApi.ts     # Generated API client (do not edit manually)
    constants.ts          # App-wide constants
    utils.ts              # Shared utilities
```

### Configuration schema

Settings are loaded from `Settings.json` or `Settings.yml` (see `docker/Settings.example.*`).

- **V1** (legacy): flat JSON/YAML parsed into `ServerSettingsV1`, then adapted via `ServerSettingsV1Adapter`.
- **V2+** (current): multi-account YAML with top-level `accounts[]` and `general` sections.

Key account-level fields: `ImmichServerUrl`, `ApiKey`, `Albums[]`, `People[]`, `Tags[]`, `ShowFavorites`, `ShowMemories`, `ImagesFromDays`.

Key general fields: `Interval`, `TransitionDuration`, `ShowClock`, `ShowWeather`, `WeatherApiKey`, `Webcalendars[]`, `Language`, `Style`, `Layout`.

### Centralized package management

`Directory.Packages.props` controls all NuGet package versions. Do not add `Version` attributes in individual `.csproj` files — add entries here instead.

## Release Writeup Instructions

When asked to create a release writeup or release notes, follow this process and format:

### Process

1. Run `git log --oneline <prev-tag>..HEAD` to get all commits since the last release.
2. Run `git diff <prev-tag>..HEAD --stat` to understand the scope of changes.
3. Fetch the GitHub release page if a URL is provided to cross-reference the auto-generated changelog.
4. Combine the raw git history with the GitHub changelog to produce a human-friendly writeup.

### Output Format

Use the template at `templates/release-template.md`. Key rules:

- **Title**: `# 📦 ImmichFrame Release vX.X.X.X – <Date>`
- **Intro**: One sentence summarising the release highlights (no heading).
- **Sections**: Follow the category order from `.github/release.yml` — Breaking Changes, New Features, Fixes, Documentation, Maintenance, Other Changes.
- **Each entry**:
  - H4 heading with emoji + feature name
  - Bold `**PR [#NNN](url) by @author**` attribution line
  - 2–4 sentences describing *what* changed and *why it matters* to the user
  - Include a code block if a config snippet helps illustrate usage
  - Separate entries with `---`
- **New Contributors**: Call out first-time contributors with 🎉
- **Footer**: Always end with the full changelog comparison URL.

### Tone

- Write for end users, not developers. Avoid internal refactor jargon unless it has a user-visible effect.
- Keep descriptions concise — 2–4 sentences per entry is enough.
- Use "you" / "your" to address users directly.
