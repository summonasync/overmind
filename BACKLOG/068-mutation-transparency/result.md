---
feature: 068
slug: mutation-transparency
status: draft
created: 2026-09-26
---

# Result 068 — Mutation transparency: маркеры на всех путях записи, statusChanged в ответах, current_task различает result-состояния (OM-11/12/13)

<outcome>
Feature 068 shipped in full: R1–R7 of the validated spec. Four tasks (01 markers, 02 statusChanged echo + term sweep, 03 feature demote, 04 honest current_task), each closed with a light/hard review and fix-rounds; the hard review (068/03) ended RE-APPROVED after two fix rounds.

State-machine truth achieved: every <log> write path stamps its door source ([agent]/[ui]/[auto]/[import] — no unmarked line can be produced anymore, importer included); patch/upsert/set_status echoes carry statusChanged ("validated → draft") and featureStatusChanged ("review → inProgress"); the feature follows its spec/plan out of review on ANY demotion path (upsert, patch/tick, manual set_status — user decision D1 extended to the manual door); auto-promotion to review now REQUIRES spec+plan validated (user decision: tighten, closing the forward side of the invariant); current_task tells the three result states apart verbatim, returns state "held" when only review/blocked tasks remain, and carries soft warnings ("spec missing"/"spec not validated"/"plan not validated") on task payloads. "Frozen" is swept from machine surfaces, locked by a grep gate; "validated" is the single term.

Behavior change to communicate at deploy: a feature with a draft spec/plan no longer auto-promotes to review when its last task closes (previously existence-only, 023 behavior) — it stays inProgress until the documents are validated. The review invariant is now two-sided enforced: review ⟺ spec+plan validated AND all tasks done.
</outcome>

<evidence>
- Tests: 1441 → 1482 (+41; Core 215, Infrastructure 484, Api 431, Mcp 352). Build 0 warnings / 0 errors throughout. dotnet format clean on every delta.
- Reviews: audits/review-068-01.md (PASS-WITH-NITS, W1/W2 fixed), review-068-02.md (PASS-WITH-NITS, W1/S1/N1 fixed), review-068-03-hard.md (APPROVED-WITH-WARNINGS → fix-round → RE-APPROVED-WITH-WARNINGS → fix-round-2, both IMPORTANTs user-decided and closed), review-068-04.md (PASS-WITH-NITS, S2/S3 fixed).
- E2E dogfood on an isolated host (scratch HOME, port 4799, kill by PID; prod 4761 untouched), all steps verbatim-verified: warnings lifecycle (missing → not-validated → key absent on the wire); tightened promotion fired live (feature → review); patch of the validated spec returned {"statusChanged":"validated → draft","featureStatusChanged":"review → inProgress"} with both [auto] lines in spec and feature logs; current_task returned the three result reasons verbatim ("All tasks are done — write the result artifact." / "Result artifact drafted — validate it." / "Result validated — close the feature."). Zero product deviations; one agent-side jq slip (404 on a lost artifact id), retried clean.
- Absorbed: microbatch #45 (append_log markers). New absorbed-by-083: none here.
</evidence>

<deviations>
- frozenAt field / IsFrozen / RequireChangeNoteWhenFrozen identifiers kept (renaming = response-shape break; D2 renamed wording only) — user follow-up candidate.
- SetStatus validated→draft writes the [auto] FEATURE demote line but no artifact-level [auto] line (R2 covers patch/upsert artifact lines; manual-door feature consequence covered by FIX B).
- statusChanged/featureStatusChanged are [NotMapped] per-mutation echoes — null after reload, never stale.
- MCP batch upsert echo stays compact without statusChanged (SUGGESTION-1 → microbatch #87).
- current_task result detection takes the first result artifact (no multi-result rule; ordering pin → microbatch #90).
- Concurrent-door last-write-wins on feature Status (no concurrency token) — documented, both log lines survive.
</deviations>

<follow-ups>
- Microbatch queue #85–#91 (Source-on-request ADR or removal, CreateTasks ForDoor-split, ValidateAsync promotion re-check, batch echo, guard try/catch placement, result ordering pin, Canned brief drift).
- D2 follow-up decision for the user: rename frozenAt/IsFrozen identifiers or keep with an ADR line.
- Field verification after deploy: agents on live projects see statusChanged/featureStatusChanged and held/warnings on their surfaces (needs MCP adapter restart on sessions).
</follow-ups>

<refs>
- Spec 9f1fa31e (validated), plan 4d6ded97 (validated), tasks a9e880ba / 1247f14a / e7945fc8 / 47edf953 (all done).
- 043 marker channel, 023/06 demote pattern, 030/04 resolver location, 078 log-guard wording.
</refs>