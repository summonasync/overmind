---
feature: 047
task: 01
slug: api-read-surface-polish
status: done
review: light
created: 2026-10-06
brief: Mention taskCounts in list_features/task_queue descriptions; replace the upsert body-param phrase; add the holes-are-legal order sentence; update Canned.cs + contract pins.
---

# T2 — MCP tool descriptions + pass-through pins

<goal>
Callers learn the new field and the two residual doc-gaps close: list_features/task_queue descriptions mention taskCounts; the upsert body parameter stops implying a required format; order holes are documented as legal facts.
</goal>

<scope>
- src/Overmind.Mcp/OvermindPlanningTools.cs:
  - planning_list_features description (~line 97): mention taskCounts (todo/inProgress/review/done/blocked, sums to taskTotal) in the fields sentence.
  - planning_task_queue description (~line 501): insert taskCounts into the enumerated field list right after "taskDone, taskTotal".
  - planning_upsert_artifact body parameter (~line 246): replace "Markdown body of the artifact with its XML sections." with EXACTLY: "Markdown body. Expected XML sections per kind are listed in this tool's description; plain markdown is accepted — for spec/plan/result only the <log> section is machine-parsed."
  - planning_upsert_artifact description (~line 241) and planning_upsert_tasks description (~line 282): append to the order block EXACTLY: "Gaps in order numbering are legal and meaningful — imported backlogs skip tasks that were planned but never written; order doubles as the stable handle (blockedBy edges and {featureSlug}/T{order} aliases address tasks by order), so never renumber existing tasks to close a hole."
  - planning_upsert_artifact order parameter (~line 247): "Sort key and stable handle; gaps are legal — do not renumber to close them."
- tests/Overmind.Mcp.Tests/Canned.cs — board (~lines 122-123) and queue (~lines 236, 241, 246) canned JSON gain taskCounts values consistent with their taskTotal/taskDone (sum invariant).
- tests/Overmind.Mcp.Tests/PlanningToolContractTests.cs — pins: taskCounts mentioned in both read-tool descriptions; the new body-param pointer fragment present AND "with its XML sections" absent from the body param description; holes-are-legal fragment present in both upsert descriptions; pass-through assertions that list_features and task_queue output carries taskCounts from the canned responses.
</scope>

<anti-scope>
- Tool names, parameters and behavior untouched — description TEXT only.
- No Host changes here (T1 owns them).
- Do not loosen existing pins — update to the new text, never weaken the assertion.
</anti-scope>

<constraints>
- Texts above are verbatim contract strings — copy, do not rephrase.
- Canned counts must satisfy sum(taskCounts) == taskTotal.
- No comments in code; no commits.
</constraints>

<acceptance-criteria>
- [x] Both read-tool descriptions mention taskCounts; pinned.
- [x] Body param no longer contains "with its XML sections"; the pointer text is pinned.
- [x] Holes-are-legal sentence present in both upsert descriptions and the order parameter; pinned.
- [x] Canned board and queue JSON carry consistent taskCounts; pass-through assertions green.
- [x] dotnet test tests/Overmind.Mcp.Tests green.
</acceptance-criteria>

<refs>
- Spec 420cfc7d-0f9b-4883-a44d-130a911f8625 (items 2 and 4 — exact texts; item 1 field mention).
- Plan c505ad37-2f6c-41a6-8f01-20d5ad4ecf96 (T2, pin-drift and canned-consistency risks).
- Depends on Task 0 (T1) shape for the canned values.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan T2.
- 2026-10-06 [agent] — 047/T2 coding round started
- 2026-10-06 [agent] — Coding round complete — commit 3aca50c. All three verbatim texts placed (taskCounts mentions in list_features/task_queue, body-param pointer, holes sentence in both upsert descriptions). One judgment call: the order parameter keeps its existing "document kinds ignore it" caveat and appends the verbatim "Sort key and stable handle; gaps are legal — do not renumber to close them." — dropping the caveat would have regressed caller docs. Canned taskCounts: demo-feature {0,1,0,1,0} (sum 2 == taskTotal, done 1 == taskDone), draft-one all zeros; shape matches spec item 1 and the already-landed T3 web mirror. Gates: dotnet build 0 warnings/0 errors; tests/Overmind.Mcp.Tests serially 513/513 green. 5/5 AC ticked.
- 2026-10-06 [agent] — coding round complete — light review gate
- 2026-10-06 [agent] — light review APPROVED — 0 blockers, 1 cosmetic NOTE (colon vs parens in the two taskCounts mentions — both pinned as landed, no drift risk). Verbatims byte-checked, Canned shape cross-verified across all three lanes (Host record / Canned / web mirror 1:1), pins purely additive, order-tail retention endorsed, spot-check 171/171. Commit 3aca50c.
</log>