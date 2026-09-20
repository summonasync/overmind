---
feature: 024
slug: feature-workspace-overview
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Spec 024 — Feature workspace rework: card header + stepper over body + Feature Overview panel

<decisions>
- Header vertical card (user sketch 2026-09-20, amended live): ← Dashboard link sits ABOVE the title (small, muted, focus ring); Title; description line `slug · updated X ago`; then the meta row [tone-styled status Select] [vN badge] [Edit] [Delete] — Delete STAYS in the meta row next to Edit exactly where it is today (user amendment: "delete кнопка остаётся там же, где сейчас. Она там будет одна") — one Delete button, no corner move, confirm modal unchanged. PageHeader is replaced by a local layout — its single-row actions shape cannot express this hierarchy.
- The status select keeps ALL 022/023 mechanics: featureStatusTone tinting, transition-aware options, done-lock, optimistic PUT + rollback — only its position changes.
- The ceremony stepper (019) renders immediately after the header, BEFORE the feature body description; body and tabs follow unchanged.
- Right panel in feature mode becomes Feature Overview: status badge (tone), version, current-pin state, tasks x/y with the progress bar idiom, spec/plan/result status chips, age in backlog (createdAt → now, "in backlog 3d / 5h"), last update (relative), Open questions — parsed from the spec body's <open-questions> section, rendered as markdown; "- none"/absent spec → "None" / "No spec yet". All from the already-loaded feature query — zero new endpoints.
- Dashboard-mode right panel untouched (only the feature-mode content changes).
- Open-questions parsing: a small pure helper extracting the section between the tags from the spec body; rendering via the shared Markdown component.
</decisions>

<open-questions>
- none (user amended the Delete placement live; everything else per the sketch)
</open-questions>

<requirements>
Web only (features/planning + the right panel):
- feature-workspace.tsx: local header layout (← Dashboard above title; slug·updated line; meta row with the status Select, vN, Edit, Delete as today); ErrorText under the header.
- Stepper reorder: header → stepper → body → tabs.
- Feature Overview panel content for feature mode: vitals per the decisions; current-pin badge only when pinned.
- Age formatting: days >24h, hours <24h; FSD-clean helper placement.
- Verify: npm run lint && npx tsc --noEmit && npm run build.
</requirements>

<acceptance-ideas>
- Open a feature: ← Dashboard above the title; meta row [● status ▾] [v1] [Edit] [Delete]; no second delete anywhere.
- Stepper between header and description.
- Right panel: tone status, x/y + bar, spec/plan/result chips, "in backlog 4d", "updated 2h ago", Open questions (or None).
- Dashboard without a feature unchanged.
</acceptance-ideas>

<log>
- 2026-09-20 — v2: user amendment — Delete stays in the meta row next to Edit (one button, no corner move); header/stepper/overview decisions unchanged.
</log>
