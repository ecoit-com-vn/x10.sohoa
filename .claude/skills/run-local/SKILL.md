---
name: run-local
description: Start the EvnHanoi.Digitization backend locally (infra containers + Aspire AppHost, or a single microservice standalone). Use when asked to run, start, debug, or test the backend against a live environment.
---

# Running the backend locally

## 1. Start infra dependencies (Oracle, Redis, RabbitMQ, Elasticsearch, MinIO)

```bash
docker compose up -d
```

Run from the `sohoa.backend/` root. This only starts infrastructure containers — no application code. Wait for Oracle to report healthy before starting services (it's the slowest to boot); check with `docker compose ps`.

## 2. Start all services via Aspire (preferred)

```bash
cd Aspire/EvnHanoi.AppHost
dotnet run
```

This is the correct way to run the *whole* backend — it wires up service discovery, injects shared config (DB/Redis/RabbitMQ/Elasticsearch/JWT/MinIO connection info) via `BackendConfigurationExtensions`, and opens the Aspire dashboard (URL printed in the console) where you can see logs/traces/env vars per service and click through to each service's Scalar API docs page.

Before the first run (or after changing `Aspire/EvnHanoi.AppHost/appsettings.Development.json`), a build automatically re-syncs that file into every service project via the `SyncDevAppSettings` MSBuild target — no manual copying needed.

## 3. Run a single microservice standalone

Useful when iterating on one service without booting everything:

```bash
cd Microservices/<ServiceName>
dotnet run
```

Valid `<ServiceName>` values: `IdentityService`, `EquipmentService`, `DigitizationService`, `NotificationService`, `ReportService`, `SyncService`, `WorkflowService`. This skips Aspire service-discovery wiring, so any service it calls synchronously (e.g. via `WithReference` in the AppHost) must already be reachable at the URL configured in `appsettings.Development.json` — check that file if the service fails to start or throws connection errors.

## 4. Health checks

Every service exposes `/health` and `/alive` (from `EvnHanoi.ServiceDefaults`) — use these to confirm a service is actually up before hitting its API.

## Do not use for this

`Start-AllServices.ps1` / `Start-Headless.ps1` / `Start-E2EServices.ps1` launch every service as a separate `dotnet run` process outside Aspire. They currently contain hardcoded personal log paths from another tool's session and are not portable — prefer the Aspire AppHost (step 2) unless you have a specific reason to avoid Aspire.
