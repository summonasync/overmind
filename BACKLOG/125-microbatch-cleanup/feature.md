---
feature: 125
slug: microbatch-cleanup
status: done
priority: p0
version: 1
created: 2026-10-06
---

# Feature 125 — Microbatch cleanup wave: close the accumulated review-findings ledger as one batch

## Goal

Close the accumulated microbatch ledger (243 non-blocking review findings) as one dedicated batch wave instead of letting it grow. The ledger lives in the crew memory; this feature is its execution contract.

## Scope (the batch)

- **#139 (priority):** pin the «hidden reply is still a valid proof» invariant on the artifact proof path (PlanningArtifactService) — a future innocent `HiddenAt == null` filter must fail loudly. Also gives Hide() its first caller (closes #137).
- **Fresh wave #232–243:** trim desync in the number-prefix validator, unbounded /api/issues pagination note, status-parse ×3 extraction, DesktopPermission barrel (done), attention error-state prime gate (`isPending || error`), CrossProjectIssueRow.projectName dead client field, artifact-validation stale approveError reset (122/02), decline double-load (122/01).
- **Top legacy items** pulled by the coder's judgment within one wave's budget: #127 (explicit required-arg MCP error text), #159 (kind-uniqueness diagnostics for S/P/R aliases), #52–53 (warning-count denominator + pollution pins).

## Non-goals

- Items marked «юзер-гейт» (ADRs #218/#230, design call #221) — they live in the user-proposal track.
- Any feature-sized work — anything exceeding a one-commit fix goes back to the ledger or becomes its own feature.

## Acceptance

- Ledger entries closed in the same commit get tombstoned in the memory file with the wave hash.
- Full suites green; no behavior change beyond the pinned fixes.

<log>
- 2026-10-06 [agent] — Van by claude claimed the feature
- 2026-10-06 [auto] — artifact created → ready
- 2026-10-06 [auto] — task 01 taken → inProgress
- 2026-10-06 [auto] — all tasks done + spec + plan validated → review
- 2026-10-06 [agent] — by user request in-session — result validated in the sprint batch tap-sitting; done-flip proof = fresh reply on notification f7a8996f (prior proof went stale when the validate log-line bumped the result). Wave closed: 4 commits, 10 fixes, 0 blockers, 12 ledger tombstones, findings #244-#246 filed back.
- 2026-10-06 [agent] — done via agent review by Van (claude), approved by human tap 2026-10-06
- 2026-10-06 [agent] — claim released on done-close
</log>
