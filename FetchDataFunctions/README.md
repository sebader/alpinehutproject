# FetchDataFunctions — crawler infrastructure (high level)

`FetchDataFunctions` is the timer-triggered crawler that scrapes hut/availability data into Azure SQL.
It runs as an isolated **.NET 10** Azure Functions app on **Flex Consumption** in **France Central**, using
**Durable Functions** with the **Azure Storage** backend (the only BYO durable backend supported on Flex
Consumption). Orchestration state lives in the app's own storage account via an identity-based connection.

## Environments

Two independent Function Apps (Flex Consumption doesn't support deployment slots), both fed from this repo:

| | Dev | Prod |
|---|---|---|
| Function App | `alpinehutscrawler-dev` | `alpinehutscrawler-prod` |
| Deploys on push to | `dev` | `main` |
| Workflow | `.github/workflows/dev_alpinehutscrawler.yml` | `.github/workflows/main_alpinehutscrawler.yml` |
| Storage account | `sthutscrawlerdev` | `sthutscrawlerprod` |
| SQL database | `alpinehutsdb_dev` | `alpinehutsdb` |
| Crawl cadence | weekly (Mondays) | daily |

The durable orchestration state (task hub `crawler`) lives in the same storage account as `AzureWebJobsStorage`.

## Key design points

- **Region**: France Central (cheapest European region for Flex compute; SQL is in West Europe, low latency).
- **Durable backend**: **Azure Storage** (the default provider — the only BYO durable backend supported on Flex
  Consumption). `host.json` sets only the `hubName` (from `%TASKHUB_NAME%`, value `crawler`); the provider reuses
  the `AzureWebJobsStorage` identity-based connection, so orchestration state (queues/blobs/tables) lives in the
  app's own storage account. Replaced the previous Durable Task Scheduler backend, which billed per action and was
  far too expensive at this crawl volume (millions of actions/month).
- **Identity**: a single shared user-assigned managed identity (`alpinehutsidentity`) is used by both apps for
  Azure Maps and storage. For the durable backend it needs **Storage Blob + Queue + Table Data Contributor** on
  the storage account.
- **Storage**: ZRS, **shared-key access disabled**. `AzureWebJobsStorage`, the durable backend, and the Flex
  deployment container all use the managed identity (`AzureWebJobsStorage__accountName` + `__credential=managedidentity`).
- **Timer schedules** are externalized to app settings (`HutsUpdateSchedule`, `AvailabilityUpdateSchedule`,
  `HuettenHolidayAvailabilityUpdateSchedule`, `CleanupSchedule`) so dev can run weekly while prod runs daily.
- **CI auth**: OIDC via dedicated least-privilege app registrations
  (`alpinehuts-crawler-dev-deploy` / `alpinehuts-crawler-prod-deploy`), each *Contributor* on only its own app.
- **SQL access** uses SQL authentication (connection string), not the managed identity.

> The exact provisioning commands are kept out of the repo. The dev database is a copy of prod with the
> availability data and notification subscriptions removed.
