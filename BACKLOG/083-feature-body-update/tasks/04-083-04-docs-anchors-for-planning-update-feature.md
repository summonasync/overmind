---
feature: 083
task: 04
slug: feature-body-update
status: done
review: light
created: 2026-09-27
brief: Docs: CONTEXT twenty-four tools + update_feature rung; STRUCTURE feature-log guard clause
---

# 083/04 — docs anchors for planning_update_feature

<goal>
The two house doc anchors learn about planning_update_feature and the feature-log guard (#74 absorption).
</goal>

<context>
Executor reads ONLY this file. CONTEXT.md MCP surface bullet currently says "twenty-three tools" (updated by 080/03) — planning_update_feature joins the enumeration next to planning_list_artifacts. STRUCTURE.md carries the priced-writes bullet (039 D9) and the feature-body context — the guard clause lands there.
</context>

<scope>
- CONTEXT.md MCP surface bullet: "twenty-three tools" → "twenty-four tools"; insert `planning_update_feature` into the enumeration next to `planning_list_artifacts` with a compact em-dash rung: partial edit of a feature's title/body/priority with a mandatory logNote — durable what/why stays correctable by agents; status not editable here.
- STRUCTURE.md: in the writes bullet (or the nearest feature-body sentence), add one clause: feature bodies are editable but their `<log>` is append-only on both doors (guard on `PlanningFeature.Update`, 083 — closes the 078 gap for features).
- Nothing else: no README, no FEATURELIST, no code.
</scope>

<anti-scope>
- No code changes of any kind; no web; no rewrite of surrounding text beyond the two anchors.
</anti-scope>

<constraints>
- English; existing bullet style; NO git commits.
- `dotnet build Overmind.sln` still green (docs-only gate).
</constraints>

<acceptance-criteria>
- [x] CONTEXT.md says twenty-four tools and enumerates planning_update_feature with the logNote wording.
- [x] STRUCTURE.md carries the feature-body append-only guard clause (both doors, #74).
- [x] grep "twenty-three" CONTEXT.md → no planning-surface hit left; diff touches only CONTEXT.md + STRUCTURE.md.
- [x] dotnet build green.
</acceptance-criteria>

<refs>
- CONTEXT.md MCP surface bullet (twenty-three → twenty-four)
- STRUCTURE.md priced-writes / feature-body sentence
- Tool description verbatim (task 03 scope) — wording source
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-27 — Task created from validated plan 19f231e5; independent file-wise, can run parallel to task 03.
- 2026-09-27 [agent] — Docs anchors done: CONTEXT.md — twenty-four tools + planning_update_feature rung (logNote wording verbatim from task scope), placed next to the planning_list_artifacts rung; STRUCTURE.md — feature-log append-only clause on both doors added to the service-seam bullet, the nearest feature-body sentence (guard on PlanningFeature.Update, 083 — closes the 078 gap). grep pins clean, diff touches only the two files, dotnet build green.
- 2026-09-27 [agent] — todo → review: all 4 AC ticked — twenty-four tools + update_feature rung in CONTEXT.md, append-only log clause in STRUCTURE.md service-seam bullet, grep pins clean (no planning-surface twenty-three left), diff limited to the two anchors, dotnet build Overmind.sln green. Docs-only task, no quirks.
- 2026-09-27 [agent] — orchestrator docs review (docs-only): дифф ровно 2 строки в CONTEXT.md+STRUCTURE.md, twenty-three=0 хитов, planning_update_feature рунг вербатим, STRUCTURE клоз в service-seam буллете (осознанный выбор — точнее ложится, чем priced-writes), build 0W/0E; финальная сверка рунга с живым description тула — после таски 03; done
</log>