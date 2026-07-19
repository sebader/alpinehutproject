# FetchDataFunctions — crawler infrastructure (high level)

`FetchDataFunctions` is the timer-triggered crawler that scrapes hut/availability data into Azure SQL.
It runs as an isolated **.NET 10** Azure Functions app on **Flex Consumption** in **France Central**, using
**Durable Functions** with the **Durable Task Scheduler (DTS)** managed backend.

## Environments

Two independent Function Apps (Flex Consumption doesn't support deployment slots), both fed from this repo:

| | Dev | Prod |
|---|---|---|
| Function App | `alpinehutscrawler-dev` | `alpinehutscrawler-prod` |
| Deploys on push to | `dev` | `main` |
| Workflow | `.github/workflows/dev_alpinehutscrawler.yml` | `.github/workflows/main_alpinehutscrawler.yml` |
| Durable Task Scheduler | `alpinehuts-dts-dev` (task hub `crawler`) | `alpinehuts-dts-prod` (task hub `crawler`) |
| Storage account | `sthutscrawlerdev` | `sthutscrawlerprod` |
| SQL database | `alpinehutsdb_dev` | `alpinehutsdb` |
| Crawl cadence | weekly (Mondays) | daily |

## Key design points

- **Region**: France Central (cheapest European region for Flex compute + DTS; SQL is in West Europe, low latency).
- **Durable backend**: Durable Task Scheduler (`azureManaged` provider). Task-hub name and connection string come
  from the app settings `TASKHUB_NAME` and `DURABLE_TASK_SCHEDULER_CONNECTION_STRING` (referenced by `host.json`
  via `%TASKHUB_NAME%`). Replaced the previous Netherite/Event Hubs backend.
- **Identity**: a single shared user-assigned managed identity (`alpinehutsidentity`) is used by both apps for
  Azure Maps, DTS (role *Durable Task Data Contributor*), and storage.
- **Storage**: ZRS, **shared-key access disabled**. `AzureWebJobsStorage` and the Flex deployment container both
  use the managed identity (`AzureWebJobsStorage__accountName` + `__credential=managedidentity`).
- **Timer schedules** are externalized to app settings (`HutsUpdateSchedule`, `AvailabilityUpdateSchedule`,
  `HuettenHolidayAvailabilityUpdateSchedule`, `CleanupSchedule`) so dev can run weekly while prod runs daily.
- **CI auth**: OIDC via dedicated least-privilege app registrations
  (`alpinehuts-crawler-dev-deploy` / `alpinehuts-crawler-prod-deploy`), each *Contributor* on only its own app.
- **SQL access** uses SQL authentication (connection string), not the managed identity.

> The exact provisioning commands are kept out of the repo. The dev database is a copy of prod with the
> availability data and notification subscriptions removed.
