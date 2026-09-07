---
feature: 001
task: 07
slug: web-ui
status: done
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
- Next 16 auto-detects any `pages/`/`src/pages` directory as the legacy Pages Router root: every `.ts`/`.tsx` file there is type-validated as a page module and built as a route, so a literal FSD `pages/` layer breaks `next build`. Workaround (standard FSD trick): `pageExtensions: ["page.tsx", "page.ts", "layout.tsx", "layout.ts"]` in `next.config.ts` + app-router files renamed to the `*.page.tsx`/`layout.page.tsx` suffix convention. The FSD layer keeps the mandated `pages/` name; its files simply don't match the page-extension suffix.
- `eslint-config-next` v16 no longer works through `FlatCompat` ("Converting circular structure to JSON"); it exports flat config arrays directly (`eslint-config-next/core-web-vitals`, `eslint-config-next/typescript`), which `eslint.config.mjs` consumes as-is. `next lint` is gone in Next 16, so the `lint` script runs `eslint .` directly.
- Task scope mentions a "Qdrant endpoint" field on the Settings page, but the Settings API contract (`SettingsContracts.cs`) exposes only embedding provider, Ollama/OpenAI base URLs and the OpenAI key flag — no Qdrant endpoint. Per anti-scope ("no changes to API contracts") the field was omitted; everything else in the Settings contract is implemented.
- npm dependency `clsx` was avoided (not in the allowed list): `shared/lib/cn.ts` is a 3-line local join helper.
- 2026-09-07 — Next 16.3.4 static export (`output: "export"`) breaks client-side router navigation: clicking `next/link` silently no-ops (no pushState, no fetch, no error). Verified identical failure when serving `web/out` via both the .NET Host and a plain Node static server, while the same code navigates fine under `next dev`. All RSC payload files serve 200, payloads consistent, single build — upstream breakage. Workaround: MPA (full-document) navigation via plain `<a href>` anchors + `window.location.assign` (all `next/link`/`router.push` page navigations converted); the Host's per-route `.html` serving (`/entries` → `entries.html` in `src/Overmind.Host/Program.cs`) makes this work. Side effect: React Query cache resets per navigation — acceptable for a localhost tool. SPA nav to be revisited when upstream fixes; `web/build/adapter.js` flattening can be dropped then too if obsolete.
</obstacles>

<log>
2026-09-06 — writing-plans — task created from approved spec.
2026-09-07 — next-js-coder — built the full web UI. Scaffolded `web/` from scratch: Next.js 16.3.4 (App Router, `output: "export"`), React 19.2, TypeScript strict, Tailwind 4.3, @tanstack/react-query 5.102; FSD layers `src/app`, `src/pages`, `src/widgets`, `src/features` (health, projects, entries, kits, search, settings), `src/shared` (typed fetch client targeting `window.location.origin`, query-key factories per domain, hand-rolled UI primitives, dependency-free safe markdown renderer for the entry editor preview). Five pages: Projects (CRUD), Entries (per project via `/entries?project=`, type/tag filters, markdown textarea + preview, skill entries with per-platform file tabs), Kits (CRUD, add/remove entries, activate/deactivate with materialization report, per-project drift view), Search (FTS/semantic toggle — semantic disabled with explanation when degraded, results with snippets), Settings (provider select onnx/ollama/openai, provider URLs, write-only API key, reindex button). Degraded banner polls `/api/health` via React Query with 30 s refetch. Mutations are React Query `useMutation` + `invalidateQueries` (no Server Actions possible in a static export — see deviation note below). Deliberate deviation recorded per task context: static export has no runtime server, so the BFF rule is relaxed for v1 — the SPA calls the local API directly at `window.location.origin`; the Host serves both UI and API on one origin. Host wiring added in `src/Overmind.Host/Program.cs`: `web/out` served via `PhysicalFileProvider` (default files + static files + `MapFallbackToFile("index.html")` for SPA deep links), path resolved relative to content root (`../../web/out`), overridable via `OVERMIND_WEB_ROOT` env var; logs and continues without UI if the folder is absent. Qdrant-endpoint settings field omitted (no such API contract — see obstacles). Verification: `npm run lint`, `npx tsc --noEmit`, `npm run build` all clean, `web/out/` produced (routes /, /entries, /kits, /search, /settings); `dotnet build` clean (0 warnings/0 errors); smoke via `dotnet run`: `/` serves the SPA (200 text/html), `/entries` deep link falls back to index.html, `_next` assets load, `/api/health` returns vectorSearch=down (Qdrant not running locally → degraded banner path exercised), and a full API roundtrip mirroring the UI flows passed — project/entry/kit CRUD, FTS search hit, kit activation materialized files on disk under the project (`E:/temp/overmind-smoke/.claude/skills/...`), drift empty after activation, settings GET/PUT roundtrip. Host process killed after the smoke run. No git commit made.
2026-09-07 — design phase: user design assets in design/ (mockups, Zerg palette sheets, sprites); brand-book created at web/brand-book.md (dark-only Zerg dev-tool: strict Linear-style admin on violet palette, background art, Kerrigan sidebar strip, meme zerlings as reaction mascots, Lucide icons). Zerling sprites + brand marks extracted to web/src/shared/assets/.
2026-09-07 — restyle applied per brand-book (design tokens, app-shell, info panels, zerlings, entry-type styling). Review verdict: PASS WITH NITS; MAJOR (KitDetailPanel god-component split into entries/activation/drift sections) + 8 minors fixed (Esc modal close, real ⌘K hotkey, tab focus rings, banner dedup, semantic markdown headings, single primary per view, removed non-reactive Recent Queries, search error semantics). Remaining minors deferred to BACKLOG.md hardening.
</log>
