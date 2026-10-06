---
feature: 120
task: 02
slug: claimed-card-frame
status: inprogress
review: light
created: 2026-10-06
depends_on: 01
brief: Render claim line left of the status column and border-primary frame for active claims in FeatureBoard.
---

<goal>
Render the claim on the board card: the claim line immediately left of the status column, and a `border-primary` frame around the card for actively held claims — the successor of the pre-099 pinned-card frame.
</goal>

<scope>
- web/src/features/planning/components/feature-board.tsx:
  - Header row (the `flex items-start justify-between gap-2` div): render the claim line as a middle child between the title link and the right cluster — `ml-auto min-w-0 truncate text-xs`; tone `text-success` when `cardClaimActive(item)` else `text-muted`; render NO element when `cardClaimLine(item)` returns null (unclaimed cards stay pixel-identical, no layout shift).
  - Card root `li` className ternary: add `cardClaimActive(item) && "border-primary"` — the exact class the pre-099 `isCurrent` pin frame used (removed in commit 1686abd). Drop-target `ring-2 ring-primary/40` and unclaimed `hover:border-border-strong` stay untouched.
- Consume the helpers exported by task 01 (web/src/features/planning/lib/board-card-context.ts) — no formatting logic in the component.
</scope>

<anti-scope>
No FeatureClaimChip changes, no overview-panel or task-queue surfaces, no brief-line moves (117's lane), no backend, no new dependencies.
</anti-scope>

<constraints>
- Unclaimed cards must be pixel-identical to today.
- Token classes only (border-primary, text-success, text-muted); the line text comes from the helper verbatim.
- Start from a clean tree; 117 may touch the same header row in a separate sequential coder run — do not pull or rebase anything, just re-read the file before editing.
</constraints>

<acceptance-criteria>
- [ ] Claimed card renders the claim line immediately left of the status column cluster.
- [ ] Actively held claim (alive agent claim or human-held) → `border-primary` frame + `text-success` line.
- [ ] Stale, ended, and null-liveness claims → `text-muted` line, NO frame.
- [ ] Unclaimed cards render no claim element and keep today's exact classes.
- [ ] `npm run lint && npx tsc --noEmit && npm test && npm run build` all green in web/.
</acceptance-criteria>

<refs>
- Plan bb7b524e-180a-4ce9-95c7-94709aa73b64 (task 2); spec c6f38160-de30-46c8-9538-c02c429df482 (binding requirements).
- Task 01 of this feature — exports cardClaimLine / cardClaimActive consumed here.
- web/src/features/planning/components/feature-board.tsx — header row (~line 217) and card root `li` className (~line 210) as of drafting; re-read before editing.
</refs>

<obstacles>
None at creation.
</obstacles>

<log>
- 2026-10-06 [agent] — Task created from the validated plan, task 2; blockedBy task 01 (compile dependency on its helper exports).
- 2026-10-06 [agent] — task 02 coding round started
</log>