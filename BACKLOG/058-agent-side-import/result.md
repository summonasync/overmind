---
feature: 058
slug: agent-side-import
status: draft
created: 2026-09-24
---

# Result 058 — Import from materialized files unavailable to agents — migration reroutes every body through the model (OM-7)

<goal>
Delivery record for 058-agent-side-import: planning_import_feature shipped — agent-side import of a materialized feature folder without piping bodies through the model.
</goal>

<context>
OM-7 from the 2026-09-23 field run: the quickstart promised import but no MCP tool exposed it; migration cost ~17K chars of bodies through the main agent, ~70K tokens per sonnet sub-agent per task.
</context>

<scope>
Shipped: BacklogImporter.ImportFeatureAsync (single feature folder, reuses ParseFeatureFolderAsync + PlanningFeatureService.ImportAsync — statuses verbatim, [import] markers, 067 tracking registration); POST /api/agent/projects/{id}/planning/import with per-artifact created/updated report (notes, trackedFiles, nextStep); MCP tool planning_import_feature(projectId, path) as a thin passthrough + quickstart line. Path contract: ABSOLUTE paths only, reject semantics (validator Must(IsPathFullyQualified) at the boundary + MaterializationException.NotAbsolute in PathGuard as defense-in-depth — CWD-dependence impossible on both doors); outside-root → 400 ImportFailed.
</scope>

<acceptance-criteria>
- [x] One call imports a feature folder with spec/plan/tasks; statuses verbatim; [import] markers in logs (pinned infra+api tests; live MCP round-trip on isolated host: artifactsCreated:2, disk-edit re-import restored a task to done)
- [x] A path outside the project root is rejected (plus relative-path rejection pinned after review fix round)
- [x] dotnet build passes (0 warnings)
</acceptance-criteria>

<refs>
audits/review-058-01.md (light review: APPROVED WITH WARNINGS → fix round W1 reject-semantics + W2 validator convention → re-check APPROVED); task cb8c0df4 (done, log carries both fix rounds); open follow-ups in microbatch queue: #63 symlink canonicalization, #64 DiskPath="/", #65 ProjectValidators null-500, #66 null-body module pattern
</refs>

<obstacles>
bodyFromPath (optional add-on in the task sketch) deliberately skipped — would require a parallel file-reading path in the upsert contract; the OM-7 goal is closed by the import tool itself (logged in the task log). FluentValidation 12 runs Must after a failed NotEmpty (Continue) — null input needed a .When guard to avoid ArgumentNullException→500.
</obstacles>

<log>
- 2026-09-24 [agent] — result recorded after APPROVED re-check; bodyFromPath skip decision documented in obstacles
</log>