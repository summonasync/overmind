---
feature: 081
task: 06
slug: task-dependencies
status: done
review: light
created: 2026-09-27
brief: Web: blocked chips on task cards + read-only dependencies panel (030 pattern)
---

# 081/06 — Web: blocked chips + dependencies panel

<goal>
The human sees the graph: blocked chips on task cards and a dependencies panel on the feature page (030 pattern) — full-stack per user decision.
</goal>

<context>
Executor reads ONLY this file. Prerequisite: task 03 (fields live on the API). Templates: 030 web surface — Dependencies panel in the feature workspace (web/src/features/planning/*) and blocked chips/badge patterns; task cards live in the feature workspace task board components. Data: roster/full-feature endpoints already carry blocked/blockedBy (orders) after task 03; titles resolvable from the same payload.
</context>

<scope>
- Task cards (feature workspace task board): a muted blocked chip when blocked=true (tooltip lists blocker orders/titles) — indication only, no click-gating of the human UI.
- Feature workspace: dependencies panel/section listing task edges (task order+title → blockedBy orders+titles), closed edges muted; read-only (edges are managed via upsert by agents; no human add/remove UI in this feature).
- English strings, RSC-first, client components only where interactivity demands; no new packages.
- Gates: npm run lint && npx tsc --noEmit; npm run build; manual check of panel + chips on a feature with a chain (dev server).
</scope>

<anti-scope>
- No backend changes; no board-wide blocked filter (follow-up if wanted); no human editor for edges.
</anti-scope>

<constraints>
- English UI; no new packages; NO git commits.
- web lint/tsc/build green.
- Diff: web/src only.
</constraints>

<acceptance-criteria>
- [x] Blocked chips appear on blocked task cards (and only on them) with blocker info on hover.
- [x] Dependencies panel lists task edges with closed-state muting; read-only.
- [x] npm lint + tsc + build green; manual check documented in task log.
</acceptance-criteria>

<refs>
- 030 web surface: Dependencies panel + chips/badge patterns (web/src/features/planning)
- Task board components (feature workspace)
- API fields from task 03 (blocked/blockedBy on task items)
</refs>

<obstacles>
<obstacles>
- 2026-09-28 [executor] Live dev-server check against the running Overmind instance is not possible until the 081 wave deploys: the production DB (~/.overmind/overmind.db) has no PlanningTaskDependencies table yet, so no feature carries task edges there. Manual check was done as a real react-dom/server smoke render of the actual components with a 3-task chain instead (documented in log).
- 2026-09-28 [executor] planning_patch_artifact acText tick failed silently twice on the AC containing "muting; read-only." — ticked via acIndex instead.
</obstacles>
</obstacles>

<log>
- 2026-09-27 — created from validated plan ca4721f1; dispatch after task 03.
- 2026-09-27 [agent] — executor picked up task 06 (web: blocked chips + dependencies panel); starting web exploration
- 2026-09-27 [agent] — chip on task cards: muted chip with hover tooltip "Blocked by #order title, ..." — smoke-verified single chip on the blocked card only
- 2026-09-27 [agent] — TaskDependenciesPanel mounted under FeatureDependenciesPanel: task order+title to blocker order+title chips, closed blockers muted with strikethrough, open in danger tone; read-only, no add/remove UI; hidden when the feature has no edges
- 2026-09-27 [agent] — web gates green: npm run lint (0 errors, 6 pre-existing warnings outside the diff), tsc --noEmit clean, next build success; manual check via throwaway react-dom/server smoke render of TaskBoard + TaskDependenciesPanel with a 3-task chain (open+closed blockers): panel heading, 1 open badge, closed edge strikethrough, exactly one blocked chip, tooltip lists both blockers; live dev-server check pending wave deploy — running instance DB predates the tasks-dependencies migration, so no chain exists there yet
- 2026-09-27 [agent] — executor done, moving to review: web-only diff (web/src) — blocked chips on task cards, read-only TaskDependenciesPanel on feature workspace, blocked/blockedBy added to PlanningArtifactResponse type; lint/tsc/build green; all 3 AC ticked
- 2026-09-27 [agent] — light review (next-js-code-reviewer) PASS-WITH-NITS (3 SUGGESTION/NIT) — audits/review-081-06.md; независимые гейты: lint 0 errors/6 pre-existing warnings, tsc clean, build success; closed-семантика web == бэк; пост-деплой живой чек зафиксирован обязательным follow-up (таблицы в проде нет до деплоя волны); done by orchestrator after review
</log>