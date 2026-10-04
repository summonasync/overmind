---
feature: 113
slug: attention-row-polish
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Spec 113 — Attention rows: dashboard number style + clickable hover-highlighted title

> User dogfood feedback 2026-10-04 on the released attention page (087), approved in chat.

## Goal

Ceremony rows match the dashboard's visual language and open like the dashboard cards: the number carries the dashboard style, the title is the primary click target with hover feedback.

## Decisions

- **Number:** render through the dashboard's number treatment (FeatureNumberBadge / formatFeatureNumber style — the 065 brand-violet constant identifier), not the raw mono span. The attention payload carries `number` (slug-derived) — the badge component takes it the same way the board does.
- **Title is the link:** the title becomes an `<a href={feature.url}>` (same target as «Open →»); hover highlight per dashboard card title treatment (hover:text-primary or underline-offset pattern — match the board card link style). «Open →» stays (redundant but harmless affordance at row end), unless it collides visually — reviewer's call.
- Row keeps badges on the right; layout rhythm unchanged.

## Non-goals

- Awaiting rows (different surface, has its own link); server payload changes; grouping/order.

<log>
- 2026-10-04 [agent] — user dogfood feedback in session: dashboard-style number, hover-highlighted clickable title; payload unchanged.
- 2026-10-04 [agent] — [review caveat, spec body untouched] The scan's «no writes» means the BOARD: ParseFeatureAsync side effect — unknown frontmatter keys get registered in the DB (FrontmatterKeys) on first sight, same as the agent import door; recorded at review 071/01 (WARNING 1) and carried into the 071/03 result.
- 2026-10-04 [agent] — [CORRECTION] previous log line is misfiled — it belongs to the 071-backlog-import-ui spec, not this feature; appended there properly. Nothing about 113 changed by it.
- 2026-10-04 [agent] — validated with human proof: notification 14289b51-7b48-4145-a508-1faf2b1b2bb7 replied 2026-10-04 17:12:18 UTC
</log>