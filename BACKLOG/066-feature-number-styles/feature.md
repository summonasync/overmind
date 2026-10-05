---
feature: 066
slug: feature-number-styles
status: done
priority: p1
version: 1
created: 2026-09-24
---

# Feature 066 — Feature-number styles per project (KEY-039 / #039 / plain) + вкладка project settings — один форматер для всех поверхностей

<goal>
Each project can choose how its feature numbers render: `KEY-039` (Jira-style with a project key), `#039` (VS Online / Azure DevOps), or plain `039` — default keeps today's form. The choice lives in a new PROJECT SETTINGS tab in the web UI (the first per-project settings surface; the storage pattern already exists — per-project overrides like RequireHumanValidation), and every number surface eats one shared formatter: the dashboard badge (065), the task-board numbers (062), notification rows.
</goal>

<requirements>
- Per-project setting: number style (key-hash | hash | plain) + optional project key (for key-hash; defaults derived from the project name where sensible).
- Project settings tab in the web UI — home for this and future per-project settings (validation gate override and materialization strategy are natural next residents, NOT in this feature's scope).
- One shared formatter in web/src/features/planning/lib (extends the 065 extraction); 065 badge, 062 task-board numbers and notification rows consume it.
- Setting rides the existing per-project override storage (the RequireHumanValidation pattern); API + round-trip.
</requirements>

<non-goals>
- Migrating existing per-project overrides into the new tab (each setting moves when touched).
- Cross-project number namespaces or global renumbering.
- Backend changes beyond the override storage/retrieval seam.
</non-goals>

<refs>
- User idea 2026-09-24 ("завести стиль оформления номеров фич для каждого проекта — вкладка project settings").
- 065-feature-number-badge-on-card (the formatter extraction + the badge); 062 (task-board numbers); 038/022 (chip surfaces).
- Per-project override storage pattern: PlanningPolicy.ResolveValidationGateAsync (Project.RequireHumanValidation).
</refs>

<log>
- 2026-10-04 [auto] — artifact created → inProgress
- 2026-10-05 [agent] — Van by claude claimed the feature
- 2026-10-05 [auto] — all tasks done + spec + plan validated → review
- 2026-10-05 [agent] — done via agent review — approved by human tap (notification reply 17:19:56 on result 56a746ab)
- 2026-10-05 [agent] — claim released on done-close
</log>
