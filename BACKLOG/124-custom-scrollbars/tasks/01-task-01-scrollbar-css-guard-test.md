---
feature: 124
task: 01
slug: custom-scrollbars
status: done
review: light
created: 2026-10-06
brief: App-wide styled thin scrollbar: additive @layer base block in web/src/app/globals.css (webkit + Firefox, token-driven colors) + string-pin guard test web/tests/scrollbar-styling.test.mts; gates lint/tsc/test/build + Chrome/Firefox eyeball.
---

# Task 01 — Scrollbar CSS + guard test

<goal>
Ship the styled thin scrollbar app-wide: one additive CSS block in web/src/app/globals.css per the validated plan's selector contract, plus a string-pin guard test — verified by the full web gate (lint, tsc, tests, build) and a manual Chrome/Firefox pass.
</goal>

<scope>
- web/src/app/globals.css — additive block inside the existing @layer base:
  - :root { scrollbar-width: thin; scrollbar-color: var(--color-muted) transparent; } (Firefox; properties inherit)
  - unqualified ::-webkit-scrollbar { width: 8px; height: 8px; }
  - ::-webkit-scrollbar-track and ::-webkit-scrollbar-corner — background: transparent
  - ::-webkit-scrollbar-thumb — background: var(--color-muted), fully rounded
  - ::-webkit-scrollbar-thumb:hover — color-mix(in oklab, var(--color-muted) 60%, var(--color-foreground))
- web/tests/scrollbar-styling.test.mts — new node:test file reading globals.css via readFileSync (pattern of entry-tombstone.test.mts), asserting scrollbar-width: thin, scrollbar-color, ::-webkit-scrollbar presence and no hardcoded hex inside the scrollbar block.
</scope>

<anti-scope>
- No .ts/.tsx changes — badge.tsx and every tone-class file are string-pinned by source-reading tests; hands off.
- No scrollbar-gutter, no theme-token edits, no JS scroll handling, no new dependencies, no per-component scrollbar CSS.
</anti-scope>

<constraints>
- Additive-only edit in globals.css — the file is read as a string by web/tests/entry-tombstone.test.mts (asserts css.includes("--color-type-tombstone:")); existing content must stay byte-identical except the addition.
- Colors from @theme tokens only (var(--color-*)), zero hardcoded hex in the new rules.
- Keep the existing body/@apply and ::selection rules intact; mirror their color-mix style for the hover state.
</constraints>

<acceptance-criteria>
- [x] globals.css carries the scrollbar block per the plan's selector contract; existing content unchanged apart from the addition.
- [x] Track/corner transparent; thumb var(--color-muted); hover visibly brighter via color-mix on theme tokens; no hardcoded hex in the new rules.
- [x] New guard test passes and pins the block; existing entry-tombstone pin test stays green.
- [x] cd web && npm run lint && npx tsc --noEmit — green.
- [x] npm test — green.
- [x] npm run build — green (static export).
- [ ] Manual pass: page scroll, sidebar, a modal, a markdown pane and the notification center show the thin styled scrollbar in a Chromium browser and Firefox; no layout jump on scrollbar appear/disappear.
</acceptance-criteria>

<refs>
- spec artifact c5da3ade-9e0e-4444-b88f-f49304ef1570 (validated) — scope, constraints, acceptance bar, contrast math.
- plan artifact 70b733be-7049-4da1-9309-1e9b8708dcb6 (validated) — selector contract, pin strategy, verification gates.
- web/src/app/globals.css — the styling file; @theme tokens and the ::selection color-mix precedent.
- web/tests/entry-tombstone.test.mts — the pin test on globals.css and the readFileSync test pattern to copy.
</refs>

<obstacles>
- None at draft time. Coder: patch any environment quirk or workaround here via planning_patch_artifact.
- 2026-10-06 [coder] — AC7 (manual Chrome/Firefox eyeball) is left unticked on purpose: headless coding round has no browser. Every static assertion the AC relies on (selector contract, token-only colors, hover color-mix) is pinned by web/tests/scrollbar-styling.test.mts and all gates passed; the visual pass stays with the reviewer/human.
</obstacles>

<log>
- 2026-10-06 [agent] — task carved from the validated plan's single-task slice (single-coder feature; CSS + guard test together).
- 2026-10-06 [agent] — coding round started
- 2026-10-06 [agent] — coding round complete — light review gate
- 2026-10-06 [agent] — light review APPROVED — 0 blockers, 1 SUGGESTION (thumb background token un-pinned — filed to ledger #247). Spot-check 15/15, anti-scope clean (2 files), commit a958703. AC7 (manual browser pass) stays for the human.
</log>