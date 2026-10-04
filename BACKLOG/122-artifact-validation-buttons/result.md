---
feature: 122
slug: artifact-validation-buttons
status: draft
created: 2026-10-04
---

# Result 122 — Approve/Decline buttons on artifact views (human validation door)

## What shipped

- **Approve on the view** (task 02, web, `7c43e67`): draft spec/plan/result views in feature-workspace carry an Approve button re-using the existing validate mutation — validation happens where the user reads, no editor detour, no notify round-trip. The editor keeps its Approve too (pinned: not removed).
- **Decline route** (task 01, repo, `a8d05b9`): human-door `POST /api/planning/artifacts/{id}/decline` — optional reason (trim, cap 500, single line), appends a `[ui] declined by human: <reason>` log line, status NEVER touched, frozen artifacts decline without demotion, tasks → 400 (dedicated `CannotDeclineTask` → 400, not the 409-family), deleted → 404 via the 094 filter. `Source` is intentionally absent from the request — the service hardcodes `PlanningLogSource.Ui`, agents cannot spoof `[ui]` through this door (MCP surface: none, per anti-scope).
- **Decline on the view**: secondary-tone button next to Approve (danger reserved for the Delete that shares the row), opens a small reason dialog (maxLength 500 matched to the backend cap), mutation mirrors the validate idiom with invalidate.

## Verification

- Task 01: build 0/0, Api 622/622 (+8 decline tests), Infra 619/619 (+4) — coder + reviewer independently.
- Task 02: lint 0e/6 pre-existing, tsc 0, 113/113 (+7 source pins incl. task-board negative pin), build ok — coder + reviewer independently.
- Reviews: both light PASS-WITH-NITS, 0 blockers (reports: conversation for 01; audits/review-122-02-web.md for 02).
- Injection safety: reason travels as input value → JSON; log renders as plain text node (React escape); markdown renderer has no HTML sinks — confirmed by review.
- Freshness: buttons derive from `artifact.status` per render — edit → demote → buttons return without local state (pinned).

## Known notes (microbatch)

- #195: stale approveError across tab switches (reset in setTab, ~5 lines); harmless decline/validate race in the request window (log-only).
- #196: barrel asymmetry — ArtifactDeclineDialog not exported while its twin DeleteArtifactDialog is.
- #193/#194 from task 01 (double LoadArtifactOrThrow; human-door [ui] attribution unauthenticated by design — loopback posture, pin in CONTEXT if the host ever leaves 127.0.0.1).

<log>
- 2026-10-04 [agent] — both tasks done with light reviews (0 blockers); result drafted; wave ready for the human tap.
</log>