# Technical Solutions

Architecture Decision Records. Newest first. Once Accepted, changing a decision requires a new ADR that supersedes the old one — never a silent edit.

## Frontend data access: React Query + same-origin fetch from a static-export SPA — 2026-09-07
**Status:** Accepted (supersedes the global Server-Actions/BFF default for this project)
**Context:** The global frontend conventions mandate Next.js Server Actions for mutations and forbid direct `fetch` from client components, routing everything through a Next.js backend (BFF). The MVP web UI is built with `output: "export"` and served as static files by the Kestrel host (spec R1.1/R7.1): in production there is no Next.js server at all, so Server Actions and route handlers are impossible by construction. Surfaced during the 001 hard architect review (`audits/architect-review-2026-09-07.md`) as an undocumented deviation.
**Decision:** All server access in `web/` goes through React Query (`useQuery`/`useMutation`) over the single `shared/api/client.ts` fetch wrapper, calling the same-origin `/api/*` of the hosting Kestrel process. The BFF role is filled by same-origin static hosting (the SPA and the API share origin, port, and loopback security boundary), not by Next.js server functions. Query-key factories stay centralized in `features/*/queries.ts`; no `useEffect` data fetching.
**Alternatives considered:** Run a Next.js server alongside Kestrel to enable Server Actions/BFF — rejected: a second runtime contradicts the single-process download-and-run product decision. Call the API via hand-rolled hooks without React Query — rejected: loses caching/invalidation discipline already established.
**Consequences:** The global Server-Actions rule does not apply to this repo. Anything requiring a Next server (SSR, route handlers, middleware) is off the table while the SPA is static-export. Navigation uses plain `<a href>` (the Next 16 static-export client router is broken upstream); revisit if upstream fixes land.

## Backend API layering: vertical-slice minimal APIs with EF Core in handlers — 2026-09-07
**Status:** Accepted
**Context:** Every Host endpoint module (`ProjectsApi`, `EntriesApi`, `KitsApi`, `SearchApi`, `SettingsApi`, `HealthApi`, `SystemApi`) injects `OvermindDbContext` directly into minimal-API handlers, with validators and mappers beside them. This diverges from the global default ("split API/DTO/Service, Clean Architecture") but is consistent, well-tested, and proportionate for a single-write-path MVP. Recorded during the 001 hard architect review (`audits/architect-review-2026-09-07.md`, WARNING-2). Live size example: `KitsApi.cs` at 288 lines is the largest module and near the extraction trigger.
**Decision:** Sanction the vertical-slice style for v1: one static class per bounded context under `Host/Api/`, handlers own their EF queries, domain invariants live on entities where practical. **Extraction trigger:** (a) a second write path for the same aggregate appears (e.g. P2 MCP tools writing beyond note/decision/gate/guard, background reconcilers), or (b) any API module exceeds ~300 lines. On trigger, extract application services per bounded context, starting with kit activation (the single-active-kit invariant is endpoint-only today — review WARNING-1).
**Alternatives considered:** Extract application services now (Mediator/handlers per use case) — rejected: indirection without a second consumer at MVP size; the trigger-based rule defers the cost until a real force exists. Keep the style without an ADR — rejected: reviewers and agents need to know the pattern is intentional, not drift.
**Consequences:** New endpoints follow the existing slice pattern (contract + validator + mapper + handler). The single-active-kit invariant and similar endpoint-only rules must be re-enforced by any new write path until extracted. The ~300-line ceiling is checked at each architect review.

## Custom MCP resource scheme is `overmind://` — 2026-09-07
**Status:** Accepted (supersedes the original `ctx://` scheme decision from spec R2.2 era)
**Context:** The custom MCP resource URI scheme was originally `ctx://`. It is generic enough to collide with other MCP servers a user has installed, and does not identify the product. The rename window is ideal: MVP stage, no external consumers, and materialized agent instructions referencing the scheme live only in the user's own projects (re-materialization fixes drift).
**Decision:** The custom MCP resource URI scheme is `overmind://` (e.g. `overmind://projects/<id>/gates`). All code, tests, and docs reference the new scheme. Supersedes the original `ctx://` decision.
**Alternatives considered:** Keep `ctx://` — rejected: collision risk with third-party MCP servers, poor self-documentation. Use a registered IANA scheme — rejected: unnecessary for an in-process MCP resource identifier.
**Consequences:** Previously materialized bootstrap files referencing `ctx://` drift until re-materialization; no migration is shipped for the same reason (drift + re-materialization covers it).

## Single active kit per project — 2026-09-06
**Status:** Accepted
**Context:** The data model (`ProjectKit` many-to-many with `IsActive`) technically allows multiple active kits per project, but the materializer emits one `overmind-bootstrap/SKILL.md` per platform, so a second kit activation on the same platform collides (409 OverwriteBlocked). Surfaced during task 04 hard review (architect IMPORTANT-1). The frozen spec already implies switch semantics: "switching a kit re-materializes files".
**Decision:** Exactly one kit may be active per project. `Activate` with another kit already active returns 409 instructing to deactivate first; the UI offers a "switch" flow (deactivate + activate). No per-kit bootstrap namespacing.
**Alternatives considered:** Multi-kit with `overmind-bootstrap-<kit-slug>/` namespacing — rejected: bigger conflict/drift surface, marginal v1 value.
**Consequences:** Simpler materialization and UI. Revisiting multi-kit = new ADR + bootstrap namespacing work.

## Project DiskPath is immutable while a kit is active — 2026-09-06
**Status:** Accepted
**Context:** `MaterializedFile` rows store relative paths; dematerialize resolves them under the project's current `DiskPath`. Changing `DiskPath` while files are materialized would make deactivate delete foreign files at matching relative paths in the new directory (task 04 review, blocker B1).
**Decision:** `PUT /api/projects/{id}` rejects `DiskPath` changes with 409 while any kit is active or any `MaterializedFile` rows exist; the user deactivates kits first.
**Alternatives considered:** Persist absolute path snapshot per `MaterializedFile` and refuse deletes on mismatch — rejected for v1 as a heavier invariant (extra migration, snapshot semantics) where a state guard achieves the same safety.
**Consequences:** DiskPath retarget requires an explicit deactivate step — visible, safe, and self-explanatory via the 409 message.

## Host↔MCP contract is unversioned, additive-only — 2026-09-06
**Status:** Accepted
**Context:** The stdio MCP adapter deserializes Host API DTOs via tolerant `JsonElement` reads. Both ship from the same repo at the same version, but the bundled-installer feature (003) makes divergent deployments conceivable. Adapter writes are restricted to note/decision/gate/guard in v1 — skill entries carry files and are managed via UI until P2 runtime delivery.
**Decision:** The Host API contract consumed by the adapter evolves additive-only: new fields allowed, renames/removals forbidden. No version negotiation in v1.
**Alternatives considered:** Explicit contract versioning / shared contracts package — rejected for v1 as overhead in a single-repo, same-version ship.
**Consequences:** Renames in Host DTOs silently break the adapter (known hazard, tolerated while single-repo); revisit when 003 introduces separate distribution.
