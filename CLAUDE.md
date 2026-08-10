# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

Build/test the whole solution:
```
dotnet build EvnHanoi.Digitization.slnx
dotnet test EvnHanoi.Digitization.slnx --configuration Release
```
Run a single test project or filter by name:
```
dotnet test Microservices/<ServiceName>/<TestProject>.csproj
dotnet test EvnHanoi.Digitization.slnx --filter "FullyQualifiedName~SomeTestClass"
```

Run everything locally via Aspire (preferred — handles service discovery/config/env vars):
```
cd Aspire/EvnHanoi.AppHost
dotnet run
```
This starts every microservice + the ApiGateway with proper `WithReference`/service-discovery wiring, plus the Aspire dashboard.

Run infra dependencies only (Oracle, Redis, RabbitMQ, Elasticsearch, MinIO) without touching app code:
```
docker compose up -d
```

Run services individually without Aspire (`Start-AllServices.ps1` / `Start-Headless.ps1`): these loop over each `Microservices/<Name>` and `ApiGateway/<Name>` folder and `dotnet run` them as separate processes. **Before reusing these scripts, strip the hardcoded personal log paths** (`C:\Users\...\.gemini\antigravity\...`) left over from another tool's session — they are not portable across machines.

Note: `Directory.Build.targets` auto-copies `Aspire/EvnHanoi.AppHost/appsettings.Development.json` into each web project before build (`SyncDevAppSettings` target) — that's the single source of truth for local dev settings, not per-service `appsettings.Development.json` files.

## Architecture

Solution: `EvnHanoi.Digitization.slnx`, four solution folders:

- **`Aspire/`** — `EvnHanoi.AppHost` (orchestrator: registers every service/gateway via `AddProject<Projects.X>`, injects shared infra env vars via `BackendConfigurationExtensions` — DB, Redis, RabbitMQ, Elasticsearch, JWT, MinIO, and inter-service URLs) and `EvnHanoi.ServiceDefaults` (shared OpenTelemetry, `/health`+`/alive` checks, service discovery, and a resilience HttpClient handler tuned with long (10–22 min) timeouts for LLM/OCR calls).
- **`ApiGateway/EvnHanoi.ApiGateway`** — YARP reverse proxy (`AddReverseProxy().LoadFromConfig()` + service-discovery destination resolver). Route table lives in `appsettings.json`. CORS allows `localhost:4200`/`4201` (frontend dev). Custom middleware normalizes trailing slashes on `/api/*` and `/api/v1/*`.
- **`BuildingBlocks/`**:
  - `EvnHanoi.Core` — placeholder scaffold, not yet built out.
  - `EvnHanoi.Infrastructure` — the real shared library: `Audit` (RabbitMQ audit event publishing + action filters), `Database` (Dapper extensions, DbUp-style migration runner, Oracle CLOB handling), `Logging` (Serilog), `Messaging` (RabbitMQ topic topologies/events), `Migrations` (per-service SQL migrations/seeds), `Security` (JWT + custom dynamic permission/RBAC — `DynamicPermissionFilter`, `PermissionDiscoveryService`, `TokenRelayHandler`).
- **`Microservices/`** — seven independently-runnable services, each an Aspire-managed ASP.NET Core project:

  | Service | Responsibility |
  |---|---|
  | `IdentityService` | Auth/RBAC — users, roles, permission groups, org units, menus |
  | `EquipmentService` | Equipment/asset data (Elasticsearch, MinIO) |
  | `DigitizationService` | Core OCR/AI pipeline — PDF processing (`PdfSharpCore`), background `OcrWorker`/`ExtractionWorker` calling external OCR-VL/LLM servers |
  | `NotificationService` | Notifications, Quartz-scheduled jobs, Elasticsearch, RedLock distributed locking |
  | `ReportService` | Reporting (MinIO, Dapper) |
  | `SyncService` | External sync with the "Pmis" system (Polly resilience, Redis lock, Quartz) |
  | `WorkflowService` | Workflow/approval engine (Dapper, custom Guid type handler) |

### Cross-cutting conventions

- **Data access**: Dapper everywhere, not EF Core. Migrations run via a DbUp-style helper (`Database/DatabaseMigrationHelper` in `EvnHanoi.Infrastructure`) against per-service SQL files under `BuildingBlocks/EvnHanoi.Infrastructure/Migrations/`.
- **Database**: Oracle (`Data Source=host:1521/orcl`) — watch for Oracle-specific CLOB parameter handling when adding queries.
- **Messaging**: raw `RabbitMQ.Client` (no MassTransit) — each service opens its own `IConnection`; topic topologies are defined per-domain in `Messaging/`. Used for both domain events and audit trail events.
- **Auth**: JWT bearer + a custom dynamic permission system, not IdentityServer/Keycloak. New endpoints needing authorization should go through `DynamicPermissionFilter`.
- **API docs**: `AddOpenApi()` + Scalar (`MapScalarApiReference()`), dev-only — this is not Swashbuckle/Swagger UI.
- **Containers**: no hand-written per-service Dockerfiles — images are built via the `aspirate` CLI driven from the Aspire AppHost manifest (`aspirate build --non-interactive` in CI), not `docker build`.
- **Scheduling**: Quartz.NET (`NotificationService`, `SyncService`); HTTP resilience via Polly / `AddStandardResilienceHandler`.

### CI/CD (`.gitlab-ci.yml`, triggers on `staging` branch only)

`test` → `dotnet test EvnHanoi.Digitization.slnx --configuration Release`
`build` → installs Aspirate + Aspire workload, `aspirate build --non-interactive` from `Aspire/EvnHanoi.AppHost`, pushes each service image to `harbor.ecoit.vn/x10/backend/<service>:latest`
`deploy` → `kubectl apply -k` on `Aspire/EvnHanoi.AppHost/overlays/production`, then `kubectl rollout restart deployment/<service> -n x10`
`backup` → mirrors the repo to a GitHub backup remote

### Non-source clutter (ignore when documenting/refactoring)

`pdf_content.txt` (extracted RFP text dump), `temp.cs` (scratch reflection snippet), `scratch/` (ad-hoc `MigrationRunner`/`temp_oracle_test` console projects for manual Oracle testing), `Microservices/test_rabbitmq.py` (manual RabbitMQ test script) — none are part of the actual build.
