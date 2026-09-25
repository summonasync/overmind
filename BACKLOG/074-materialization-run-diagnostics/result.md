---
feature: 074
slug: materialization-run-diagnostics
status: validated
created: 2026-09-25
validated: 2026-09-25
---

# Result 074 — Materialization run diagnostics: lastSuccess/lastAttempt, per-file outcome, atomicity, diagnostic tool

<summary>
Shipped (2 tasks, ~25 files): materialization runs are atomic (plan-then-write — the full op plan computes before any disk write; any refusal aborts the run with ZERO writes and a 409 naming every refusal and the next step), every completed/aborted run persists a structured per-file JSON report on Project ({startedAt, finishedAt, atomic, written, skipped[no-op "content already matches the board"], refused[reason]}), and Project distinguishes lastAttempt (every run, durably stamped before any plan code) from lastSuccess — the BobHouse 020 "lastRun=never(failed) while the file was regenerated" confusion is dead. Onboard and planning_materialize render the split honestly; skip counts re-labeled to byte-identical no-ops. New MCP tool planning_materialize_status (ReadOnly+Idempotent, 22nd planning tool / 31st total): lastSuccess/lastAttempt(+reason), per-file last-run report, trackedFiles, stalePaths (shared drift detector), strategy/targetDir — full mirror state without filesystem access. Tool descriptions and CONTEXT.md rewritten to the refusal+abort truth (stale 067 skip promises burned). 067-D2 supersede (skip-not-abort → refusal+abort) documented on the feature for the user's review.
</summary>

<evidence>
- Suite: 1415 → 1441 (Core 212, Infra 470, Api 414, Mcp 345); build 0 warnings; two consecutive green runs at task close; W1+W2 point fix added the killed-attempt pin.
- Reviews: 01 round-1 BLOCKED (B1: attempt stamp died on failure branches via ChangeTracker.Clear) → fix (durable stamp right after marking + write-phase catch persisting the aborted report + self-caught staged-row detach regression) → same-reviewer re-check APPROVED; 02 APPROVED (all 8 round-1 handoffs burned; W1+W2 fixed point-wise; W3 → microbatch). Audits: review-074-01.md (+re-check), review-074-02.md.
- Live walk (isolated host, port 4801, scratch HOME, killed by PID): clean run → status shows lastAttempt < lastSuccess, atomic=true, trackedFiles=2, stalePaths=[] → hand-edit a materialized file → materialize → 409 "Materialization aborted; nothing was written" with the per-file refusal and the transcribe-or-force hint → status: lastSuccess untouched, lastAttempt advanced with the full reason, lastRun.atomic=false with refused[], stalePaths names the drifted file → force → 200, lastSuccess advances, atomic=true, written=[feature.md], stalePaths=[].
- Deviations (declared in task logs, review-verified): relativePath key on run-report wire (054 consistency), projectId in the status payload (037), report-absence pinned below the API layer (plan-phase aborts unreachable through doors — slug validation), Off-strategy runs do not stamp (documented).
- Documented contract boundary (accepted, user-visible on the done gate): write-phase IO failure compensates (rolls back rows, removes new files) but does not restore pre-existing overwritten files — self-heals on the next force; plan-phase aborts stamp but persist no report ("no report for this attempt" rendering).
</evidence>

<deferred>
- Microbatch: W3 Archive-strategy drift dead-zone in stalePaths (description clause or detector widening); W3-old success-report save outside the write tx (crash window); AllSkipped dead enum member (500 if resurrected); GuardOverwrites directory-wording.
- Policy flag for the user: 067-D2 skip-not-abort superseded by 074 atomicity — auto-sync on every board mutation now aborts (and stamps a failure) when a hand-edited or untracked file sits on the plan path; force adopts. If the user prefers silent-skip, atomicity needs rework.
</deferred>

<refs>
Spec 1f69dd0b (validated); plan b8e98220 (validated); tasks 034fdace + 32cf4759 (both done); audits review-074-01.md (+re-check section), review-074-02.md (+fix round); BobHouse 020 note (entry c7f34f40); 054/033/067/042/037 precedents.
</refs>