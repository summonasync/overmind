---
feature: 105
task: 01
slug: register-all-task-sections
status: todo
review: light
created: 2026-10-04
brief: Canonicalizer + upsert/import wiring + friction characterization + 400 hint
---

# Task 01 — Canonicalizer + entrance wiring + 400 hint

<goal>
Every section present in an artifact body becomes patchable: canonicalize one-line sections at the entrance.
</goal>

<scope>
- Core canonicalizer: one-line `<tag>content</tag>` → multi-line; empty `<tag></tag>` → `<tag>\n</tag>`; idempotent on canonical bodies (byte-stable).
- Wired into single upsert_artifact, batch upsert_tasks, and import entrances.
- Characterization test reproducing the friction payload: multi-line skeleton + inline anti-scope/constraints → both patchable without a re-send.
- PatchSectionNotFound 400 gains a hint line when an inline candidate for the requested name exists (old bodies path).
</scope>

<anti-scope>
- Parser changes (anchored multi-line contract stays); data migrations.
</anti-scope>

<acceptance-criteria>
- [ ] Friction payload characterization: inline anti-scope/constraints patchable after upsert — pinned.
- [ ] Canonicalizer idempotent (canonical in → byte-identical out) — pinned.
- [ ] All three entrances canonicalize — pinned.
- [ ] 400 hint fires on old-style bodies — pinned.
</acceptance-criteria>

<refs>
- Spec 110bcde0 (root cause verified by probe); plan 7eecf2c9 (task 1).
</refs>