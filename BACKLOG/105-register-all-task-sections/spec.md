---
feature: 105
slug: register-all-task-sections
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Spec 105 — Register all task sections on upsert (patch without full re-send) — Qwen friction #4 / bug 6ee3be0f

> Grooming 2026-10-04 (user in session). Source: Qwen friction #4 + bug 6ee3be0f. Root cause FOUND during grooming (regex probe, 2026-10-04).

## Root cause (verified, not a hypothesis)

The section parser's anchored regex (`^[ \t]*<tag>[ \t]*\r?$`) requires each tag on its OWN line. One-line inline sections (`<anti-scope>None.</anti-scope>`) are INVISIBLE to `List()`/`Find()` — verified by probe:

- multi-line `<anti-scope>\nNone.\n</anti-scope>` → found;
- one-line `<anti-scope>None.</anti-scope>` → NOT found.

An agent sends a task body where the skeleton sections are multi-line but additions (`anti-scope`, `constraints`) are inline; upsert stores the body verbatim; patch then refuses with «Sections present: goal, scope, …» — the sections are in the body but unparseable. Nothing in the pipeline strips them; the parser just cannot see them.

## Decisions

- **Canonicalize at the entrance, not in the parser** (039 non-goal «rewriting the section parser» is respected): single `upsert_artifact`, batch `upsert_tasks` and import normalize one-line sections to canonical multi-line form (`<tag>content</tag>` → `<tag>\ncontent\n</tag>`, empty `<tag></tag>` → `<tag>\n</tag>`). Idempotent: canonical bodies pass through unchanged (byte-stability of re-import preserved).
- **Backfill is lazy** (challengeable): bodies already stored with inline sections normalize on their next full upsert; no data migration. For old bodies a section patch keeps failing — but the 400 gains a hint line when an inline candidate for the requested name exists: «a one-line '<name>' section exists in the body — one-line sections need the canonical multi-line form; re-send the body once».
- **Pin the friction payload**: an upsert carrying the observed shape (multi-line skeleton + inline anti-scope/constraints) → both sections patchable afterwards without a body re-send.

## Non-goals

- Parser changes (anchored multi-line contract stays); data migrations; changing what sections MEAN.

<log>
- 2026-10-04 [agent] — groomed with the user in session; root cause verified by regex probe (anchored regex misses one-line sections); decision: canonicalize at entrance (upsert×2 + import), lazy backfill, 400 hint for old bodies.
- 2026-10-04 [agent] — validated with human proof: notification 69d84ca7-aa85-48a0-bb87-dccfd9384c4e replied 2026-10-04 11:48:53 UTC
</log>