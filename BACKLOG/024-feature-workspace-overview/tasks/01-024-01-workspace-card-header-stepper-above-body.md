---
feature: 024
task: 01
slug: feature-workspace-overview
status: done
review: light
created: 2026-09-20
---

# 024/01 — Workspace card header + stepper above body

<goal>
The feature workspace header becomes the vertical card per the spec (v2): ← Dashboard above the title, slug·updated line, meta row [tone Select][vN][Edit][Delete as today]; the ceremony stepper moves between the header and the description body. All existing select/stepper mechanics unchanged.
</goal>

<context>
Feature 024, task 1 of 2. feature-workspace.tsx is hot (021/022/023/019 landed in it) — read the current state, edit surgically. PageHeader's single-row actions shape gets replaced by a local header layout.
</context>

<scope>
- feature-workspace.tsx: replace the PageHeader usage with a local header block — ← Dashboard link (small, muted, focus ring) above the title; title; `slug · updated X ago` description line; meta row: the existing tone-styled status Select (unchanged mechanics), vN badge, Edit button, Delete button (same place/behavior as today, confirm modal untouched). ErrorText directly under.
- Move the <CeremonyStepper> render to sit between the header block and the feature body description (currently below the body / above the tabs — relocate).
- Verify: npm run lint && npx tsc --noEmit && npm run build.
</scope>

<anti-scope>
- No select behavior changes, no stepper changes, no right-panel work (task 02), no backend.
</anti-scope>

<constraints>
- TypeScript strict; English-only; FSD; keep all aria semantics.
</constraints>

<acceptance-criteria>
- [ ] ← Dashboard renders above the title; slug·updated below it; meta row with all four controls.
- [ ] Exactly one Delete button, same confirm flow.
- [ ] Order: header → stepper → body → tabs.
- [ ] lint + tsc + build green.
</acceptance-criteria>

<refs>
- Spec: feature 024, spec v2 (Delete amendment).
- Pattern refs: feature-workspace.tsx (current), PageHeader (what it looked like), STATUS select at :134–149, CeremonyStepper mount.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Execution started — next-js-coder subagent (clean tree after the 020 commit; user is walking the 0.1.8 UI in parallel, entries-tag drift report pending a screenshot).
- 2026-09-20 — Coder done: local vertical header (← Dashboard above title, slug·updated line, meta row Select/vN/Edit/Delete byte-identical handlers+aria); stepper moved to header → ErrorText → stepper → body → tabs; the null-feature branch keeps PageHeader deliberately (out of scope). lint 0 / tsc 0 / build green. To review.
- 2026-09-20 — Review PASS (audits/review-024-01.md): 0 blockers, 3 INFO (workspace select was never tone-tinted — board parity is a microbatch, not a regression; mb-6→space-y-4 spacing inherent to the card; null-branch keeps PageHeader — deliberate). Header mechanics byte-identical (aria, done-lock, optimistic PUT); order header → ErrorText → stepper → body → tabs verified; semantic header landmark improvement noted. Done.
</log>
