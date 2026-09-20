---
feature: 024
slug: feature-workspace-overview
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Result 024 — Feature workspace rework: card header + stepper over body + Feature Overview panel

<outcome>
024-feature-workspace-overview shipped: the feature workspace became a vertical "feature card" — ← Dashboard above the title, slug·updated line, a meta row [tone status Select][vN][Edit][Delete] (Delete stays in the row per the user's live amendment; one button, confirm modal unchanged; all 022/023 select mechanics intact), and the ceremony stepper moved above the description body (header → stepper → body → tabs). When a feature is open, the right panel becomes Feature Overview: tone status badge, version, current-pin, tasks x/y with the board's progress-bar idiom, spec/plan/result chips, "In backlog Xd/Xh/m" age, updated-relative, and Open questions parsed from the spec's <open-questions> section rendered as markdown (No spec yet / None / as-is). Dashboard mode untouched; zero new endpoints — everything from the already-loaded queries.
</outcome>

<evidence>
- Reviews: audits/review-024-01.md (PASS — header semantics, mechanics byte-identical, PageHeader retained in the null branch deliberately), audits/review-024-02.md (PASS — query dedup via shared keys verified, FSD via the slice public API, age edge cases pinned, progress-bar a11y byte-identical, dashboard branch untouched by diff).
- lint 0 errors / tsc 0 / build green (10/10 routes) on both tasks.
- Files: feature-workspace.tsx (local vertical header + stepper relocate), open-questions.ts (pure parser), relative-time.ts (+formatBacklogAge), feature-overview-panel.tsx (new), planning-info-panel.tsx (feature-mode branch), planning-page.tsx (prop), index.ts (public API).
</evidence>

<deferred>
- Microbatch: tone the workspace status select like the board's (parity); unify sub-minute age rounding with formatRelativeTime; the null-feature branch still uses PageHeader (unify on touch).
- Visual pass with the user — he is walking the 0.1.8 UI; this lands in the next pack.
</deferred>
