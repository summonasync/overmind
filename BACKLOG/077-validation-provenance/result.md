---
feature: 077
slug: validation-provenance
status: draft
created: 2026-09-26
---

# Result 077 — Validation provenance: planning_validate requires proof of human approval (token or explicit human path)

<outcome>
Feature 077 shipped in full: agent-door validation of spec/plan now requires server-side proof of human approval (spec R1–R6). The proof is an Approval notification bound to the feature (slug + ProjectId match) with a non-empty human reply, and the artifact untouched since the reply — no proof → typed 409 ValidationProofRequired with actionable text. Zero new agent-facing parameters: the server finds the proof itself (user decision D2); the ceremony costs one canonical notify call. Every validation now writes a sourced log line — "[ui] — validated via UI" on the human door, "[agent] — validated with human proof: notification <id> replied <ts> UTC" on the agent door (closes microbatch #43). Gate-on projects keep the STOP-first precedence; asMigrated and task/result validation untouched; human door stays free.

Behavior change to communicate at deploy: agents on humanGate=off projects can no longer validate spec/plan unilaterally — the notify → bell-reply → validate flow is now mandatory, and any edit to the artifact voids the reply (re-validation needs a fresh one).
</outcome>

<evidence>
- Tests: 1482 → 1500 (+18; Core 215, Infrastructure 492, Api 438, Mcp 355; reviewer-verified count). Build 0 warnings / 0 errors throughout.
- Reviews: audits/review-077-01.md (PASS-WITH-NITS; W-1 cross-project proof binding + N-1 unreplied pin fixed in-tree), review-077-02.md (PASS-WITH-NITS; texts byte-accurate, W-1 count correction logged).
- E2E dogfood on an isolated host (scratch HOME, port 4799, kill by PID; prod untouched), 10/10 steps verbatim with zero product deviations: 409 without notification; unreplied approval → 409; human reply "lgtm" → validate 200 with the proof line carrying id+ts; one reply covered spec AND plan (proof by feature); patch demote + edit → revalidate 409 (stale proof); cross-project same-slug reply → 409 (ProjectId binding); human door free ("[ui] — validated via UI"); gate-on → HumanValidationRequired STOP before proof logic.
</evidence>

<deviations>
- Proof is single-use by construction (validate bumps UpdatedAt); even a retried validate needs a fresh reply — fail-closed, documented in the tool description.
- MCP error surface shows the ProblemDetails detail only; the ValidationProofRequired code name stays HTTP-level (pinned).
- 078 guard rejected full-body obstacles upserts twice during execution (append-only working as designed) — executor notes landed as log lines instead; guard error diff → microbatch #94.
</deviations>

<follow-ups>
- W-2 RESOLVED (user, 2026-09-26): reply semantics stay as-is (any non-empty reply = approval). Future idea by the user: once the crew builds its internal copilot, add a reply-validation function to it — revisit deny-semantics/approve-button then.
- Microbatch #95 (bootstrap active-branch pin), #94 (guard error diff), #92 (quickstart promotion wording) and the rest of the standing queue.
</follow-ups>

<refs>
- Spec 55333889 (validated), plan 114c3e53 (validated), tasks 5575c49f / cda56b18 (both done).
- 055 gate/asMigrated, 043 sourced lines, 068 door sources + demote rule, 020 notification entity.
</refs>