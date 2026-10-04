---
feature: 103
slug: ac-tick-ux
status: draft
created: 2026-10-04
---

# Result 103 — AC tick UX: echo what was ticked, un-tick op, index-base consistency (Qwen friction #2)

## What shipped

**Release notes line (breaking, loud):**
> `acIndex` is now **1-based** across every surface — MCP, HTTP, diagnostics, and the materialized mirror render the same numbering (acIndex=1 is the first item; 0 is rejected). New `untick_ac` op with an honest no-op; every tick/untick echoes `tickedIndex` + `tickedText`. Wire field `alreadyTicked` renamed to `noChange` (breaking, no external consumers).

- **Domain (c16de1a)**: parser numbers AC items from 1 (`PlanningSections.cs:98`); `TickAc`/`UntickAc` return a `PlanningAcTickResult(Index, Text, Changed)` via a single `FlipAc`/`ResolveAcTarget` path; `[X]` uppercase markers accepted; ambiguous/no-op/markerless diagnostics render 1-based listings.
- **Doors (09b947f)**: HTTP `untick_ac` op with the same xor-selector shape; echo fields in `PlanningArtifactPatchResponse` (`WhenWritingNull` preserved for section/brief patches — key-order pins intact); MCP tool gains the `untick` parameter, description carries the verbatim `AcIndexBasisLine` («acIndex is 1-based; numbering matches the materialized mirror; acText is the recommended selector») pinned by tests; shape errors (untick+section/brief/selector-less) reject before any HTTP call.
- **Bonus**: 29 migration files canonicalized (0f4be6d) — repo-wide `dotnet format --verify-no-changes` is green (EXIT 0), closing the migrations part of microbatch #23/#183.

## Verification

- Gates (coder + reviewer + architect, all re-run independently): build 0/0, Api 614/614, Mcp 446/446, Infra 615/615, Core 301/301; format clean.
- Consistency pass: echo parity across both doors confirmed by review (HTTP echo fields, MCP echo JSON); diagnostics listing parity with the mirror pinned by the verbatim AcIndexBasisLine test; no 0-based assertions remain (sweep confirmed by review + architect).
- Reviews: 01 light PASS-WITH-NITS, 02 light PASS-WITH-NITS (1 WARNING closed by spec log-note — the alreadyTicked→noChange rename note), architect light **APPROVED** (0 blockers; 1 suggestion → microbatch #190, 1 observation — MCP no-op renders flat text by the honest-noop convention, deliberate).

## Post-release click-check

- [ ] Open any artifact in the materialized mirror, tick an AC item by the number AS RENDERED (1-based) via MCP patch — echo must name that exact item.

## Microbatch additions

- #188 (McpSessionTestBase hard-coded tick echo), #185/#186 (tick-01 nits), #190 (SwapAcMarker uppercase edge: prefix check at index 0 before IndexOf).

<log>
- 2026-10-04 [agent] — wave closed: tasks 01+02 done with light reviews, architect light APPROVED, result drafted.
</log>