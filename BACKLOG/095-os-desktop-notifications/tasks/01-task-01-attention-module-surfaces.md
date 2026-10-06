---
feature: 095
task: 01
slug: os-desktop-notifications
status: done
review: light
created: 2026-10-04
brief: Web: attention module — hidden-only OS toast + chirp + favicon/title attention through the 107 owner + tests
---

# Task 01 — Attention module + surfaces

<goal>
Approvals/questions reach the user when the tab is hidden — toast, chirp, tab attention.
</goal>

<scope>
- One client module over the 093 pipeline: hidden-only OS toast (Web Notifications API; permission via a settings-page toggle «Desktop notifications» with granted/denied/blocked states; body per the 101 hierarchy — project prominent, feature/agent secondary; click focuses the tab + follows the 093 deep-link url).
- Hidden-only chirp (Web Audio, built-in, settings toggle, no asset pipeline).
- Tab attention: favicon swap to an attention variant + «(N)» title prefix composed THROUGH the 107 title owner (third input: attention count); clears on tab focus / after opening the notification; no private blink loop.
- Web tests: trigger matrix (hidden/focus × approval/question), permission-state rendering, attention compose/clear.
</scope>

<anti-scope>
- Native/Electron layer; per-project sound; non-attention kinds; mobile push.
</anti-scope>

<acceptance-criteria>
- [x] Trigger matrix pinned (no duplicates in focus; re-raise while hidden).
- [x] Permission states rendered; settings toggles wired.
- [x] Attention composes through the title owner; clears on focus/open — pinned.
- [x] Gates green (lint/tsc/node test).
</acceptance-criteria>

<refs>
- Spec 12623237; plan 6b95d68b (task 1); 107 title owner; 093 url contract; 087/093 pipeline.
</refs>

<log>
- 2026-10-05 [agent] — taken by orchestrator — dispatching web-coder (sonnet); permission click-check deferred to post-release per the 101 precedent (code + unit matrix now, human click after deploy)
- 2026-10-06 [agent] — web-coder: attention module landed — pure core (attention-plan decision matrix, attention-toast-copy 101 hierarchy, desktop-toggle-state mapping, attention-favicon SVG builder), browser wrappers (desktop-notification, chirp WebAudio), external attention store composed through the 107 owner (useTabTitle third input, max(awaiting, attention)), use-attention-surfaces mounted in AppShell, AttentionSettingsSection on the settings page (desktop toggle with granted/denied/blocked/unsupported honest states + chirp toggle, localStorage prefs). Conservative calls: first notifications sync primes seen-ids without firing (no backlog storm); attention badge = max(awaiting, attention); attention favicon is a runtime SVG data-URL badge over the captured default icon (no asset pipeline); chirp defaults on, desktop toast defaults off pending permission. Gates: npm test 242/242 exit 0 (5 new test files incl. Notification API mock), lint 0 errors (6 pre-existing warnings, none in new files), tsc --noEmit exit 0, next build exit 0. Took over the stale Van claim to log.
- 2026-10-06 [agent] — web-coder: POST-RELEASE CLICK-CHECK (human, per the 101 precedent — real permission click cannot be automated): (1) open Settings, flip "Desktop notifications" on → browser shows the permission prompt → Allow → toggle stays checked; (2) hide the Overmind tab, have an agent fire an approval/question → OS toast arrives with project as title, feature · agent + text in the body, chirp plays (if "Attention sound" on), favicon gains the amber badge and the title shows the (N) prefix; (3) click the toast → tab focuses and lands on the 093 deep link, favicon badge and attention prefix clear; (4) deny path: with permission denied the toggle renders disabled with the "Blocked by the browser" warning and toasts stay silent while hidden; (5) browser-settings-blocked state renders the same honest warning on the settings page; (6) in-focus regression: approval shows the bubble only — no toast, no chirp, no favicon swap. Record the outcome in this task log or the 02 result.
- 2026-10-06 [agent] — review round 1: BLOCKED with blocker B1 — prime fired on mount with an empty React Query cache, the backlog arriving later would re-raise everything unseen (toast storm on a hidden load). Orchestrator fix: prime gated on isPending (deps updated); nits fixed in the same round: DesktopPermission exported from the barrel (deep import removed), pruneAttention trims firedIds against live awaitingIds (bounded growth, prune pin updated). Gates re-run: lint 0e/6 pre-existing, tsc clean, 242/242, build ok. Deferred by design: settings permission hint does not react to browser-side permission changes until the next render (post-release click-check covers the real flow); window.focus() may not raise the tab in Safari (accepted, click-check verifies).
- 2026-10-06 [agent] — review round 1 BLOCKED (B1 toast-storm on hidden load: prime on mount instead of first sync) -> orchestrator fix round: prime gated on isPending, barrel export for DesktopPermission, firedIds pruning bounded; prune pin updated honestly. Delta needs a re-review before done — re-dispatching web-code-reviewer on the fix diff.
- 2026-10-06 [agent] — delta review PASS: B1 (prime gated on isPending) verified with deps order, nits 1-2 closed (barrel export, firedIds pruning with reference-identity preservation), nits 3-4 deferred under the click-check per log. Gates re-run by orchestrator: 242/242, lint 0e/6 pre-existing, tsc clean, build ok. Residual micro-gap (error-state first sync) -> microbatch #243.
</log>
