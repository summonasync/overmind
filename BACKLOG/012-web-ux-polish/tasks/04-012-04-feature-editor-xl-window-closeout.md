---
feature: 012
task: 04
slug: web-ux-polish
status: done
review: light
created: 2026-09-20
---

# 012/04 — Feature editor xl window + closeout

<goal>
The Edit/New feature window becomes full-size: both FeatureForm call sites render in the shared Modal "xl" variant, the body textarea grows to 20 rows, and the body gets the shared Write/Preview toggle (fourth consumer). Closes feature 012.
</goal>

<context>
Feature 012, web half #2 + closeout. Depends on task 02 (shared toggle). The xl variant already exists in shared/ui/modal.tsx (:50) — artifact/task/entry editors use it.
</context>

<scope>
- web/src/features/planning/components/feature-form.tsx — body Textarea rows 8 → 20 (and sensible max-h if the pattern exists); integrate the shared WritePreviewToggle over the body: Write mode = current Textarea, Preview mode = the same markdown preview component the other editors use (match their rendering path exactly).
- Call sites: feature-workspace.tsx (Modal "Edit feature", ~321–334) and planning-page.tsx (~158–178) switch size "md" → "xl". Submit shape unchanged ({slug, title, body}).
- Verify: npm run lint && npx tsc --noEmit && npm run build.
- Closeout (orchestrator-side, recorded here): write the 012 result artifact, validate, feature done via human door, unpin current, re-materialize, report changed files, single feat-commit per the user's standing instruction (no push).
</scope>

<anti-scope>
- No field additions to FeatureForm (slug/title/body only, as today).
- No changes to the artifact/task editors beyond task 02's extraction.
- No backend work.
</anti-scope>

<constraints>
- Markdown preview must reuse the exact component/util the three editors use — do not invent a second preview path.
- TypeScript strict; English-only; a11y: toggle aria semantics from the shared component.
</constraints>

<acceptance-criteria>
- [ ] Both feature editor call sites open full-size (xl); textarea 20 rows; Write/Preview toggle works over the body.
- [ ] Submit unchanged; existing feature edit flows still pass (create new + edit existing manually sane via build + review).
- [ ] lint + tsc + build green.
- [ ] Closeout artifacts written and the feature moved to done (orchestrator).
</acceptance-criteria>

<refs>
- Spec: feature 012, spec artifact — edit-feature window decision.
- Pattern refs: feature-form.tsx, feature-workspace.tsx:321–334, planning-page.tsx:158–178, shared/ui/modal.tsx:49–51 (xl variant), the editors' preview rendering (entry-editor/artifact-editor/task-editor).
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Execution started — next-js-coder subagent (shared toggle from 02 and TagsApi from 01 both in tree).
- 2026-09-20 — Coder done: FeatureForm in xl (both call sites), textarea rows 20 + flex-fill (editors' pattern), WritePreviewToggle 4th consumer, preview via the shared Markdown path 1:1; Field→explicit label htmlFor for Body (Field's label wrapper would intercept toggle clicks); submit shape untouched. lint 0 / tsc 0 / build green. To review.
- 2026-09-20 — Review PASS (audits/review-012-04.md): 0 blockers, 3 no-action observations (dangling htmlFor in preview mode — inert; toggle right-of-label vs standalone-left elsewhere — cosmetic; coder's Field-click-capture rationale overstated for modern browsers but the replacement is semantically right). Preview path verified 1:1 with the three editors; flex-fill chain works in xl; submit untouched. Done.
</log>
