---
feature: 103
slug: ac-tick-ux
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Spec 103 — AC tick UX: echo what was ticked, un-tick op, index-base consistency (Qwen friction #2)

> Grooming 2026-10-04 (user in session): index-compat decision made by the user — **1-based everywhere**.

## Goal

Kill the silent wrong-tick class (Qwen friction #2, entry 500f8197): `acIndex` is 0-based on the wire while every human-facing rendering (materialized mirror checkboxes, 400 diagnostics wording) reads 1-based — an agent ticking "item 3" hits the neighbour and the write succeeds. Plus: no un-tick op, so repairing costs a full section re-send.

## Decisions

- **acIndex becomes 1-based everywhere**: wire parameter (MCP + HTTP), domain `TickAc`, 400 diagnostics listing, patch echo. Item numbering matches the materialized mirror exactly — one numbering, not two. **Breaking change, loud and documented**: tool descriptions state «acIndex is 1-based; numbering matches the materialized mirror» verbatim; existing 0-based pin tests are rewritten (not kept as compat shims — there is no installed base worth a shim; our agents re-read descriptions).
- **Tick echo**: the patch response carries `tickedIndex` + `tickedText` (plus the existing `alreadyTicked` flag) — the caller sees WHAT was ticked; the observed mis-tick would have been caught instantly.
- **Un-tick op**: a new patch operation symmetric to tick — `untick` with `acText` (primary) or `acIndex` (1-based). Clears `[x]` → `[ ]`. Already-unticked → successful no-op reporting «already unticked — no change» (same honesty pattern as the tick retick no-op). Non-checkable item (no checkbox marker) → 400 `TickItemNotCheckable` as today.
- **acText stays the recommended path** — descriptions teach exact-text as primary, index as fallback.
- Version bump on un-tick = same rules as tick (real change bumps, no-op doesn't; logNote rules untouched).

## Non-goals

- Section parser changes (105's territory); AC data-model changes beyond using the existing `Checked` tri-state; compat shims for 0-based callers.

<log>
- 2026-10-04 [agent] — groomed with the user in session: 1-based everywhere (user decision), tick echo, un-tick op, acText-primary teaching. Breaking change accepted and made loud.
- 2026-10-04 [agent] — validated with human proof: notification d9415511-7925-4205-b641-02524360c2e3 replied 2026-10-04 11:48:51 UTC
</log>