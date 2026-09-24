---
feature: 055
slug: human-validation-gate-default
status: done
priority: p1
version: 1
created: 2026-09-23
---

# Feature 055 — Human validation gate off by default — agent freezes spec/plan unattended (OM-3)

Field report OM-3 (FEATURELIST/overmind-features-2026-09-23.md): `planning_validate` on mmt-36-2 spec and plan passed immediately ("Artifact validated.") with no STOP — the human gate resolves via `Project.RequireHumanValidation` override falling back to the global `planning.requireHumanValidationDefault`, and the default is off.

In the field case this was acceptable (artifacts pre-approved on disk as frozen v9/v3 before migration), but in the general case an agent can freeze a spec with no human in the loop — our own process (CLAUDE.md rule 7) makes spec/plan validation a user gate, and the server must enforce it, not the prompt.

Requests from the report: enable the gate for the DMT project (or flip the default); show gate state in onboard (delivered by the rendering task on 042); optionally a validate flag "as migrated, already approved by user" with mandatory logNote so migrations don't require UI clicks.

<log>
- 2026-09-23 [auto] — artifact created → inProgress
- 2026-09-24 [agent] — Closing by user mandate 2026-09-24: single task done with light review PASS-WITH-NITS (audits/review-055-01.md); all three AC met (gate STOP covered via per-project override tests, onboard visibility delivered by 042). The global default flip (planning.requireHumanValidationDefault → on) remains PENDING USER SIGN-OFF by the task's own constraints — one settings line, offered on return.
</log>
