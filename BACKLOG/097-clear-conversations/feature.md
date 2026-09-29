---
feature: 097
slug: clear-conversations
status: done
priority: p0
version: 2
created: 2026-09-29
---

# Feature 097 — Clear conversations: guarded bulk-delete of replied notifications (077 proof-aware)

<goal>
The user's bell fills with replied notification rows (every human answer "approved"/"ок"/"+" lives there forever). 096 gave per-row × (works on replied rows) and bulk clean of read rows — but deliberately excluded replied rows from bulk because they carry 077 validation proofs. Field report 2026-09-29: the user's bell was 5 screens deep, "Clear read" returned 200 with nothing cleaned (all rows replied), and manual × clicking was the only escape. A proof-aware bulk delete closes the loop.
</goal>

<requirements>
- Agent-facing nothing; this is the human door: POST /api/notifications/clear-conversations (or DELETE verb variant per API conventions) — bulk-delete replied rows that are NO LONGER live proofs.
- Proof guard (the 077 contract, server-side): a replied row is a live proof iff some non-terminal artifact (spec/plan/result not validated, task not done) has UpdatedAt <= row.RepliedAt. Live-proof rows are skipped; the rest are deleted. Response: { deleted: N, protected: M } so the UI can explain itself.
- Rationale: validation materializes the proof line into the artifact log ("validated with human proof: … replied at ts") — after that the DB row is redundant; only pending artifacts need their rows alive.
- Web: "Clear conversations" button in the notification center header next to "Clear read" (disabled when no replied rows), optimistic patch aligned with the 096 mutation idiom, protected-count surfaced honestly (e.g. tooltip/badge "M kept as validation proofs") instead of a silent no-op.
- The clear-read reply-exclusion (096 spec v2) stays as is; this feature is the explicit, guarded counterpart.
</requirements>

<non-goals>
- Removing the 077 proof mechanism or weakening validation gates.
- Touching per-row × semantics (it stays the explicit proof-row remover).
- Hiding/archiving instead of deleting (hard delete matches 096's decision).
- Desktop/OS notifications (095), toast system (093 scope closed).
</non-goals>

<refs>
- Field report 2026-09-29 (user: «clear read не работает… ничего не чистится», крестики работают по одному) + original intake 2026-09-28 («мне критически не хватает возможности чистить notifications»).
- 096-notification-cleanup — the exclusion this feature replaces with a guarded bulk; architect-review-2026-09-28-096.md finding 1 (proof-row overlap) is the constraint source.
- 077 validation-proof protocol — BuildHumanProofLineAsync (PlanningArtifactService.cs:417-436) searches replied rows (ReplyText != null && RepliedAt >= artifact.UpdatedAt); artifacts' terminal statuses freeze them.
- 084 invariant reply⇒read — replied rows are read; clear-read skip logic lives in NotificationsService.ClearReadAsync.
</refs>

<log>
- 2026-09-29 [auto] — artifact created → inProgress
- 2026-09-29 [auto] — all tasks done + spec + plan validated → review
- 2026-09-29 [agent] — by user request in-session: both tasks done (AC 6/6), reviews PASS-WITH-NITS ×2, architect APPROVED-WITH-WARNINGS, result fdacb923 validated, live e2e green; review NIT-2 obstacles premise fixed at closeout — feature closed
</log>
