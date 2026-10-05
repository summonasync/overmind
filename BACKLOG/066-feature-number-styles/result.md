---
feature: 066
slug: feature-number-styles
status: validated
created: 2026-10-05
validated: 2026-10-05
---

# Result 066 — Feature-number styles per project (KEY-039 / #039 / plain) + вкладка project settings — один форматер для всех поверхностей

## What shipped

Per-project feature-number style with one formatter feeding every display surface:

- **Backend (T1, b17ff75):** `ProjectNumberStyle` enum (hash default=0 | key | plain) + `Project.SetNumbering` domain guard — prefix required non-empty for key (`[A-Z][A-Z0-9_]{1,15}`), non-Key styles always clear the prefix. Migration `AddProjectNumberStyle` zero-impact (INTEGER NOT NULL default 0, `HasDefaultValue`+`HasSentinel(Hash)` mirroring ClaimStaleThresholdMinutes), up+down pinned by migration tests incl. legacy-row raw-INSERT. Settings update path with composition rules: key without prefix → 400, prefix without key → 400 (no silent inert storage), legacy PUTs without number fields keep numbering untouched, key→key PUT without prefix reuses the stored one (idempotent). Unknown style → ValidationProblem naming allowed values.
- **Web (T2):** `formatFeatureNumber(slug, style, prefix)` in `shared/lib/feature-number.ts` — single formatter for all surfaces; key without valid prefix falls back to the hash form (designed + pinned, not silent). `useProjectNumbering` resolves numbering on pages/widgets layer through the `@/features/projects` barrel (zero cross-feature imports, invariant held); `ProjectNumbering` type lives in shared next to the formatter. Settings section «Feature numbers»: style select + prefix input (client validation mirrors the backend regex, maxLength 16, submit disabled while invalid) + live preview on a real board slug. Call-sites converted: card badge, task titles (board ×2, delete-dialog, dependencies panel), notifications (row + awaiting ×2), ceremonies; grep pass recorded — no local `#`-templates left (`board-search.ts` deliberately untouched: matching, not display).

## Review rounds

- T1 light review PASS-WITH-NITS 0 blockers → orchestrator fix: prefix regex single-sourced in `[GeneratedRegex] Project.NumberPrefixRegex` (repo convention), validator moved to `.Must` with explicit null-guard.
- T2 light review PASS-WITH-NITS 0 blockers → independent grep pass confirmed single-formatter coverage and zero cross-feature imports; PUT semantics match backend composition rules.
- Architect light **APPROVED**, 0 blockers, 4 informational (audits/architect-066.md): composition rules converge across stacks, prefix pattern single-sourced per stack, EF config consistent, FSD boundaries hold, all spec surfaces covered.

## Gates (re-run by orchestrator, honest)

- dotnet: build 0w/0e (clean rebuild after one MSB4166 node crash), Core 365, Infrastructure 662, Api 650, Mcp 486 — all green; format verify green (Core/Host/Infra).
- web: lint 0 errors (6 pre-existing warnings), tsc clean, node --test 182/182 (+18 new pins), next build ok.

## Decisions recorded

- Prefix pattern duplicated across stacks (C# GeneratedRegex / TS const) — cross-language, both sides pinned by tests.
- Trim desync (validator 400s `" OM "`, entity would trim) — kept: layers serve different purposes, API fails loud and closed; unify at next validator touch (ledger #232).
- Enum-string parse duplicated in endpoint vs validator — fine for three values (architect I-3).
- Task-order `#N` templates (feature-workspace, task-blockers, artifact-deletion) are a different numbering system — conscious non-scope; visual mix `066` next to `#12` under plain style accepted (architect I-1).
- Per-feature overrides and server-side number rendering — spec non-goals, untouched.

## Microbatch

#232 trim desync, #233 board polling for preview slug, #234 hash fallback beside warning, #235 raw enum select labels.

## Post-release click-check

1. Project settings → Feature numbers: select `key`, prefix `OM` → save → board cards, task titles, notifications, ceremonies all flip to `OM-066` without reload (invalidation-driven).
2. Preview shows a real board slug live while typing the prefix.
3. Prefix `om` (lowercase) or 17 chars → input invalid, save disabled.
4. Switch to `plain` → `066`; prefix input disappears; stored prefix cleared on save (server truth, not just UI).
5. Switch to `hash` → back to `#066`; legacy PUT from an old client tab keeps numbering untouched.
6. Notification rows and attention rows follow the project style (numbering resolved per project, not per page).

<log>
- 2026-10-05 [agent] — result drafted after closeout: architect light APPROVED (0 blockers, 4 informational), both light reviews PASS-WITH-NITS, gates green on both stacks (dotnet 2163 total across 4 suites, web 182/182), microbatch #232-235. Waiting on the human tap to validate.
</log>