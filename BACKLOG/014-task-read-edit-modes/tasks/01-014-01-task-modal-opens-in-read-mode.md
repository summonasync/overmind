---
feature: 014
task: 01
slug: task-read-edit-modes
status: todo
review: light
created: 2026-09-20
---

# 014/01 — Task modal opens in read mode

<goal>
Clicking an existing task opens its (full-size, 012) modal in read mode: rendered markdown via the shared toggle's Preview, Brief input and Save/Cancel hidden, close-only. Flipping to Write reveals the familiar editor. Create flow still opens in Write.
</goal>

<context>
Feature 014, the only task. Rides 012's shared WritePreviewToggle and the xl modal. task-editor.tsx already carries the Brief input (021) and the shared toggle (012/02) — edit surgically on top of the current state.
</context>

<scope>
- web/src/features/planning/components/task-editor.tsx: initialize showPreview = true when an existing task is passed (create flow keeps false); in Preview mode hide the Brief field and the Save/Cancel bar (close-only affordance remains — the modal's existing close button); Write mode renders exactly as today.
- The toggle component itself is NOT modified.
- Verify: npm run lint && npx tsc --noEmit && npm run build.
</scope>

<anti-scope>
- No doc-artifact editor changes, no backend, no mode persistence, no new components.
</anti-scope>

<constraints>
- TypeScript strict; English-only; FSD.
- PARALLEL WORK: 023/03 edits types.ts/feature-board.tsx/feature-workspace.tsx/status-tone — do not touch those; a backend reviewer may run dotnet — ignore. npm lock noise → wait, retry.
</constraints>

<acceptance-criteria>
- [ ] Existing task opens in read: rendered body, no inputs, no Save/Cancel, close works.
- [ ] Toggle → Write: Brief + textarea + Save/Cancel exactly as today.
- [ ] Create task: opens in Write with empty fields.
- [ ] lint + tsc + build green.
</acceptance-criteria>

<refs>
- Spec: feature 014, spec artifact (validated 2026-09-20) — read-as-preview decision, mode-aware bar.
- Pattern refs: task-editor.tsx (current state post-021/012), shared/ui/write-preview-toggle.tsx.
</refs>

<obstacles>
</obstacles>

<log>
</log>
