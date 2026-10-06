---
feature: 117
slug: phase-label-as-tag-chip
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Plan 117 — Board card: phase label as tag chip after the slug

> Groomed 2026-10-06. 2 tasks, 01 → 02 (02 builds on 01's null-safe signature). All paths relative to `web/`.

## Task chain

1. **01 — Null-safe phase label + type + test pins.** `src/features/planning/types.ts`: `phase: string | null` on `PlanningFeatureBoardItem` (only consumer today is `cardPhaseLabel`). `src/features/planning/lib/board-card-context.ts`: `cardPhaseLabel` returns `string | null` — null or trimmed-empty phase → null, otherwise the server label verbatim. `tests/board-card-context.test.mts`: new cases for null / `""` / `"   "` → null; the seven server labels pass through; existing passthrough and done-card pins stay green (the `assert.notEqual(…, null)` done-card pin survives the signature change). Verify: `npm test`, `npx tsc --noEmit`.
2. **02 — Card header swap.** `src/features/planning/components/feature-board.tsx`: the slug line inside the header link becomes an inline flex row — slug span (`min-w-0 truncate font-mono text-xs text-muted`) + phase chip (`Badge`, default neutral tone, `shrink-0`) rendered only when `cardPhaseLabel` returns non-null; delete the old `text-[10px] uppercase tracking-wide` span and its `Ceremony phase: …` tooltip from the right-side group (blocked badge + status select untouched). Verify: `npm run lint && npx tsc --noEmit && npm test && npm run build`; visual tap on the dashboard board card (chip right after the slug; header row unchanged on a null-phase card) during the consolidated validation sitting.

## Verification

- Gates (in `web/`): `npm run lint && npx tsc --noEmit && npm test && npm run build`.
- Pin strategy: `tests/board-card-context.test.mts` — the existing board-card web test — is extended, not replaced; the repo has no component-render harness, so layout regression is covered by tsc/build plus the human visual check.
- Review: light both tasks (cosmetic, single feature slice, no security surface).

<log>
- 2026-10-06 [agent] — plan groomed: 2 tasks (null-safe label + type first, card swap second); Badge neutral reuse, no new variant; pinned on board-card-context tests + full gates.
- 2026-10-06 [agent] — validated with human proof: notification d557b412-83dc-43f2-801e-9e34d8cac869 replied 2026-10-06 13:05:11 UTC
</log>