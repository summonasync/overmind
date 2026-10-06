---
feature: 095
slug: os-desktop-notifications
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Result 095 — Attention surfaces: OS desktop toasts + web sound + browser tab attention для approval/question

## What shipped

Approvals and questions now reach the user when the tab is hidden — one client module over the 093 pipeline, three surfaces, hidden-only:

- **OS toast** (Web Notifications API): permission via the settings toggle «Desktop notifications» (honest granted/denied/unsupported/default states; the prompt fires only on the user's click, never on mount; toasts default off until granted). Body per the 101 hierarchy — project prominent in the title, feature · agent + trimmed text in the body; click → `window.focus()` + `window.location.assign` along the 093 deep-link (ADR-conformant navigation, no client router), plus attention clear.
- **Chirp** (Web Audio, synthesized — no asset pipeline), settings toggle, hidden-only.
- **Tab attention**: favicon swaps to a runtime-built SVG badge variant over the captured default (separate `link[rel=icon]` writer, no blink loop); «(N)» title prefix composed THROUGH the 107 title owner as its third input — `titleFor(project, awaiting, attention)` with `max()` semantics, single writer of `document.title` preserved (grep-verified). Clears on focus / visibilitychange(visible) / toast click.
- **Dedup + prime**: firedIds make one surface per event; the prime happens on the first non-pending notifications sync (review blocker B1 fixed — mount-time prime would have toasted the whole backlog on a hidden load), so no storm on cold start; a new id while hidden re-raises. pruneAttention trims both seen and attention ids against live awaiting (bounded growth).
- **Settings section** on the settings page: both toggles with honest browser-side permission rendering.
- Storage: `overmind.attention.*` localStorage (web-only booleans, browser-owned permission — deliberately not backend settings).

## Review rounds

- T1 light review round 1 **BLOCKED** (B1: prime on mount → toast storm) → orchestrator fix round: prime gated on `isPending`, DesktopPermission barrel export, firedIds pruning bounded; prune pin updated. Delta review **PASS**.
- Architect light **APPROVED-WITH-WARNINGS** (audits/architect-095.md): W1 (router.push vs the static-export ADR) fixed in-tree with `window.location.assign`; W2 recorded — the bubble gates awaiting by `readAt`, attention by `hiddenAt` (same source query, no duplicate noise; unification is a candidate microtask). Residual re-fire edge (an item leaving and returning to awaiting re-toasts) accepted per the spec's re-raise semantics.

## Gates (re-run by orchestrator after every round)

web: lint 0 errors (6 pre-existing), tsc clean, 242/242 node --test (+25 attention pins), build ok.

## Microbatch

#243 error-state first-sync micro-gap (prime gate `isPending || error`), plus the W2 predicate-unification candidate and the re-fire edge note.

## Post-release click-check (permission needs the human)

1. Settings → Attention → toggle «Desktop notifications» → browser prompt → Allow.
2. Hide the tab; from another window reply nothing but create an approval (or have an agent request one) → OS toast arrives (project in title, feature · agent in body) + chirp + favicon badge + «(N)» tab title.
3. Click the toast → tab focuses, deep-links to the bubble, badge/title clear.
4. Deny the permission → toggles show the honest blocked hint, toasts stay silent, chirp+tab attention still work.
5. Tab focused → nothing fires anywhere except the 093 bubble (no duplicates).
6. Cold load with a hidden tab and a non-empty backlog → zero toasts (prime-on-first-sync).
7. Safari: toast click focuses the tab — if window.focus() loses there, the deep-link still lands (known nit, accepted).

<log>
- 2026-10-06 [agent] — closeout: review cycle complete (BLOCKED→fix→delta PASS), architect light APPROVED-WITH-WARNINGS with W1 fixed in-tree and W2 documented (audits/architect-095.md), result drafted with the 7-step permission click-check. Feature goes to review; done-flip waits on the human tap.
</log>