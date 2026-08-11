---
name: run-local
description: Start the admin-portal Angular app locally against the backend, or run its lint/test/e2e targets. Use when asked to run, preview, or verify a change in the frontend.
---

# Running the frontend locally

## Dev server

```bash
npx nx serve admin-portal
```

Serves at `http://localhost:4200`. The backend ApiGateway (see `sohoa.backend`) allows CORS from `localhost:4200`/`4201` — no proxy config needed as long as the backend is reachable and its gateway URL matches what's in `apps/admin-portal/src/environments/environment.ts` (`@env/environment` alias).

The backend must be running separately (see `sohoa.backend`'s `run-local` skill: `docker compose up -d` then `dotnet run` from `Aspire/EvnHanoi.AppHost`) for any API/SignalR-backed feature to work — the frontend dev server does not mock or stub the API.

## Lint / unit test / e2e

```bash
npx nx lint admin-portal
npx nx test admin-portal
npx nx e2e admin-portal-e2e
npx nx e2e admin-portal-e2e -- <spec-file>   # single spec
```

To run a lib's own tests directly (not through the app): `npx nx test <lib-name>` where `<lib-name>` is the project name from `libs/<group>/<name>/project.json` — note this is the Nx *project name*, which may differ from the `@sohoa.frontend/<group>/<name>` import alias used in code.

## Production build (to sanity-check SSR before deploying)

```bash
npx nx build admin-portal --configuration=production
node dist/apps/admin-portal/server/server.mjs
```

Matches what the Docker image runs (`PORT=4000` by default in that image).
