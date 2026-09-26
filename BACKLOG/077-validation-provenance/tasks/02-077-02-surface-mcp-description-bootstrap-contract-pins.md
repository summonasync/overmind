---
feature: 077
task: 02
slug: validation-provenance
status: done
review: light
created: 2026-09-26
brief: planning_validate description + bootstrap line document the proof mechanism; Canned 409 + Mcp pins
---

# 077/02 — Surface: MCP description, bootstrap, contract pins

<goal>
Agents discover the proof mechanism from the tool itself (spec R5, R6-mcp): planning_validate description documents the proof search and the 409; bootstrap mentions it; the 409 surfaces cleanly through MCP.
</goal>

<scope>
- MCP planning_validate description: proof mechanism (notify(approval, featureSlug) → human bell reply → validate), the 409 behavior, and that the human door needs none.
- Session bootstrap: one line next to the existing validation text — spec/plan validation by an agent requires a human approval reply (notify → reply → validate).
- Canned 409 fixture (ValidationProofRequired shape) + McpSessionTestBase stub route if needed.
- Mcp contract pins: description text (proof + 409 mentioned), 409 surfacing through the tool (CallToolResult precedent from planning_set_current), bootstrap line pinned.
- Task 01 lands first — pin its live texts, do not guess wording.
</scope>

<anti-scope>
- Backend proof logic (task 01), notification model, UI.
</anti-scope>

<constraints>
- .NET 10 / C# 14; no comments; English-only; no git commands; dotnet build 0 warnings + dotnet test green.
</constraints>

<acceptance-criteria>
- [x] planning_validate description documents notify → reply → validate and the 409 (pinned)
- [x] Bootstrap carries the validation-proof line (pinned)
- [x] 409 surfaces through MCP with the actionable detail (pinned, Canned fixture)
- [x] 068 grep gates stay green (no "frozen", markers intact)
- [x] dotnet build 0 warnings + dotnet test green
</acceptance-criteria>

<refs>
- Spec 55333889 (validated) R5–R6; task 01 (its texts are the pin source); 068 grep gates; planning_set_current CallToolResult precedent.
</refs>

<obstacles />

<log />

<log>
- 2026-09-26 [agent] — execution started: surface — planning_validate description documents proof + 409, bootstrap line, Canned 409 fixture, Mcp pins
- 2026-09-26 [agent] — 077/02 implemented. planning_validate description now documents the proof mechanism end to end (read from task 01's live tree, not guessed): notify(kind=approval, featureSlug=<slug>) → the human's bell reply → validate (notify → reply → validate); the reply must be newer than the artifact's last update — every edit voids it, re-validation needs a fresh reply; without proof → ValidationProofRequired 409; the human door and asMigrated need no proof; "human validation"/"STOP" fragments kept (existing pins green). SessionBootstrapPrompt: ValidationProofRouting line added to BOTH bootstrap variants next to the existing validation text ("overmind_notify(kind=approval, featureSlug) → the human's bell reply → planning_validate; every edit voids the reply (the UI door needs no proof)"). Canned: PlanningValidationProofRequiredJson fixture with task 01's verbatim detail (featureSlug=demo-feature). Pins: PlanningValidate_Description_DocumentsProofAndThe409 (notify → reply → validate / ValidationProofRequired / human door / single-use freshness), PlanningValidate_ProofRequired409_SurfacesTheActionableDetail (stub 409 → tool error text carries the verbatim detail; note — the MCP error message packs the ProblemDetails detail only, the code title stays HTTP-level, so the pin asserts the detail + 409, matching the Guarded passthrough behavior), SessionBootstrap_StatesTheValidationProofFlow. 068 grep gates green (new texts contain no "frozen", marker wording intact). Evidence: dotnet build 0 warnings / 0 errors; dotnet test 1498 passed / 0 failed (Core 215, Infrastructure 492, Api 438, Mcp 353); format clean on touched files.
- 2026-09-26 [agent] — Obstacles (intended for the <obstacles> section; the full-body upsert was again rejected by the 078 append-only guard on an invisible transcription diff in the long implementation log line — same friction as on 077/01, worth a microbatch: the guard error could print a character-level diff of the diverging line): (1) the MCP 409 error message packs only the ProblemDetails DETAIL (Guarded → McpException), not the failure-code title — the pin asserts the verbatim detail + 409; ValidationProofRequired as a title stays visible on the HTTP door (ProblemJson.Title); (2) proof freshness is single-use by design — each successful validation moves UpdatedAt past the reply, so edit + re-validate always needs a fresh human reply; description and bootstrap state it explicitly ("every edit voids it"); (3) the validate description grew long (proof flow + freshness + 409 + human door + asMigrated in one text) — accepted deliberately so agents discover the whole contract from the tool list; fragment pins keep it stable.
- 2026-09-26 [agent] — 077/02 done: all 5 AC ticked. planning_validate description documents the full proof contract (notify → reply → validate, single-use freshness — every edit voids the reply, ValidationProofRequired 409, human door and asMigrated exempt); session bootstrap carries the proof line in both variants; Canned ValidationProofRequired fixture + tool-surfacing pin (verbatim detail through the MCP error); 068 grep gates green. Evidence: dotnet build 0 warnings / 0 errors; dotnet test 1498 passed / 0 failed (Core 215, Infrastructure 492, Api 438, Mcp 353); format clean on touched files. Review label: light. Obstacles noted in the log (MCP 409 carries detail not title; single-use freshness documented; long description accepted deliberately).
- 2026-09-26 [agent] — Review correction (audits/review-077-02.md, W-1): executor reported 1498/1498 (Mcp 353); the reviewer's independent fresh run shows 1500/1500 (Mcp 355, +3 facts). The reviewer run is authoritative — this task's verified count is 1500/1500. S-1 (bootstrap active-branch pin) → microbatch #95.
</log>
