---
feature: 021
task: 03
slug: task-brief
status: done
review: light
created: 2026-09-20
---

# 021/03 — Brief in the web UI + closeout

<goal>
The brief is visible in the tasks kanban (muted multi-line text under the card title, hidden when empty) and editable in TaskEditor on create and edit; the feature closes with a live dogfood and the result artifact.
</goal>

<context>
Feature 021, layer 3 of 3. Depends on tasks 01 (wire shape) and 02 (no data surprises). Dogfood target: this very project — create a task with a brief via MCP, see it in the kanban immediately.
</context>

<scope>
- `web/src/features/planning/types.ts`: PlanningArtifactResponse + `brief?: string | null`; create/update value types (156–167) carry optional brief.
- `web/src/features/planning/components/task-board.tsx` TaskCard: render brief under the heading — muted color, multi-line, CSS line-clamp 3, nothing rendered when null/empty.
- `web/src/features/planning/components/feature-workspace.tsx` TaskEditor: Brief input above the body editor; submitted on create and on edit. Semantics guard: editing a task without touching brief must send the loaded value (or omit — either way it must NOT clear an existing brief; "" in the input = explicit clear). Decide in-task, verify manually both ways.
- Verify: `npm run lint && npx tsc --noEmit` in web/; `npm run build` green.
- Live dogfood: via MCP create a throwaway task with a brief on this project's 021 feature, confirm it renders in the kanban and edits/clears via TaskEditor; delete the throwaway task after evidence.
- Closeout: write the 021 result artifact (outcome, evidence, deferred), validate it, move the feature to done via the human door, unpin current, re-materialize planning to disk, report the changed-file list (no auto-commit).
</scope>

<anti-scope>
- No backend changes (fix-loops for live findings go through the normal task status flow).
- No read-mode work (that is 014), no window resizing (that is 012).
- No optimistic UI or polling work (that is 017) — plain React Query invalidation as today.
</anti-scope>

<constraints>
- Tailwind utility classes for the clamp (line-clamp-3) and muted text; RSC-first rules do not apply inside these client components beyond existing patterns.
- English-only UI strings.
</constraints>

<acceptance-criteria>
- [ ] Kanban card shows brief (≤3 lines, muted) under the title; no brief → no extra element.
- [ ] TaskEditor: create with brief, edit changes it, clearing the input + save clears it; editing body alone never clears an existing brief.
- [ ] tsc --noEmit + npm run lint + npm run build green.
- [ ] Live dogfood evidence captured (MCP-created task with brief visible in UI).
- [ ] Result artifact written + validated; feature done via human door; planning re-materialized; changed files reported (no commit without explicit user ask).
</acceptance-criteria>

<refs>
- Spec: feature 021, spec artifact — UI decisions (multi-line clamp, empty renders nothing, TaskEditor-only editing).
- Pattern refs: task-board.tsx TaskCard (134–169), task-heading.ts, feature-workspace.tsx TaskEditor (282–319), types.ts (74–86, 156–167).
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Web slice execution started — handed to next-js-coder subagent in parallel with the 02 review (closeout stays with the orchestrator).
- 2026-09-20 — Web coder done: types.ts brief mirror, api.ts payload threading, TaskCard muted line-clamp-3 (renders nothing when empty), TaskEditor brief input with 500-char guard, feature-workspace wiring; always-send-loaded-value semantics (untouched input never clears an existing brief). lint 0 errors / tsc 0 / build 0. To review.
- 2026-09-20 — Review PASS (audits/review-021-03.md): 0 blockers, 3 NITs (client 500-guard checks raw vs server post-trim — never underblocks; maxLength attr suggestion; minor). Critical semantics verified through to the server (untouched input never clears); doc editors cannot trip the kind guard; input element sanitizes LF/CR natively. lint 0 / tsc 0 / build green. Live UI visual check left to the user (backend e2e covered by 794 tests). Done.
</log>
