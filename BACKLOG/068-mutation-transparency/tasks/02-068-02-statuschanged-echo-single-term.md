---
feature: 068
task: 02
slug: mutation-transparency
status: done
review: light
created: 2026-09-26
brief: Patch/upsert echo carries status + statusChanged on demotion; term unified to "validated" ("frozen" swept, grep gate)
---

# 068/02 — statusChanged echo + single term

<goal>
The agent sees what its mutation did (spec R2 / OM-12a + D2): demotion is echoed in the response, recorded by an [auto] line, and every machine surface says "validated" — never "frozen".
</goal>

<scope>
- Response: patch and upsert echoes carry status and statusChanged ("validated → draft") when the mutation demoted a validated artifact; compact-echo shape from 037/039 extended, not replaced; Canned updated.
- [auto] log line records the demotion (exact wording: "artifact demoted: validated → draft").
- Term sweep: 409 ChangeNoteRequired detail, MCP tool descriptions, SessionBootstrapPrompt/quickstart lines — "frozen" replaced by validated-wording everywhere machine-facing.
- Grep-gate test: fails if "frozen" reappears in machine-facing strings (tool descriptions + known error texts).
</scope>

<anti-scope>
- Demotion semantics unchanged (validated→draft on edit stays as-is); no feature-level transitions here (task 03).
</anti-scope>

<constraints>
- .NET 10 / C# 14; no comments; English-only; no git commit/push; dotnet build + dotnet test green.
</constraints>

<acceptance-criteria>
- [x] Patching a validated spec via MCP → response shows statusChanged "validated → draft" (test pin)
- [x] The <log> gains the [auto] demotion line
- [x] 409 ChangeNoteRequired text contains "validated", no "frozen"
- [x] Grep-gate test exists and passes
- [x] dotnet build + test green
</acceptance-criteria>

<refs>
- Spec 9f1fa31e R2, D2; 078 409 wording; 037/039 echo shape; task 01 lands first (same service file).
</refs>

<obstacles>
- frozenAt response field name and entity members (IsFrozen, RequireChangeNoteWhenFrozen) kept: the task enumerates error texts, tool descriptions and prompt/quickstart lines; renaming the frozenAt field is a response-shape break across Canned/UI and stays out of this scope (flag for D2 follow-up if the user wants the field renamed).
- SetStatus-based unfreeze (validated → another status) writes the user's logNote line but no [auto] demotion line — R2 covers patch/upsert mutations only.
- statusChanged echoes only when the response's artifact instance was demoted by THIS mutation ([NotMapped], null on reload) — reads and stale payloads never carry a stale statusChanged.
- UpsertTasks batch items pick up statusChanged through the shared ToArtifactResponse mapper; the MCP batch echo (CompactTaskBatch) stays compact and does not surface it (out of the enumerated echo surfaces).
</obstacles>

<log />

<log>
- 2026-09-26 [agent] — execution started: statusChanged echo on demotion + [auto] demotion line + "frozen" term sweep with grep gate
- 2026-09-26 — 068/02 implemented. Echo: PlanningArtifact gained [NotMapped] StatusChanged + consts DemotedStatusEcho ("validated → draft") / DemotionLogNote ("artifact demoted: validated → draft"); ApplyBodyMutation sets the echo and appends the [auto] demotion line when a validated artifact is mutated (after the user logNote line); ToArtifactResponse and PlanningArtifactPatchResponse carry optional statusChanged (JsonIgnore WhenWritingNull — shape unchanged when absent, batch upsert items get it for free via the shared mapper); MCP CompactArtifact copies statusChanged into the upsert echo and planning_patch_artifact text appends ", statusChanged 'validated → draft'" when present. Term sweep: ChangeNoteRequired → "A logNote is required to modify a validated artifact."; MigratedNoteRequired example → 'validated v9 on disk...'; entity guards → "Cannot update/move a validated artifact..."; planning_validate + asMigrated/logNote descriptions → "already validated on disk". SessionBootstrapPrompt and OnboardQuickstart contained no "frozen" (verified, now gated). Grep-gate: StatusModelContractTests.MachineSurfaces_NeverSayFrozen covers all tool descriptions + session_bootstrap prompt + OnboardQuickstart.Text + ChangeNoteRequired/MigratedNoteRequired texts. Pins: Core demotion echo + [auto] line + null-case; Api agent-door upsert/patch demotion pins (statusChanged + [auto] line); Mcp patch text + upsert echo via new Canned.PlanningArtifactDemotedJson; pre-existing FrozenEdit_WithLogNote_ResetsToDraft updated to expect the demotion line (Log.Length 1 → 2). Evidence: dotnet build 0 warnings / 0 errors; dotnet test 1458 passed / 0 failed (Core 215, Infrastructure 473, Api 422, Mcp 348); format clean on touched files.
- 2026-09-26 [agent] — 068/02 done: all 5 AC ticked. statusChanged echo + [auto] "artifact demoted: validated → draft" line on patch/upsert demotion; "frozen" swept from 409 ChangeNoteRequired, MigratedNoteRequired, entity guards, planning_validate/asMigrated/logNote descriptions; grep-gate MachineSurfaces_NeverSayFrozen (tool descriptions + bootstrap prompt + quickstart + known error texts). Evidence: dotnet build 0 warnings / 0 errors; dotnet test 1458 passed / 0 failed (Core 215, Infrastructure 473, Api 422, Mcp 348); format clean on touched files. Review label: light.
- 2026-09-26 — Review correction (audits/review-068-02.md, N2): the executor report said "two stale-expectation test updates" — the diff contains exactly one (PlanningApiTests.cs:689 log-length expectation). Miscount in the report, no code impact. Fix-round W1+S1+N1 dispatched; verdict PASS-WITH-NITS, 0 blockers.
- 2026-09-26 — Fix-round for review PASS-WITH-NITS (audits/review-068-02.md): W1 — stale canned 409 fixture in PlanningUpsertTasks_ApiSurfacesPerItemIndexInDetail aligned with the live ChangeNoteRequired wording ("validated artifact"). S1 — MachineSurfaces_NeverSayFrozen now also trips both entity guards on a real PlanningArtifact (UpdateBody without note, SetStatus off validated without note) and pins their messages say "validated", never "frozen". N1 — FrozenEdit_WithLogNote_ResetsToDraft asserts statusChanged "validated → draft" in the PUT response body (PlanningApiTests.ArtifactJson gained an optional StatusChanged; the [NotMapped] echo survives the handler's ReloadAsync since only mapped properties are overwritten — pin guards that thin spot). Evidence: dotnet build 0 warnings / 0 errors; dotnet test 1458 passed / 0 failed (Core 215, Infrastructure 473, Api 422, Mcp 348); format clean on touched files.
</log>
