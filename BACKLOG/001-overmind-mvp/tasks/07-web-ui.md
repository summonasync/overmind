---
feature: 001
task: 07
slug: web-ui
status: todo
review: light
created: 2026-09-06
---

# 001/07 — Web UI

<goal>
A Next.js web UI (served by Overmind.Host as static files) provides the full management surface: Projects, Entries (per type), Kits, Search, Settings. It talks only to the local HTTP API and shows a degraded-mode banner when vector search is down.
</goal>

<context>
Spec R7: tray app + web UI, browser is the window. Per global frontend conventions: Next.js 16 App Router, React 19, TypeScript strict, Tailwind 4, React Query v5 for server state, FSD layout, Server Actions for mutations, BFF rule (client components never call the API directly — Next.js route handlers proxy to the .NET API). UI is statically exported (`output: 'export'`) and served by Kestrel; since static export has no runtime server, the BFF proxy constraint is relaxed for v1: the SPA calls the local API directly at runtime (same machine, URL from `window.location.origin` — the Host serves both). Record this deliberate deviation in this file's <log> when executing.
</context>

<scope>
`web/`:
- Next.js 16 scaffold (App Router, TypeScript strict, Tailwind 4), `output: 'export'`, FSD folders: `app/`, `pages/` (FSD pages layer), `widgets/`, `features/`, `shared/`.
- `shared/api/client.ts` — typed fetch client targeting `window.location.origin` (Host serves SPA + API on one origin).
- Query keys via factory pattern in `features/<domain>/queries.ts`.
- Pages:
  - Projects — list/create/edit/delete; disk path picker = plain text input (native picker is P2+).
  - Entries — per project; filter by type (note/decision/gate/guard/skill) and tag; markdown editor (plain textarea + preview is enough); skill entries edit per-platform file tabs.
  - Kits — CRUD, add/remove entries, link to projects, activate/deactivate buttons, drift view per project.
  - Search — query box, mode toggle (FTS/semantic, semantic disabled with tooltip when degraded), results with snippets.
  - Settings — embedding provider select (onnx/ollama/openai), provider URLs, API key (write-only field), Qdrant endpoint, reindex button.
- Degraded banner — polls `/api/health` via React Query (30s refetch), shows "Vector search unavailable — full-text only" when down.
- Mutations: plain fetch mutations + `queryClient.invalidateQueries` (no Server Actions in static export — deviation noted above).

Host wiring (`src/Overmind.Host/`):
- Serve `web/out/` via static files with SPA fallback to `index.html`; `web/out` path resolved relative to app content root, overridable via `OVERMIND_WEB_ROOT`.

Verification: `npm run lint && npx tsc --noEmit && npm run build` clean; manual smoke — Host serves the UI at `/` and the Projects page lists API data.
</scope>

<anti-scope>
- No auth screens, no user management.
- No native file picker, no drag-drop.
- No dark/light theme system beyond a single sensible default (brand/design system is a separate feature).
- No E2E test framework (Playwright etc.) — manual smoke only in MVP.
- No changes to API contracts; if a needed endpoint/shape is missing, stop and report.
</anti-scope>

<constraints>
- TypeScript strict, no `any`.
- No `useEffect` for data fetching — React Query only; `useEffect` allowed for browser APIs only.
- Centralized query keys (factory pattern).
- No new npm dependencies beyond Next/React/Tailwind/React Query without asking the user first.
- All UI text in English.
</constraints>

<acceptance-criteria>
- [ ] `npm run lint && npx tsc --noEmit && npm run build` all clean; `web/out/` produced.
- [ ] Host serves the exported SPA at `/` (manual or integration check).
- [ ] All five pages functional against the real API: full CRUD roundtrips in Projects, Entries, Kits; search returns results; settings persist.
- [ ] Degraded banner appears when Qdrant is down and semantic mode is disabled with explanation.
- [ ] Kit activation from the Kits page results in materialized files on disk (verified via API/drift view).
</acceptance-criteria>

<refs>
- @../plan.md — task 07
- @../spec.md — R7, decisions/ui
- tasks/03-core-api.md — API surface (must be done first)
- tasks/04-skill-kits-materialization.md — kits endpoints consumed here
- tasks/05-embeddings-qdrant.md — settings endpoints consumed here
</refs>

<obstacles>
<!-- Filled by executors during work -->
</obstacles>

<log>
2026-09-06 — writing-plans — task created from approved spec.
</log>
