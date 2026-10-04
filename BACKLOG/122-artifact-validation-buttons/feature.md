---
feature: 122
slug: artifact-validation-buttons
status: done
priority: p1
version: 1
created: 2026-10-04
---

# Feature 122 — Approve/Decline buttons on artifact views (human validation door)

<goal>
The human validation door should live ON the artifact, not only in the notification round-trip: spec/plan/result views need Approve / Decline buttons so the user can validate without the agent asking first and tapping a notification.
</goal>

<context>
User intake 2026-10-04: «у меня нет в ui на spec и plan возможности завалидировать это — надо подумать. На данный момент валидация только от тебя — ты просишь, я тапаю. Но так — нет кнопок Approve/Decline». Existing machinery: humanGate=on mode already has an UI approval path (planning_validate returns STOP, user approves in UI); this project runs humanGate=off where the only proof channel is notify→reply. Design questions for grooming: do UI approvals write the same proof record the notify-reply writes (agent's planning_validate must accept a FRESH UI approval as proof)? Where do buttons live (artifact view header?), what does Decline carry (reason → [ui] log line), does it apply to result too, how does it interact with the demote-on-edit rule (approval voided by a later edit — same freshness rule as notify proofs).
</context>

<log>
- 2026-10-04 [auto] — artifact created → inProgress
- 2026-10-04 [agent] — Van by claude claimed the feature
- 2026-10-04 [auto] — all tasks done + spec + plan validated → review
- 2026-10-04 [agent] — agent by unknown released the claim
- 2026-10-04 [agent] — by user request in-session — result 22cafda3 validated on the user's notification tap (reply 2026-10-04T20:44Z), wave closed
</log>
