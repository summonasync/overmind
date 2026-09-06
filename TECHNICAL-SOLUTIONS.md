# Technical Solutions

Architecture Decision Records. Newest first. Once Accepted, changing a decision requires a new ADR that supersedes the old one — never a silent edit.

## Single active kit per project — 2026-09-06
**Status:** Accepted
**Context:** The data model (`ProjectKit` many-to-many with `IsActive`) technically allows multiple active kits per project, but the materializer emits one `overmind-bootstrap/SKILL.md` per platform, so a second kit activation on the same platform collides (409 OverwriteBlocked). Surfaced during task 04 hard review (architect IMPORTANT-1). The frozen spec already implies switch semantics: "switching a kit re-materializes files".
**Decision:** Exactly one kit may be active per project. `Activate` with another kit already active returns 409 instructing to deactivate first; the UI offers a "switch" flow (deactivate + activate). No per-kit bootstrap namespacing.
**Alternatives considered:** Multi-kit with `overmind-bootstrap-<kit-slug>/` namespacing — rejected: bigger conflict/drift surface, marginal v1 value.
**Consequences:** Simpler materialization and UI. Revisiting multi-kit = new ADR + bootstrap namespacing work.

## Project DiskPath is immutable while a kit is active — 2026-09-06
**Status:** Accepted
**Context:** `MaterializedFile` rows store relative paths; dematerialize resolves them under the project's current `DiskPath`. Changing `DiskPath` while files are materialized would make deactivate delete foreign files at matching relative paths in the new directory (task 04 review, blocker B1).
**Decision:** `PUT /api/projects/{id}` rejects `DiskPath` changes with 409 while any kit is active or any `MaterializedFile` rows exist; the user deactivates kits first.
**Alternatives considered:** Persist absolute path snapshot per `MaterializedFile` and refuse deletes on mismatch — rejected for v1 as a heavier invariant (extra migration, snapshot semantics) where a state guard achieves the same safety.
**Consequences:** DiskPath retarget requires an explicit deactivate step — visible, safe, and self-explanatory via the 409 message.
