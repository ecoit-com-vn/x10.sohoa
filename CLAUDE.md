# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

All real commands are Nx targets (`package.json` `scripts` is empty) — invoke via `npx nx <target> <project>`:

```
npx nx serve admin-portal                        # dev server (http://localhost:4200)
npx nx build admin-portal                        # dev build
npx nx build admin-portal --configuration=production
npx nx lint admin-portal                         # ESLint
npx nx test admin-portal                         # unit tests (Vitest, via @angular/build:unit-test)
npx nx e2e admin-portal-e2e                      # Playwright e2e
npx nx e2e admin-portal-e2e -- <spec-file>        # single e2e spec (pass-through to Playwright)
npx nx show project admin-portal                 # list all targets for the app
```

Building/testing a lib directly: `npx nx build <lib-name>` / `npx nx test <lib-name>` where `<lib-name>` matches the project name in `libs/<group>/<name>/project.json` (not the import alias).

Generators: `npx nx g @nx/angular:library libs/features/<name>` / `npx nx g @nx/angular:component ...` — follow the existing `shared/*` vs `features/*` placement convention (see Architecture).

## Architecture

Nx monorepo, npm package manager, single deployable app:

- **`apps/admin-portal`** — Angular 21 app with SSR (Express server, `server.ts`), UI via **PrimeNG 21** + PrimeFlex/PrimeIcons, i18n via `@ngx-translate/core`, real-time via `@microsoft/signalr`, workflow diagrams via `bpmn-js`. Prod entry point after build is `dist/apps/admin-portal/server/server.mjs` (port 4000 in the Docker image).
- **`apps/admin-portal-e2e`** — Playwright e2e suite, `implicitDependencies: ["admin-portal"]`.
- **`libs/shared/*`** — cross-cutting: `core` (services/guards/interceptors/interfaces/config), `layout`, `ocr-viewer`.
- **`libs/features/*`** — one lib per business domain, mirroring backend microservices: `administration`, `catalog`, `dashboard`, `digitization`, `document-management`, `dossier-management`, `equipment`, `error`, `ocr-correction`, `ocr-module`, `physical-storage`, `reports`, `search`, `workflow`.
- Every lib is imported via a path alias `@sohoa.frontend/<group>/<name>` (see `tsconfig.base.json` `paths`) mapping to its `src/index.ts` barrel — always import through the alias/barrel, not deep-relative paths across lib boundaries.
- `@nx/enforce-module-boundaries` is configured in `eslint.config.mjs` but **not actually enforced yet** — `depConstraints` currently allow any tag to depend on any tag, since no libs have been tagged. Don't assume boundary violations will be caught by lint today.
- `@env/environment` alias points at `apps/admin-portal/src/environments/environment.ts` for environment-specific config.

### Deployment (`.gitlab-ci.yml`, triggers on `staging` branch only)

`build` → Docker multi-stage build (`node:20-alpine`: `npm install` → `npx nx build admin-portal --configuration=production`; runtime stage `npm install --omit=dev`, `CMD node dist/apps/admin-portal/server/server.mjs`, `PORT=4000`), pushed to `harbor.ecoit.vn/x10/frontend:latest`.
`deploy` → `kubectl apply -f deploy/k8s-deploy.yaml` into namespace `x10`, ingress host `qlshx10.ecoit.com.vn` (routes `/api`, `/hubs` → backend `apigateway` service, `/` → this app), then waits on rollout.
`backup` → mirrors the commit to a GitHub backup remote.

### Non-source clutter (ignore when documenting/refactoring)

`test-results/` (generated Playwright run output), `gitlab_issues_result.json` (cached export of GitLab issues, not code).
