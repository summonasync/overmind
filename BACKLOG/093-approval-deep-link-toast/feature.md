---
feature: 093
slug: approval-deep-link-toast
status: done
priority: p1
version: 1
created: 2026-09-27
---

# Feature 093 — Approval reach: agent quotes a deep-link in chat + global approval toast in an open Overmind

<goal>
When an agent requests an approval, the human has to hunt for it: the chat reply carries no link, and an open Overmind tab stays silent until the human happens to look at the bell. Approval latency is pure dead wait. Two complementary channels requested by the user ("возможно нужны оба варианта"): (1) the agent's chat reply contains a ready link to the exact approval, (2) an already-open Overmind surfaces the new approval immediately, on any page.
</goal>

<requirements>
- Deep-link in tool response: `overmind_notify` response carries a ready-to-paste URL resolving to the notification/approval (feature + tab deep link the UI already understands); agent-facing contract (tool description + session bootstrap line) instructs the agent to quote that URL whenever it asks for an approval or reports a question.
- Live toast in an open Overmind: a new approval/question notification triggers a global toast (any page, not only pages that poll the bell) — short text + direct actions [Approve] [Open]; title-tab attention hint (blink/counter) since a browser tab cannot be focused programmatically.
- Delivery mechanism for the toast is a spec question (poll cadence vs SSE/WS) — must not regress current bell behavior (088 quick actions stay).
</requirements>

<non-goals>
- Focusing/popup-hijacking of browser tabs (platform forbids; toast + title blink is the honest substitute).
- Desktop-notification OS integration (possible follow-up, not v1).
- Changing notification data model (kinds/read/reply flow untouched).
</non-goals>

<refs>
- User intake 2026-09-27 ("агент просит аппрув — возвращает в чат ссылку... или аппрув открывался в открытом и активном overmind, возможно нужны оба").
- 084-notification-human-actions — sibling surface; spec must draw the boundary (093 = delivery/latency channel, 084 = the center/actions page itself).
- 088-bell-approval-quick-actions — existing bell approve/reject buttons to reuse in the toast.
- 020 — notification agent↔human path (notify/reply, cap 50, unreadCount).
</refs>

<log>
- 2026-09-28 [auto] — artifact created → inProgress
- 2026-09-28 [auto] — all tasks done + spec + plan validated → review
- 2026-09-29 [agent] — by user request in-session («ну чего, мужик, закрывай задачку», 2026-09-29): result b3a499ef validated пруфом ba569102, 3/3 тасок done, все ревью позади (2×PASS, PASS-WITH-NITS, architect APPROVED), 1644/1644 + live e2e зелёные
</log>
