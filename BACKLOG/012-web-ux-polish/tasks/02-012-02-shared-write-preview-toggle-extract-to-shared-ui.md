---
feature: 012
task: 02
slug: web-ux-polish
status: done
review: light
created: 2026-09-20
---

# 012/02 — Shared Write/Preview toggle (extract to shared/ui)

<goal>
The Write/Preview toggle that exists in three near-identical copies (entry-editor, artifact-editor, task-editor) is extracted into one shared/ui component consumed by all three — identical rendering and behavior, zero visual change.
</goal>

<context>
Feature 012, hygiene half (the 006/09 NIT finally collected). The feature editor (task 04) becomes the fourth consumer. Pure refactor — no behavior, no markup drift allowed.
</context>

<scope>
- Diff the three copies: web/src/features/entries/components/entry-editor.tsx:88–107 (state :30), web/src/features/planning/components/artifact-editor.tsx:82–105 (state :43), web/src/features/planning/components/task-editor.tsx:84–105 (state :34).
- Create web/src/shared/ui/write-preview-toggle.tsx: one component, minimal props ({value/onChange} or {mode, onModeChange} — match the copies' actual shape; include the shared Button/segment classes exactly once).
- All three editors consume it; their local state stays; only the toggle markup is replaced.
- If the copies have drifted (different classes/aria), preserve each editor's exact rendering — either via className prop passthrough or by noting the intentional unification in the task log (prefer passthrough when drift is accidental-looking; unify only what is provably cosmetic-identical).
- Verify: npm run lint && npx tsc --noEmit && npm run build in web/.
</scope>

<anti-scope>
- No behavior changes to any editor (submit shapes, state flow untouched).
- No feature-form changes (task 04 adds the fourth consumer).
- No backend, no new deps.
</anti-scope>

<constraints>
- TypeScript strict, no any; FSD: shared/ui may not import from features; English-only.
- Zero visual change is the acceptance bar — when in doubt, passthrough.
</constraints>

<acceptance-criteria>
- [ ] write-preview-toggle.tsx exists in shared/ui with no feature imports.
- [ ] All three editors render the toggle via the shared component.
- [ ] lint + tsc + build green.
- [ ] Any drift decisions recorded in the log (preserved vs unified, with reasons).
</acceptance-criteria>

<refs>
- Plan: feature 012, plan artifact — risk note on copy drift.
- Pattern refs: the three editor files above; shared/ui/button.tsx, badge.tsx (component conventions).
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Execution started — next-js-coder subagent (parallel with task 01, disjoint file sets).
- 2026-09-20 — Coder done: write-preview-toggle.tsx in shared/ui (imports only @/shared/lib/cn); three copies were byte-identical except Prettier line-wraps — unified without passthrough props (drift provably cosmetic); props {showPreview, onShowPreviewChange}; +6/−77 across the three editors. lint 0 errors / tsc 0 / build green (6 pre-existing warnings elsewhere). To review.
- 2026-09-20 — Review PASS (audits/review-012-02.md): 0 blockers, 2 no-action observations (aria-pressed absent — preserved from originals, now a one-line microbatch candidate). Token-level markup equivalence verified across all four sites; useState untouched; cn dead-import removal confirmed; FSD clean. Done.
</log>
