# STRUCTURE — Project Layout & Conventions

How the repo is organized and the rules for writing code in it. System design lives in `CONTEXT.md`, decision rationale in `TECHNICAL-SOLUTIONS.md`, work tracking in `BACKLOG.md` + `BACKLOG/`.

## Repository layout

```
├── CONTEXT.md / STRUCTURE.md / TECHNICAL-SOLUTIONS.md / BACKLOG.md
├── BACKLOG/                  # feature folders (nnn-slug/): feature.md, spec.md, plan.md, tasks/
├── audits/                   # skill-produced reports, named <skill>-*.md
├── src/
│   ├── Overmind.Core/        # domain: Entities/, Enums/, Abstractions/ — zero dependencies
│   ├── Overmind.Infrastructure/  # Persistence/, Search/, VectorSearch/, Embeddings/, Materialization/, Configuration/, Migrations/
│   ├── Overmind.Host/        # composition root: Program.cs, DependencyInjection.cs, Api/, tray+lifecycle at root
│   └── Overmind.Mcp/         # stdio MCP adapter, no project references
├── tests/                    # mirrors src: Overmind.Core.Tests, .Infrastructure.Tests, .Api.Tests (Host), .Mcp.Tests
└── web/                      # Next.js 16 static-export SPA (FSD)
```

Central package management: `Directory.Packages.props`; shared build props: `Directory.Build.props`. Pin new package versions centrally. Solution: `Overmind.sln`.

## Backend architecture rules

1. **Dependency direction is one-way:** `Core ← Infrastructure ← Host`. `Overmind.Mcp` references nothing and proxies over HTTP. Never add a reverse or skip-level reference (e.g. Core → Infrastructure, Infrastructure → Host). Test projects reference only their target project.
2. **Core stays pure.** Entities, enums, abstractions, domain DTOs. No `using Microsoft.*`, no EF annotations, no serialization attributes. Domain behavior goes on entities (factory methods, invariant guards) where practical.
3. **Persistence entities live in `Core/Entities`.** (`MaterializedFile` currently sits in `Infrastructure/Materialization/` — recorded debt from the 2026-09-07 architect review, pending move or an explicit exception rule.)
4. **API style: vertical-slice minimal APIs with EF Core in handlers** — one static class per bounded context under `Host/Api/`, `Map*Api()` extension registers the routes, handlers inject `OvermindDbContext` and services directly. Sanctioned for v1 by ADR; **extraction trigger:** a second write path for the same aggregate (e.g. P2 MCP tools) or a module growing past ~300 lines → extract application services.
5. **Per-request wiring is explicit:** contracts in `Api/Contracts/`, FluentValidation validators in `Api/Validation/`, entity→DTO mapping in `Api/Mapping/`, error shaping via `HttpErrors` (ProblemDetails). All DI registration goes through `Host/DependencyInjection.cs` (`AddOvermind`).
6. **Domain invariants with an ADR must hold regardless of entry point.** Known gap: "single active kit per project" is currently enforced only in `KitsApi.Activate` (recorded in the 2026-09-07 review) — any new write path must re-enforce it or move the guard into the domain first.
7. **Disk is a managed cache.** The DB is the source of truth; materialized files on disk are derived state tracked by `MaterializedFile` rows + SHA-256 hashes. Never treat files on disk as authoritative; reconcile via drift detection.
8. **Loopback is the security boundary.** Kestrel binds `127.0.0.1` only. Localhost-only endpoints (e.g. `system/browse-folder`) must never do anything a local user shouldn't be able to do.
9. **Host↔MCP contract is additive-only** (ADR). Renames/removals in Host DTOs silently break the adapter's tolerant `JsonElement` reads — additive changes only, or update both sides in the same change.

## Backend code style

- C# 14+ / .NET 10: records for DTOs, pattern matching, `required`/init where it fits, file-scoped namespaces.
- **File per type**, filename matches the type. No `region` directives, no Hungarian notation.
- **No comments by default** — comments exist only for non-obvious decisions. Never restate the code.
- All user-facing messages, identifiers, and docs in English.
- No `async void`. Cancellation tokens flow through async APIs.
- No god-services: if a class accumulates >5 public methods, split it. Current ceiling is 3 (`SkillMaterializer`).
- FluentValidation for request validation; Shouldly + NSubstitute + xUnit for tests. Manual mapping or mappers — no AutoMapper.
- EF Core migrations in `Overmind.Infrastructure/Migrations/`, generated, never hand-edited (the FTS5 trigger SQL in the initial migration is the documented exception).

## Test conventions

- Test project per source project, same root namespace + `.Tests`. Test file per subject type (`ProjectApiTests`, `MaterializationTests`).
- API integration via WebApplicationFactory with the `Testing` environment (skips single-instance and tray). Infrastructure tests use temp SQLite files / temp dirs — no shared state between tests.
- MCP contract tests spawn the real adapter process against a stub HTTP API and pin **our** wire behavior, not the SDK's.

## Frontend structure (`web/src`, FSD)

Layers and allowed import direction — **never import upward**:

```
app        → pages, widgets, features, shared   (Next routes, providers, globals)
pages      → widgets, features, shared          (page compositions, one per route)
widgets    → features, shared                   (app-shell, kit-workspace, degraded-banner)
features   → shared                             (domain slices: entries, kits, projects, search, settings, health)
shared     → (nothing above)                    (api client, ui primitives, lib, assets)
```

- **Features must not import other features.** Known violation: `features/projects` imports `browseFolder` from `features/system` (2026-09-07 review) — `features/system` is a single stateless call that belongs in `shared/api`; fix by moving it, not by adding more cross-feature edges.
- **Public APIs:** every feature and page slice has an `index.ts` barrel; import from the slice root (`@/features/kits`), not from its internals. Widgets currently lack `index.ts` and are consumed via deep imports — recorded debt; when touching a widget, add its barrel and route imports through it.
- **Data access:** every `fetch` goes through `shared/api/client.ts` to same-origin `/api/*`. No exceptions — no direct fetch/axios anywhere else.
- **React Query pattern:** query-key factories + `queryOptions` live in `features/<domain>/queries.ts` (`entryKeys`, `kitKeys`, …). Hooks in `features/<domain>/lib/` return `{ data, isPending, error, refetch }`; mutations use `useMutation` calling the feature's `api.ts`, then `queryClient.invalidateQueries` with the factory keys. **No `useEffect` for data fetching** (browser APIs only).
- **No Server Actions, no Next server.** Static export (`output: "export"`) means the production app is fully static — see ADR "static-export SPA data access".
- **Routing/MPA quirk:** `pageExtensions` is `["page.tsx", ...]`, so route files are named `page.page.tsx`. Navigation uses **plain `<a href>`** (sidebar, mobile nav, links) — the Next 16 static-export client router is broken upstream; full-page loads between routes are the accepted behavior. Do not introduce `next/link`/`useRouter` without verifying upstream is fixed.
- `'use client'` only where interactivity/hooks require it; RSC by default. TypeScript strict, no `any`.
- Brand/design tokens: `web/brand-book.md` is the source of truth for palette, typography, and assets (`shared/assets/brand/`).

## AI agent guidelines

- Read `CONTEXT.md` (this system's design), `BACKLOG.md` (index), and the feature folder before changing anything; check `TECHNICAL-SOLUTIONS.md` before proposing an alternative to an existing pattern. Accepted ADRs are changed only by a new superseding ADR — never a silent edit.
- Complex work follows `BACKLOG/nnn-slug/` task files; executing agents read only their `tasks/NN-foo.md`; accepted spec/plan are read-only snapshots.
- No auto-commits. Report changed files; the user commits.
- New third-party packages need license vetting (OSI-approved) and central version pinning before adding.
- Structural nits discovered but not fixed get recorded (BACKLOG hardening / audit), not silently dropped or silently "fixed" mid-task.

## Do / Don't

**Do**
- Keep Core dependency-free; push framework concerns to Infrastructure/Host.
- Add new endpoints as vertical slices in the matching `Host/Api/*Api.cs` module with contract + validator + mapper.
- Return ProblemDetails via `HttpErrors`; use 409 for domain conflicts with an explanatory `detail`.
- Track every materialized file in `MaterializedFile` with a normalized-content SHA-256.
- Centralize query keys in `features/*/queries.ts`; reuse `queryOptions` across widgets.
- Update `CONTEXT.md`/`STRUCTURE.md` in the same change that alters what they describe.

**Don't**
- Don't add project references that violate `Core ← Infrastructure ← Host` or make Mcp reference anything.
- Don't put EF/serialization attributes or `using Microsoft.*` in Core.
- Don't bypass `IMaterializer` to write into project skill directories.
- Don't bind Kestrel beyond loopback, and don't remove or rename Host DTO fields consumed by the MCP adapter (additive-only contract).
- Don't fetch outside `shared/api/client.ts`, don't use `useEffect` for data, don't import across or upward between FSD slices.
- Don't add `next/link`/`useRouter` navigation while the static-export router is broken upstream.
- Don't write Server Actions or anything that assumes a Next.js server exists in production.
