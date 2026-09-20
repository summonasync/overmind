---
feature: 038
slug: priority-inline-edit
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Spec 038 — Priority inline edit: селект прио на карточке и в хедере фичи

<decisions>
- Mirror the 022 status-select idiom exactly on both surfaces — no new interaction vocabulary: compact Select from shared/ui, immediate PUT through the existing human-door mutation, optimistic cache update, rollback to the previous value on 400/error (the 022 snapshot-rollback path).
- Board card: the priority chip's slot becomes interactive — same position in the metadata row, display keeps the tone mapping (p0 danger / p1 warning / p2 neutral / p3 muted), interaction is a Select offering P0-P3. The chip look stays (Select styled to the chip tone family) so the board's visual calm is preserved; the affordance follows the status select's precedent on the same card.
- Workspace header: priority Select rendered next to the status Select, same sizing/spacing.
- Same PUT path as the editor (UpdatePlanningFeatureValues with priority; other fields null/omitted — the 034/01-verified null-guard keeps title/body/status untouched).
- After a successful change the board re-sorts immediately (existing composite sort reacts to the updated cache entry) — that immediacy is the point of the feature; no extra work needed.
- isPending state disables the select during flight (022 asymmetry note from microbatch #10 — wire it correctly here rather than repeating the asymmetry).
</decisions>

<open-questions>
- none (the 022 pattern and 034 tone system decide everything; user request is the requirement).
</open-questions>

<requirements>
- feature-board.tsx: interactive priority select on the card (chip-styled, four options, disabled while mutation pending); optimistic update + rollback on failure.
- feature-workspace.tsx header: priority select beside the status select, same behavior.
- Reuse the existing update mutation; no API changes; no new components beyond what the two call sites need (a small shared priority-select in features/planning if both sites would otherwise duplicate markup).
- Gates: npm run lint && npx tsc --noEmit && npm run build green.
</requirements>

<anti-scope>
- No agent-door priority updates (still non-goal).
- No drag-and-drop, no batch re-prioritization, no sort settings UI.
- No backend changes.
</anti-scope>