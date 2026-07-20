# Copilot instructions — Alpine Hut Project

Aggregates alpine hut booking availability into a website (https://alpinehuts.silenced.eu/).
Data is scraped from two sources into an Azure SQL database, then served to a Vue SPA.

## Repository layout (three deployables + E2E suite)

- **`FetchDataFunctions/`** — .NET **10** isolated Azure Functions app on **Flex Consumption** in **France Central**, deployed to `alpinehutscrawler-dev` (dev) and `alpinehutscrawler-prod` (prod). Timer-triggered crawlers + **Durable Functions** (Durable Task Scheduler backend) that scrape huts/availability and write to Azure SQL via EF Core. See `FetchDataFunctions/README.md` for the infra overview.
- **`FetchDataFunctions.Tests/`** — **NUnit** unit tests for the crawler's pure logic (availability reconciler, coordinate/altitude parsing, Swiss-grid conversion, provider-payload deserialization).
- **`WebsiteBackendFunctions/`** — .NET **8** isolated Azure Functions app. The Static Web App's managed API (`/api/*`); read/admin endpoints backed directly by the SQL bindings (no EF Core).
- **`website/`** — Vue 3 SPA (**Vite**, dev/build on `:8080`), deployed to Azure Static Web Apps.
- **`e2e/`** — standalone Playwright end-to-end smoke suite (its own `package.json`; **not deployed** and intentionally kept out of the frontend build). See Testing.

The two Functions apps plus the `FetchDataFunctions.Tests` project are in `AlpineHutProject.sln`.

## Framework version constraint (do not break)

`FetchDataFunctions` targets `net10.0`; `WebsiteBackendFunctions` targets `net8.0`. **Keep `WebsiteBackendFunctions` on .NET 8 until Azure Static Web Apps supports .NET 10** — do not bump its `TargetFramework` before then. Because the two projects differ, a full-solution `dotnet build` fails on a machine that only has the .NET 8 SDK; build each project independently.

## Build & run

The `FetchDataFunctions` app has an **NUnit** unit-test project (`FetchDataFunctions.Tests`); `WebsiteBackendFunctions` has none. Frontend/site validation is a Playwright E2E suite (see Testing).

```bash
# Build each Functions app independently (avoids the mixed-SDK solution build)
dotnet build FetchDataFunctions/FetchDataFunctions.csproj -c Release
dotnet build WebsiteBackendFunctions/WebsiteBackendFunctions.csproj -c Release

# Crawler unit tests (NUnit, net10)
dotnet test FetchDataFunctions.Tests/FetchDataFunctions.Tests.csproj

# Website (Node 22; Vite)
cd website && npm ci && npm run build   # production build -> website/dist
npm run serve                           # dev server on :8080
npm run lint                            # ESLint (flat config, eslint-plugin-vue)
npm run format                          # Prettier (writes src/)
```

Local end-to-end run (see `website/README.md`): start the backend (`func start` from `WebsiteBackendFunctions/`) and the SWA emulator (`swa start --api-devserver-url http://localhost:7071` from `website/`, opens http://localhost:4280). Requires the `swa` and Azure Functions Core Tools (`func`) CLIs.

Run the `FetchDataFunctions` crawler app locally with `dotnet run` from `FetchDataFunctions/` (the recommended path for .NET isolated — `func start` warns and may fail to load worker extensions). It needs **Azurite** running for `AzureWebJobsStorage` (blob secret repo + timer/durable listeners); without it the host cancels startup. Durable state uses the **Durable Task Scheduler**; locally point `DURABLE_TASK_SCHEDULER_CONNECTION_STRING` at the DTS emulator (Docker image `mcr.microsoft.com/dts/dts-emulator`) with `TASKHUB_NAME=default` (already in `local.settings.json`). Timer CRON schedules come from app settings (`HutsUpdateSchedule`, `AvailabilityUpdateSchedule`, `HuettenHolidayAvailabilityUpdateSchedule`, `CleanupSchedule`). Timer crawlers early-return in Development, but you can drive a single hut on demand: `GET http://localhost:7071/api/UpdateAvailabilityHttpTriggered?hutid=<id>` (also `UpdateHutHttpTriggered`), which invoke the same activity code (incl. the EF-Core SQL writes) directly. Both Functions apps' `local.settings.json` point `DatabaseConnectionString` at the **real prod Azure SQL** DB, so it's queryable directly for diagnosis.

## Testing

No unit tests for the website/backend. `FetchDataFunctions` has an **NUnit** suite (`FetchDataFunctions.Tests`, 40 tests: availability reconciler, coordinate/altitude parsing, Swiss-grid conversion, provider-payload deserialization) — run `dotnet test FetchDataFunctions.Tests/FetchDataFunctions.Tests.csproj`. Everything else is validated by **build + lint + an end-to-end Playwright smoke suite** run against a **live deployed environment** (the API needs the real Azure SQL DB, so E2E can't run fully offline).

- **E2E suite** lives in `e2e/` (standalone `package.json`, deliberately kept out of the frontend build). Six read-only smoke tests (map markers, `/api/huts`, hut list, hut detail + availability, info page, `/api/availability/{today}`) with threshold assertions. Run locally:
  ```bash
  cd e2e && npm ci && npx playwright install chromium
  E2E_BASE_URL="<target-url>" npx playwright test   # defaults to production
  ```
- **Automatic post-deploy check — how changes are tested in dev:** the SWA workflow (`azure-static-web-apps-white-pond-0976e3603.yml`) has a final `e2e_smoke` job that `needs` the deploy, waits until the slot serves the just-pushed commit (polls `config.js` for the short SHA so it never tests a stale build), then runs the suite — against the **dev staging slot** for `dev` pushes and **production** for `main`. So the loop is: push to `dev` → staging deploy → E2E runs automatically; check the Action run (and the uploaded `playwright-report` artifact) before promoting via a `dev`→`main` PR.
- **Scheduled monitoring:** `e2e-smoke.yml` runs the same suite daily against production (+ manual dispatch with a `base_url` input) as synthetic monitoring, independent of deploys.
- The dev staging slot URL is `https://white-pond-0976e3603-dev.westeurope.1.azurestaticapps.net` (the deploy Action log also prints it).

## Azure Functions conventions

- Isolated worker model everywhere. Name functions with `[Function(nameof(MethodName))]` and reference orchestration/activity functions by `nameof(...)` when scheduling.
- **Data access differs by app:** `FetchDataFunctions` uses EF Core via an injected **`IDbContextFactory<AlpinehutsDbContext>`** (pooled; one short-lived `await using` context per activity — the right fit for the Durable fan-out). The connection string is bound from configuration in `Program.cs`. `WebsiteBackendFunctions` uses the `[SqlInput(...)]` / SQL output bindings directly against `DatabaseConnectionString` — do not add EF Core there.
- Timer-triggered crawlers early-return when `IHostEnvironment.IsDevelopment()` so they never run locally. Their CRON expressions come from app settings (`%HutsUpdateSchedule%` etc.), so dev runs weekly and prod runs daily from the same code.
- Durable orchestrations use the **Durable Task Scheduler** (`azureManaged` provider in `host.json`; `hubName` + connection string resolved from `TASKHUB_NAME` / `DURABLE_TASK_SCHEDULER_CONNECTION_STRING`). They fan out in **batches of 10 with a 1-minute delay** via the shared `Functions/DurableFanOut.cs` helper; use `context.CreateReplaySafeLogger` inside orchestrators. The per-day availability upsert/diff logic is the pure, unit-tested `AvailabilityReconciler`.
- Outbound scraping goes through the named `"HttpClient"` (configured in `FetchDataFunctions/Program.cs`) which carries a Polly retry policy that also retries HTTP 429 and 403 (treated as rate limiting).

## Domain model

- A hut's `Source` column selects the crawler: `"AV"` = Alpine clubs' central booking system (`hut-reservation.org`, `FetchDataFunctions/Helpers.cs` URLs); `"HuettenHoliday"` = huetten-holiday.com (`FetchDataFunctions/Functions/HuettenHoliday/`).
- Provider URLs, the test-hut exclusion list, and geocoding helpers (OSM Nominatim + Azure Maps) live in `FetchDataFunctions/Helpers.cs`.
- **Availability ↔ bed categories (subtle, spans both apps):** `Availability` carries two AV category ids — `BedCategoryId` (the per-hut `categoryID`, large/hut-specific) and `TenantBedCategoryId` (the tenant-level `tenantBedCategoryId`, small/shared). The **website joins `Availability.TenantBedCategoryId → dbo.BedCategories.id`** for the display name; `BedCategories` is a lookup keyed by `tenantBedCategoryId`, and `SharesNameWithBedCateogryId` groups synonyms onto a canonical name. A missing lookup row *hides a hut's availability entirely*, so the crawler self-maintains it via `EnsureBedCategoriesExist` (`UpdateAvailabilityFunctions.cs`) and the two website availability endpoints LEFT JOIN it defensively. "Hut closed" days are encoded as `BedCategoryId = -1`, `TenantBedCategoryId = NULL`.
- The DB enforces **no** FK from `Availability` to `BedCategories` (only `FK_Huts_Availability` and the `BedCategories` self-ref `FK_BedCategory_SameAs`). `Hut.Id` and `BedCategory.Id` are non-identity keys set explicitly — configure new such entities with EF `ValueGeneratedNever()` (see `AlpinehutsDbContext.cs`).

## Frontend conventions

- Backend services (`website/src/services/*-service.js`) are registered as global properties (`$HutService`, `$AvailabilityService`, `$NotificationService`, etc.) in `main.js` and reach the API via `window.API_URL`. `main.js` is bootstrap-only; the router (`src/router/`, with **lazy-loaded** route components), i18n (`src/i18n/`), and the cross-component event bus (`src/event-bus.js`) are separate modules.
- Runtime config is injected through `window.*` globals in `website/public/config.js`, which is **replaced at deploy time**; the SWA workflow rewrites `window.VERSION_LABEL` with the short commit SHA.
- i18n via `vue-i18n` with locale `de` and fallback `en` — add every user-facing string to both message files (`website/src/i18n/en.js` and `website/src/i18n/de.js`).
- Routing/auth: `website/staticwebapp.config.json` gates `PUT`/`DELETE /api/huts/*` to the `admin` role and redirects 401s to AAD login.

## CI/CD

- `.github/workflows/main_alpinehutscrawler.yml`: deploys `FetchDataFunctions` to the prod Flex app **`alpinehutscrawler-prod`** (France Central) on push to **`main`** (paths `FetchDataFunctions/**`), via **OIDC** (least-privilege app registration `alpinehuts-crawler-prod-deploy`, secrets `AZURE_PROD_*`).
- `.github/workflows/dev_alpinehutscrawler.yml`: deploys `FetchDataFunctions` to the dev Flex app **`alpinehutscrawler-dev`** on push to **`dev`**, via OIDC (`alpinehuts-crawler-dev-deploy`, secrets `AZURE_DEV_*`). Both workflows delete any root `global.json` so `setup-dotnet` (10.0.x) drives the SDK.
- `.github/workflows/azure-static-web-apps-white-pond-0976e3603.yml`: builds/deploys `website` + `WebsiteBackendFunctions` on push to `main` and `dev`, then runs the `e2e_smoke` job (Playwright, see Testing) against the deployed environment as a final gate.
- **`dev` is the integration branch** (default target for Dependabot PRs); `main` is production. Open PRs against `dev`.
- Pushing to `dev` auto-deploys to a **SWA staging slot** and auto-runs the E2E smoke suite against it; the Action build log also prints the preview URL for manual checks.
