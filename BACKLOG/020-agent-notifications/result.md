---
feature: 020
slug: agent-notifications
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Result 020 — Agent → human notifications via MCP with deep links + reply path

<outcome>
020-agent-notifications shipped end to end: agents push notifications to the human via two new MCP tools (overmind_notify with kind approval/question/info + target featureSlug/tab + free-form agent identity; overmind_replies as the poll-back channel), backed by an AgentNotification table and a both-doors NotificationsApi (agent create/replies; human list with unreadCount cap 50, idempotent mark-read, one-reply-overwrites). The web gained a topbar bell with an unread badge (capped "50+") opening a notification center: kind tones, agent + relative time, plain-anchor deep links into /dashboard?project&feature&tab (fire-and-forget mark-read on click), reply inputs for question/approval showing the sent answer once set, read items dimmed — all fresh via the 017 polling. Project resolution in the tools rides the 013 chain (explicit projectId → OVERMIND_PROJECT_ID → onboard cwd), with a clear call-overmind_onboard error on failure.

Tool surface 16 → 18 (contract pin grown). Suite 831 → 856 across the backend tasks. Reviews: 01 PASS, 02 PASS, 03 PASS-WITH-NITS with WARN-1 (silent reply failure) fixed in-tree — ErrorText under the form; a failed reply no longer looks sent.
</outcome>

<evidence>
- LIVE e2e green on an isolated dev host (scratch cwd DB, port 4799; production 0.1.7 host untouched, verified alive after): agent-door notify (question, target 017-live-ui-sync/spec, from claude) → 201 with the full target and projectId on the item → human list shows unread 1 → mark-read → 0 → human reply "Ship 15s, revisit with SSE later" → the agent-door replies endpoint returns exactly that text with RepliedAt.
- dotnet build 0w/0e; dotnet test 856/856 (Core 106, Infra 283, Mcp 213, Api 254): lifecycle both doors, idempotent read pinned by timestamp, reply overwrite, includeRead both values, cap semantics (52 unread → count 52 under a 50-item list), 400/404 theories, tool contract tests (posted JSON shape, omission, env/onboard resolution branches, failure path).
- includeRead defaults TRUE on replies (review-upheld: a human's early click must not hide the answer from the agent forever); unknown projectId → 404.
- Web: lint 0 errors / tsc 0 / build green (10/10 routes) — re-verified after the in-tree WARN-1 fix.
- Reviews: audits/review-020-01.md (PASS), review-020-02.md (PASS), review-020-03.md (PASS-WITH-NITS, WARN-1 fixed in-tree).
</evidence>

<deferred>
- Microbatch: keepalive on fire-and-forget markRead (navigation can kill the POST; recovery = button + poll); promote the compact relative-time formatter to shared/lib (FSD forced a local duplicate); redundant markingThis guard; badge without aria-live (AT does not hear new notifications); onboard-200-broken-body branch unpinned; 503-on-onboard maps to a semantic-search error without an onboard hint; env-parse helper duplication ×2 (extract on the third copy).
- UI visual pass + packed release (0.1.8) pending — same posture as the session's features; the bell renders in the production UI only after the pack.
- External channels (email/telegram/webhook) and realtime chat remain out (the feature body's own non-goals).
</deferred>
