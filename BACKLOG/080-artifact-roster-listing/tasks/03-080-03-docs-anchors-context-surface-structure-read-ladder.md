---
feature: 080
task: 03
slug: artifact-roster-listing
status: done
review: light
created: 2026-09-26
brief: Docs: CONTEXT MCP surface twenty-three tools + roster mention; STRUCTURE read ladder roster rung
---

# 080/03 — docs anchors: CONTEXT surface + STRUCTURE read ladder

<goal>
The two house doc anchors learn about planning_list_artifacts: the MCP surface enumeration in CONTEXT.md and the read-cost ladder in STRUCTURE.md.
</goal>

<context>
Executor reads ONLY this file. CONTEXT.md:150 carries the MCP planning surface bullet ("twenty-two tools (...) and three markdown resources (...)"). STRUCTURE.md:50 carries the priced-writes + mirrored read-ladder bullet (ends: planning_read_feature → most expensive read, the whole feature with all artifacts).
</context>

<scope>
- CONTEXT.md:150 — "twenty-two tools" → "twenty-three tools"; insert `planning_list_artifacts` into the enumeration next to `planning_read_feature`, phrased as the metadata-only roster (cheapest: what a feature contains, before any body read).
- STRUCTURE.md:50 — extend the read-ladder sentence with the roster rung: `planning_list_artifacts` → cheapest read (metadata roster of a feature's artifacts, no bodies); keep the existing three rungs untouched.
- Nothing else: no README, no web docs, no FEATURELIST edits.
</scope>

<anti-scope>
- No code changes of any kind; no web/README; no rewrite of surrounding doc text beyond the two anchors.
</anti-scope>

<constraints>
- English; keep the existing bullet style (compact, em-dash rungs); NO git commits.
- `dotnet build Overmind.sln` still green (docs-only change, but run it as the gate).
</constraints>

<acceptance-criteria>
- [x] CONTEXT.md says twenty-three tools and enumerates planning_list_artifacts.
- [x] STRUCTURE.md read ladder includes the planning_list_artifacts rung as the cheapest read.
- [x] grep "twenty-two" CONTEXT.md → no planning-surface hit left; diff touches only CONTEXT.md + STRUCTURE.md.
- [x] dotnet build green.
</acceptance-criteria>

<refs>
- CONTEXT.md:150 (MCP planning surface bullet)
- STRUCTURE.md:50 (priced writes + read ladder)
- Tool description pin (task 02) — wording source for the roster rung
</refs>

<obstacles>
- Wording collision inherited from the mandated rung text: the STRUCTURE ladder now labels both `planning_list_artifacts` and `planning_read_artifact` with `sections` as "cheapest read". The task scope pinned the new rung verbatim and forbade touching the existing three rungs, so both stay; the roster rung (metadata, no bodies) is the genuinely cheaper one. Flagged for the reviewer.
- Working tree also carries unrelated in-flight edits from tasks 01/02 (src/Overmind.Host, BACKLOG mirror) — this task's delta is CONTEXT.md + STRUCTURE.md only, verified via `git diff --stat` on those two files.
</obstacles>

<log>
- 2026-09-26 — Task created from validated plan 39417216; independent of tasks 01-02 file-wise, can run in parallel.
- 2026-09-26 [agent] — Both anchors done: CONTEXT.md:150 "twenty-two" → "twenty-three" and `planning_list_artifacts` inserted next to `planning_read_feature` as the metadata-only roster em-dash rung; STRUCTURE.md:50 read ladder gains the roster rung (`planning_list_artifacts` → cheapest read, no bodies), existing three rungs untouched. Grep gates: "twenty-two" 0 hits in CONTEXT.md, `planning_list_artifacts` present in both files; this task's diff = CONTEXT.md + STRUCTURE.md only. dotnet build Overmind.sln succeeded, 0 warnings / 0 errors. Rung wording pinned from task 02 tool description; one inherited wording collision logged in obstacles (double "cheapest read" in the ladder).
- 2026-09-26 [agent] — Task 080/03 executed in-place on main by dotnet-coder sub-agent: docs-only sweep of the two anchors (CONTEXT.md:150, STRUCTURE.md:50), all four AC ticked, grep pins and dotnet build verified green; moved to review, not done — reviewer picks it up.
- 2026-09-26 [agent] — orchestrator docs review (docs-only, C#-ревьюер не нужен): дифф ровно 2 строки в CONTEXT.md+STRUCTURE.md, twenty-two=0 хитов, planning_list_artifacts в обоих якорях, build 0W/0E; NIT принят — «cheapest read» теперь дважды в STRUCTURE-лестнице (roster=cheapest overall, sections=cheapest среди body-чтений), семантика не врёт; done
</log>