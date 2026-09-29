---
feature: 093
slug: approval-deep-link-toast
status: validated
created: 2026-09-28
validated: 2026-09-28
---

# Spec 093 — Approval reach: agent quotes a deep-link in chat + global approval toast in an open Overmind

<goal>
Kill approval dead-wait at both ends: (1) an agent asking for an approval quotes a ready URL in chat, so the human clicks instead of hunting; (2) an already-open Overmind surfaces new approvals/questions immediately on any page and marks the browser tab as needing attention. Research finding (2026-09-28): the "global toast" half already shipped — the 084 zerling ApprovalBubble is a global all-pages surface with Approve/Reject/Open, quiet/nag discipline and dead-target detection. This feature therefore shrinks to the actual delta: the quoted-URL channel, question-kind surfacing in the bubble, and the title-tab hint.
</goal>

<decisions>
- Delivery mechanism (the body's open spec question): the existing global poll. useNotifications already polls every LIVE_SYNC_INTERVAL_MS (15s) from the app-shell on every page; the bubble derives from that query. No SSE/WebSocket, no new poll cadence in v1 — if 15s latency bites, a per-query acceleration is a follow-up, not a redesign.
- The "toast" is the existing ApprovalBubble (widgets/app-shell), evolved — not a new toaster. Web has no generic toast infra and does not need one for this.
- The deep-link URL is built by the HOST, not the adapter: the host knows its public browser URI (BrowserUri.FromLoopback over the bound address — Program.cs precedent). POST /api/agent/notifications response gains url: absolute <browser-origin>/dashboard?project=…&feature=…&tab=…, present when featureSlug is set (project param optional, mirroring notificationHref). The adapter just proxies the field through — no origin heuristics duplicated in the stdio process.
- Agent-side instruction surface: overmind_notify tool description + the adapter session bootstrap (ServerInstructions / SessionBootstrapPrompt) gain a line: when asking for an approval or reporting a question, quote the returned url in chat.
- Question kind joins the bubble: the awaiting filter widens from approval-only to {approval, question}, unread and not yet replied. Question rows render text + Open → (typed answers stay in the notification center); approval rows keep Approve/Reject/Open.
- Title-tab attention hint: while ≥1 approval/question awaits, document.title carries an "(N) " prefix counter; resets when the set empties. Plain counter, no blink — blink is noise, the counter is the honest minimal signal.
</decisions>

<open-questions>
- none pending — decisions above need the user gate check only
</open-questions>

<requirements>
Host/MCP:
- Notification-create response (additive contract change only): host computes its browser origin once (BrowserUri.FromLoopback of the bound URL) and stamps url onto created notifications that carry featureSlug; no url field when there is none.
- OvermindNotificationTools.Notify proxies url in the tool response; tool description instructs quoting it for approvals/questions; session bootstrap line updated to match.
Web:
- Bubble awaiting filter widens to question kind (unread, unreplied); question variant renders without Approve/Reject.
- document.title "(N)" counter while the awaiting set is non-empty (browser-API effect, static-export safe).
- Bell/center/quick actions untouched — 084/088/096 invariants hold (reply⇒read, proof rows survive clear-read, cleanup buttons).
</requirements>

<non-goals>
- SSE/WebSocket push — <removed 2026-09-28 — existing 15s poll already delivers the bubble globally; revisit only if latency proves painful>.
- OS desktop notifications — that is 095's surface.
- Browser-tab focus/popup hijacking (platform forbids; bubble + title counter is the honest substitute).
- Generic toast system, poll cadence change, notification data-model changes.
</non-goals>

<acceptance-ideas>
- Agent calls overmind_notify(approval, …, featureSlug, tab) → tool response contains url="…/dashboard?project=…&feature=…&tab=…"; pasting it in a browser lands on the feature page's tab.
- A question notification appears in the bubble on any page within ~15s, with Open; approving from the bubble still replies and marks read (084 invariant).
- With one awaiting item the tab title shows "(1) …"; after approve/reject/read it drops back.
- Contract pins: Api tests pin url presence/absence; Mcp tests pin the url passthrough.
</acceptance-ideas>

<refs>
- Feature body intake 2026-09-27 («возможно нужны оба варианта» — both channels, kept).
- 084-notification-human-actions — the bubble this feature evolves; boundary: 084 = actions/invariants, 093 = reach/latency channels.
- 088-bell-approval-quick-actions — APPROVAL_REPLY constants reused.
- 020-agent-notifications — notify/reply path, LIVE_SYNC_INTERVAL_MS polling, deep-link param format.
- 095-os-desktop-notifications — sibling channel, unaffected.
- 096-notification-cleanup — deep links mortal (deleted rows); the bubble already surfaces dead targets; url targets features, not notification rows.
</refs>

<log>
- 2026-09-28 [agent] — spec drafted from feature body + 2026-09-28 code research: 084 bubble already covers the global-toast half; scope narrowed to quoted URL + question kind + title hint.
- 2026-09-28 [agent] — validated with human proof: notification ac60d4e9-3bbc-42fc-8b1f-86e79d382acc replied 2026-09-28 14:27:41 UTC
</log>