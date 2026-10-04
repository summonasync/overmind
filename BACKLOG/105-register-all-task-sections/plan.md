---
feature: 105
slug: register-all-task-sections
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Plan 105 — Register all task sections on upsert (patch without full re-send) — Qwen friction #4 / bug 6ee3be0f

> Grooming 2026-10-04. 2 tasks, 01 → 02 (small wave; closeout folds the architect pass).

## Task chain

1. **01 — Canonicalizer + entrance wiring + diagnostics.** Core canonicalizer (one-line `<tag>…</tag>` → multi-line; idempotent on canonical bodies); wired into single upsert, batch upsert_tasks and import entrances; characterization test reproducing the friction payload (multi-line skeleton + inline anti-scope/constraints → both patchable, no re-send); 400-hint when an inline candidate for the requested section name exists (old bodies path).
2. **02 — Door pins + closeout.** MCP/HTTP door pins (canonicalization observable on both doors, import round-trip byte-stable for already-canonical bodies); consistency pass; architect light; result with post-release click-check (send a body with an inline section through upsert, patch it).

## Verification

- Characterization + canonicalizer tests (01), door pins (02), gates both tasks, architect light (02).

<log>
- 2026-10-04 [agent] — plan groomed: 2 tasks; root cause pre-verified by the grooming probe, characterization test is the pin.
- 2026-10-04 [agent] — validated with human proof: notification 69d84ca7-aa85-48a0-bb87-dccfd9384c4e replied 2026-10-04 11:48:53 UTC
</log>