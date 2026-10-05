---
feature: 092
slug: waits-for-closed-blockers-bug
status: done
priority: p1
version: 1
created: 2026-09-27
---

# Feature 092 — Bug: "waits for" shows done/review blockers as waiting (feature page)

<goal>
Expected: the feature page dependency block "waits for" shows only blockers the feature is actually gated by. Actual: done and review features appear in the list indistinguishable from open ones — a closed blocker reads as "waiting", which is wrong (a dependency closes when its blocker reaches done or review).
</goal>

<requirements>
- "Waits for" renders only unclosed blockers (blocker status not in {done, review}).
- Closed blockers either disappear from the list or move to a visually separated muted group ("closed blockers") — spec decides, hiding is the default lean.
- The same filter applies to any surface reusing the dependency list (feature page panel; MCP list_feature_dependencies already returns closed flags — data layer needs no change).
</requirements>

<non-goals>
- Changing dependency data/closure semantics (030 is correct — closure = done|review).
- MCP payload changes.
</non-goals>

<refs>
- Bug report by user 2026-09-27 ("на странице фичи есть WAITS for — blocker, и там в списке есть даже done/review — залёт-воин").
- 030-feature-dependencies — closure semantics (closed = done|review), web Dependencies panel.
</refs>

<log>
- 2026-10-04 [auto] — artifact created → inProgress
- 2026-10-04 [auto] — all tasks done + spec + plan validated → review
- 2026-10-05 [agent] — by user request in-session — result 0dbbc7db validated on the user's notification tap (reply 2026-10-04T22:35Z, found buried in replies inventory); bug wave closed
</log>
