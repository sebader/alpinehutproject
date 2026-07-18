# Copilot instructions — Alpine Hut Project

Aggregates alpine hut booking availability into a website (https://alpinehuts.silenced.eu/).
Data is scraped from two sources into an Azure SQL database, then served to a Vue SPA.

## Repository layout (three deployables + one shared config)

- **`FetchDataFunctions/`** — .NET **10** isolated Azure Functions app (deployed as Function App `alpinehutscrawler`). Timer-triggered crawlers + Durable Functions orchestrations that scrape huts/availability and write to Azure SQL via EF Core.
- **`WebsiteBackendFunctions/`** — .NET **8** isolated Azure Functions app. The Static Web App's managed API (`/api/*`); read/admin endpoints backed directly by the SQL bindings (no EF Core).
- **`website/`** — Vue 3 SPA (Vue CLI / `vue-cli-service`), deployed to Azure Static Web Apps.
- **`swa-db-connections/`** — Data API Builder (DAB) config exposing `Hut` and `BedCategory` entities as REST straight from SQL (SWA Database Connections).

The two Functions apps are the projects in `AlpineHutProject.sln`.

## Framework version constraint (do not break)

`FetchDataFunctions` targets `net10.0`; `WebsiteBackendFunctions` targets `net8.0`. **Keep `WebsiteBackendFunctions` on .NET 8 until Azure Static Web Apps supports .NET 10** — do not bump its `TargetFramework` before then. Because the two projects differ, a full-solution `dotnet build` fails on a machine that only has the .NET 8 SDK; build each project independently.

## Build & run

There are **no test projects** in this repo.

```bash
# Build each Functions app independently (avoids the mixed-SDK solution build)
dotnet build FetchDataFunctions/FetchDataFunctions.csproj -c Release
dotnet build WebsiteBackendFunctions/WebsiteBackendFunctions.csproj -c Release

# Website
cd website && npm ci && npm run build   # production build -> website/dist
npm run serve                           # dev server on :8080
```

Local end-to-end run (see `website/README.md`): start DAB (`dab start -c ./staticwebapp.database.config.json` from `swa-db-connections/`), the backend (`func start` from `WebsiteBackendFunctions/`), and the SWA emulator (`swa start --api-devserver-url http://localhost:7071` from `website/`, opens http://localhost:4280). Requires the `swa`, `dab`, and Azure Functions Core Tools (`func`) CLIs.

## Azure Functions conventions

- Isolated worker model everywhere. Name functions with `[Function(nameof(MethodName))]` and reference orchestration/activity functions by `nameof(...)` when scheduling.
- **Data access differs by app:** `FetchDataFunctions` uses EF Core via `Helpers.GetDbContext()` (reads the `DatabaseConnectionString` env var, one short-lived `AlpinehutsDbContext` per activity). `WebsiteBackendFunctions` uses the `[SqlInput(...)]` / SQL output bindings directly against `DatabaseConnectionString` — do not add EF Core there. DAB reads the connection string from `DATABASE_CONNECTION_STRING` instead.
- Timer-triggered crawlers early-return when `AZURE_FUNCTIONS_ENVIRONMENT == "Development"` so they never run locally.
- Durable orchestrations (Netherite storage provider) fan out in **batches of 10 with a 1-minute delay** between batches to avoid provider rate limiting; use `context.CreateReplaySafeLogger` inside orchestrators.
- Outbound scraping goes through the named `"HttpClient"` (configured in `FetchDataFunctions/Program.cs`) which carries a Polly retry policy that also retries HTTP 429 and 403 (treated as rate limiting).

## Domain model

- A hut's `Source` column selects the crawler: `"AV"` = Alpine clubs' central booking system (`hut-reservation.org`, `FetchDataFunctions/Helpers.cs` URLs); `"HuettenHoliday"` = huetten-holiday.com (`FetchDataFunctions/Functions/HuettenHoliday/`).
- Provider URLs, the test-hut exclusion list, and geocoding helpers (OSM Nominatim + Azure Maps) live in `FetchDataFunctions/Helpers.cs`.

## Frontend conventions

- Backend services (`website/src/services/*-service.js`) are registered as global properties (`$HutService`, `$AvailabilityService`, etc.) in `main.js` and reach the API via `window.API_URL`.
- Runtime config is injected through `window.*` globals in `website/public/config.js`, which is **replaced at deploy time**; the SWA workflow rewrites `window.VERSION_LABEL` with the short commit SHA.
- i18n via `vue-i18n` with locale `de` and fallback `en` — add every user-facing string to both `de` and `en` message blocks in `main.js`.
- Routing/auth: `website/staticwebapp.config.json` gates `PUT`/`DELETE /api/huts/*` to the `admin` role and redirects 401s to AAD login.

## CI/CD

- `.github/workflows/main_alpinehutscrawler.yml`: deploys `FetchDataFunctions` on push to **`main`** (paths `FetchDataFunctions/**`). It deletes any root `global.json` so `setup-dotnet` (10.0.x) drives the SDK.
- `.github/workflows/azure-static-web-apps-white-pond-0976e3603.yml`: builds/deploys `website` + `WebsiteBackendFunctions` on push to `main` and `dev`.
- **`dev` is the integration branch** (default target for Dependabot PRs); `main` is production. Open PRs against `dev`.
- Pushing to `dev` auto-deploys to a **SWA staging slot**; the Action build log prints the preview URL — use it to smoke-test/E2E the change (e.g. with Playwright) against a live environment.
