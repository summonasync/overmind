---
feature: 111
slug: brief-cap-500
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Spec 111 — Board card brief cap: 150 → 500 text elements (user dogfood feedback)

> User dogfood feedback 2026-10-04, approved in chat («450-600... карточки будут выглядеть только лучше» — taking 500).

## Goal

More goal text on the board card: the brief excerpt cap moves from 150 to 500 text elements. Same derivation, same safety: first non-empty `<goal>` paragraph, StringInfo text elements (surrogate pairs never split), «…» on overflow, null on absent goal/body/blank goal — nothing changes but the constant.

## Decisions

- Single constant `BriefCapLength = 500` in `CeremonyPhasePlanner.BriefExcerpt` (091) — one source feeds board cards AND `planning_task_queue` briefExcerpt (091 parity contract stays).
- No line-clamp introduced: the 091 decision was «bounded upstream, plain <p>» — the bound just moves. Visual check rides the release.
- Tests: the 091 cap matrix (150/151 boundary, cyrillic long goal, surrogate pairs) re-pinned at 500/501.

## Non-goals

- Multi-paragraph briefs; per-project cap config; queue-specific caps.

<log>
- 2026-10-04 [agent] — user dogfood feedback in session: cap 150 → 500, approved in chat; one constant, matrix re-pinned.
- 2026-10-04 [agent] — validated with human proof: notification e21a3661-fa22-428f-961c-159ee83f044b replied 2026-10-04 16:36:37 UTC
</log>