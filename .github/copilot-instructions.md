# Copilot Instructions for ImmichFrame

## Build, test, and lint commands

This repository is split between a .NET 8 backend/core solution and a Svelte frontend in `immichFrame.Web`.

### Main workflows

| Task | Command |
| --- | --- |
| Install frontend dependencies | `npm --prefix immichFrame.Web install` |
| Run the app locally | `make dev` |
| Build the .NET solution | `dotnet build ImmichFrame.sln` |
| Build the frontend | `npm --prefix immichFrame.Web run build` |
| Run core tests | `make test-core` or `dotnet test ./ImmichFrame.Core.Tests/ImmichFrame.Core.Tests.csproj` |
| Run Web API tests | `make test-webapi` or `dotnet test ./ImmichFrame.WebApi.Tests/ImmichFrame.WebApi.Tests.csproj` |
| Run a single NUnit test | `dotnet test ./ImmichFrame.WebApi.Tests/ImmichFrame.WebApi.Tests.csproj --filter "FullyQualifiedName~AssetControllerTests.GetRandomImage_ReturnsImageFromMockServer"` |
| Run a single core NUnit test | `dotnet test ./ImmichFrame.Core.Tests/ImmichFrame.Core.Tests.csproj --filter "FullyQualifiedName~RandomDateAssetsPoolTests.TestLoadAssets_WithValidDateRange_ReturnsAssets"` |
| Frontend type/svelte checks | `npm --prefix immichFrame.Web run check` |
| Frontend lint | `npm --prefix immichFrame.Web run lint` |
| Regenerate the frontend API client from backend Swagger | `make api` or `npm --prefix immichFrame.Web run api` |

### Important command context

- `make dev` runs `dotnet run --project ./ImmichFrame.WebApi`.
- `ImmichFrame.WebApi` uses SPA proxy settings pointing at the Vite dev server in `immichFrame.Web`, so frontend work usually also needs `npm --prefix immichFrame.Web run dev`.
- CI currently runs the two .NET test projects separately; there is no repo-wide lint workflow in GitHub Actions.
- Copilot cloud-agent sessions can bootstrap from `.github/workflows/copilot-setup-steps.yml`, which restores .NET dependencies, installs frontend dependencies, and preinstalls Playwright MCP tooling plus Chromium.

## High-level architecture

### Solution shape

- `ImmichFrame.WebApi` is the application host and composition root. `Program.cs` loads configuration, registers services, exposes the REST API, and serves the SPA.
- `ImmichFrame.Core` holds the domain logic: Immich API integration, asset pools, account-selection strategies, caching, and weather/calendar services.
- `immichFrame.Web` is the Svelte frontend that drives the slideshow UI and talks only to the backend API under `/api/...`.

### Request and data flow

1. `Program.cs` loads config from `Config/Settings.json`, `Settings.yml` / `Settings.yaml`, or environment variables, then registers it as `IServerSettings` and `IGeneralSettings`.
2. `ConfigController` projects server-side `IGeneralSettings` into `ClientSettingsDto` for the frontend instead of exposing the full settings object directly.
3. Asset endpoints call `IImmichFrameLogic`, which is implemented by `MultiImmichFrameLogicDelegate`.
4. `MultiImmichFrameLogicDelegate` creates one `PooledImmichFrameLogic` per configured account and uses an `IAccountSelectionStrategy` to choose which account/pool supplies the next assets.
5. Each `PooledImmichFrameLogic` builds its asset source from the account settings: all assets, favorites, memories, albums, people, or the chronological wrapper around `RandomDateAssetsPool`.
6. The frontend route just mounts `src/lib/components/home-page/home-page.svelte`, which manages the slideshow backlog/history, preloads upcoming assets, switches between single and split view, handles video playback, and updates theme colors from the current asset.

### Frontend/backend integration

- The backend serves static files from `wwwroot` in production and falls back to `/index.html`.
- In development, `ImmichFrame.WebApi.csproj` is configured to proxy to the Vite dev server at `https://localhost:5173`.
- The TypeScript client in `immichFrame.Web/src/lib/immichFrameApi.ts` is generated from `openApi/swagger.json`; treat it as generated code and regenerate it instead of hand-editing it.
- The C# Immich client is also generated from the upstream Immich OpenAPI spec via the `OpenApiReference` in `ImmichFrame.Core.csproj`.

## Key conventions

## Installing or assuming new tools

- If a task appears to require a new tool, package manager, or CLI utility that is not already installed or declared in the project’s configuration (for example Docker, `package.json`, or `requirements.txt`), do not execute or suggest installation commands directly without explicit user approval.
- Do not work around a missing proper tool by fabricating scripts, hacks, or substitutes that normally depend on that tool.
- Instead:
  1. Clearly state which tool or package is missing and why it is needed.
  2. Ask for explicit confirmation before suggesting installation steps, or ask the user to install it and provide the command they want used.
  3. Only proceed with installation guidance or tool-dependent changes after the user explicitly agrees.

### Configuration and settings

- Keep server settings and client settings separate. If a new setting needs to reach the browser, update both the server-side settings model and `ClientSettingsDto.FromGeneralSettings(...)`.
- Config loading preserves backward compatibility: the loader tries current JSON/YAML formats first, then adapts older v1 formats, then falls back to environment variables.
- Local development expects a docker `.env` file one directory up at `docker/.env`; `Program.cs` loads it only in development.

### Authentication and client identity

- Authentication is a custom bearer-token check against `GeneralSettings.AuthenticationSecret`, not ASP.NET Identity or JWTs.
- The frontend persists `authSecret` and `clientIdentifier` in local storage and sets the `Authorization` header through `src/lib/index.ts`.
- Controllers sanitize `clientIdentifier` before logging or including it in webhook notifications. Keep using `SanitizeString()` for this flow.

### Asset and slideshow behavior

- Preserve asset ordering when working in selection strategies or multi-account aggregation. `MultiImmichFrameLogicDelegate.GetAssets()` explicitly avoids shuffling so chronological grouping still works.
- Asset-selection behavior belongs in `ImmichFrame.Core/Logic/Pool` and `Logic/AccountSelection`, not in controllers.
- `PooledImmichFrameLogic.GetImage(...)` treats images and videos differently: images may be cached to `ImageCache`, videos are streamed from Immich.
- The frontend slideshow logic assumes preloaded asset queues plus a bounded history, and split view is only used when the next two assets satisfy the layout checks.

### Frontend coding patterns

- The Svelte app uses Svelte 5 rune syntax such as `$state`, `$derived`, and `$bindable`; follow the existing style instead of older store/reactive syntax when editing these components.
- API access should go through `$lib/index.ts` / the generated client, not ad-hoc `fetch` wrappers.
