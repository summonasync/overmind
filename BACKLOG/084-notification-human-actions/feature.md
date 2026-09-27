---
feature: 084
slug: notification-human-actions
status: inProgress
priority: p1
version: 1
created: 2026-09-26
---

# Feature 084 — Notification center: human actions (approval/question) — видимость, прямые ссылки на overmind, actionability

<goal>
Human actions requested by agents (approval/question notifications) are currently tucked inside the bell modal — easy to miss, no way to act from where the human actually is. Make them LOUD and directly actionable: the human must SEE that an agent awaits their call, and reach the exact overmind surface in one click (direct link into the project/feature/tab). Approval actions should feed the 077 proof mechanism directly (a reply/approve on the notification IS the validation proof).
</goal>

<requirements>
- Density of the open questions is deliberate — to be refined into a full spec via Socratic pass:
- Visibility: how does a pending human action break out of the bell modal? Candidates: OS-level notifications (host-side, linux desktop), a persistent web banner/toast while any approval is unanswered, unread-state styling, sound. Decide per surface, avoid spam (dedup/debounce rules for agent floods).
- Direct link: every notification deep-links into the overmind UI at the exact target (project → feature → tab) with the host port/URL resolved — one click from OS notification or banner to the action surface.
- Actionability: can the human approve/reply WITHOUT opening the feature page (inline reply in the notification center is already there; consider approve/reject buttons on approval kind)? Actions must integrate with 077 (reply = proof) — approve semantics must stay provable.
- Lifecycle: what happens when the agent's request is answered/voided (feature moved on, proof consumed) — stale action cleanup so the center doesn't nag forever.
</requirements>

<non-goals>
- Multi-user/auth model (localhost trust posture stands); mobile; push to external services.
</non-goals>

<refs>
- 020-agent-notifications (bell, center, reply path, deep links as plain anchors).
- 077-validation-provenance (done): approval reply = validation proof — approve UX feeds it directly.
- 082-п.3 shares the "resources pointer" cleanup only; no overlap here.
</refs>

<log>
- 2026-09-27 [auto] — artifact created → inProgress
</log>
