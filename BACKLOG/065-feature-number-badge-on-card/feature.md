---
feature: 065
slug: feature-number-badge-on-card
status: done
priority: p0
version: 1
created: 2026-09-24
---

# Feature 065 — Feature number badge on dashboard cards: номер фичи перед тайтлом бейджем-сниппетом (яркий текст+бордер, фирменный фиолетовый)

<goal>
On the dashboard feature card, the feature number moves from the small slug line under the title to a badge BEFORE the title: a snippet in the chip family (bright text + bright border, transparent fill), font slightly larger than the title, numbers only (letter suffixes like 003b stay part of the number). The slug line under the title loses the number and keeps the slug text.
</goal>

<requirements>
- Badge before the title on every dashboard feature card: number parsed by the existing feature-number.ts (tolerant of non-numbered slugs — no badge when there is no number).
- Styling in the chip family (priority/status/version): colored text + colored border, no solid fill; font-size a notch above the title.
- Number color: brand purple (recommendation — see decision note); constant across cards, NOT status- or priority-tinted.
- The slug line under the title shows the slug without the number prefix.
- Task-board card numbers (062) are NOT touched — different surface, already shipped.
</requirements>

<non-goals>
- Task-board cards (062 territory).
- Feature workspace/header pages — dashboard card only unless trivially shared.
- Renaming or re-slugging anything.
</non-goals>

<refs>
- User request 2026-09-24 (explicit, p0): number before the title as a bright chip, slightly larger than the title; color — priority/status/brand was an open question for the agent to reason about.
- web/src/features/planning/lib/feature-number.ts (parser), components/feature-number.tsx (existing component), feature-board.tsx (card), brand tokens (kerrigan palette, 018).
- 062-dashboard-visualisation-issues — the task-board sibling of this; 038/022 chip-family precedents.
</refs>

Design note on color (the user's open question): the number is an IDENTIFIER, not a state. Status and priority already have their own chips on the card — tinting the number by either duplicates an existing signal and makes the identifier's color unstable across the card's lifetime (and across the board: numbers would rainbow). A constant brand-purple badge reads as "this is the feature's number", stable forever. Recommendation: brand purple.

<log>
- 2026-09-24 [auto] — artifact created → inProgress
- 2026-09-24 [agent] — Closing by user mandate ("сделаем её сразу же", in-session): single task done, light review PASS-WITH-NITS with 0 code findings; the badge ships with the formatFeatureNumber extraction as the foundation for 066.
</log>
