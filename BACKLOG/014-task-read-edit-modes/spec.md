---
feature: 014
slug: task-read-edit-modes
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Spec 014 — Task open modes: read view + edit

<decisions>
- Read mode IS the existing Write/Preview toggle defaulting to Preview: clicking a task card opens the (already full-size, 012) task modal in read state — the body renders through the same shared Markdown path; no separate read window, no new surface. Switching to Write is the explicit edit intent, which is exactly the "protection from accidental edits" the feature body asks for (decision delegated by the user 2026-09-20).
- The action bar is mode-aware: read (Preview) shows only the close affordance; write (Write) shows the existing Save/Cancel. The Brief input and all form fields render only in write mode — read view is pure rendered markdown, clean for reading the ceremony contract.
- The default applies on OPEN only: if the user flips to Write, edits, saves, the modal closes (current behavior); reopening starts in read again. No mode persistence.
- Doc artifacts (spec/plan/result editors) keep their current behavior — this feature is about TASKS (the ceremony contract reading pain). Their editors already carry the toggle explicitly.
- No backend, no new endpoints, no mutation changes.
</decisions>

<open-questions>
- none (user delegated autonomous decisions 2026-09-20)
</open-questions>

<requirements>
Web only (features/planning):
- task-editor.tsx: initial showPreview state = true when opening an EXISTING task (create flow still opens in Write — you cannot preview what doesn't exist); reuse the shared WritePreviewToggle as-is.
- Mode-aware action bar: Preview mode → close button only; Write mode → the existing Save/Cancel. Form fields (Brief input, body textarea) hidden in Preview mode (the toggle already swaps textarea↔markdown; extend the hiding to the Brief field and action buttons).
- The workspace open-task flow unchanged except the modal's initial mode.
- Verify: npm run lint && npx tsc --noEmit && npm run build.
</requirements>

<acceptance-ideas>
- Click a task card → full-size modal in read mode: rendered markdown of the ceremony contract, no inputs, only close.
- Toggle Write → the familiar editor (Brief + body + Save/Cancel).
- Create task → opens directly in Write (no empty preview).
</acceptance-ideas>
