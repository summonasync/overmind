---
feature: 024
slug: feature-workspace-overview
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Plan 024 — Feature workspace rework: card header + stepper over body + Feature Overview panel

<approach>
Two web tasks: the workspace card (header hierarchy + stepper reorder — pure reshuffle of existing mechanics), then the Feature Overview panel (new presentational content in the right panel's feature mode, fed by the loaded feature + a tiny open-questions parser). Closeout rides the orchestrator.
</approach>

<contract>
- Header: local layout replacing PageHeader — ← Dashboard (above title, small muted) / Title / slug·updated / meta row [tone Select][vN][Edit][Delete as today]; all 022/023 select mechanics intact.
- Order: header → stepper → body → tabs.
- Right panel feature mode: Feature Overview (tone status, version, pin, tasks x/y + bar, spec/plan/result chips, backlog age d/h, updated relative, Open questions markdown from the spec's <open-questions> or None/No spec yet).
- No backend, no new endpoints.
</contract>

<tasks>
- [ ] 01-workspace-card-header — header hierarchy + Delete stays + stepper above body + verification → tasks/01-workspace-card-header.md
- [ ] 02-feature-overview-panel — right panel feature-mode content + open-questions parser + verification + closeout (orchestrator) → tasks/02-feature-overview-panel.md
</tasks>

<risks>
- The right panel lives in dashboard chrome (widgets/info-panel territory) — feature-mode branching must not disturb the dashboard overview; keep the switch at the panel's composition point.
- The workspace file is hot (021/022/023/019 all touched it) — surgical edits, current file state is the base.
</risks>

<verification>
- lint + tsc + build green; visual sanity lands with the user's live pass (he is walking the UI now).
</verification>
