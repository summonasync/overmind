---
feature: 028
task: 03
slug: cross-project-bug-intake
status: done
review: light
created: 2026-10-02
depends_on: 02
brief: Six issues_* tools in new IssuesTools.cs (god file untouched), protocol-first descriptions, contract tests
---

# Task 03 — MCP tools: IssuesTools.cs (parallel with 04)

<goal>
The agent-facing surface: six issues_* tools in a NEW file — the god file does not grow.
</goal>

<scope>
- src/Overmind.Mcp/IssuesTools.cs (new class): issues_create(projectId, authorProjectId, title, body), issues_list(projectId, status?, authorProjectId?), issues_get(issueId), issues_append(issueId, authorProjectId, text), issues_set_status(issueId, status, source, logNote), issues_promote(issueId, featureSlug, source).
- Descriptions carry the foreign-project protocol (create on THEIR board, append follow-ups, owner team triages/promotes) + body-template recommendation (Repro/Expected/Actual) + compact echo shapes per the 037 pattern.
- Registration alongside the existing tool classes.
- Contract tests: tool list contains all six, description asserts (protocol present, template recommendation present), echo shapes, happy-path roundtrip against the test host.
</scope>

<anti-scope>
No web (task 04), no docs beyond descriptions, no backend changes beyond registration wiring.
</anti-scope>

<constraints>
English-only, no comments by default, no auto-commits; OvermindPlanningTools.cs must NOT gain issues code — the new file only.
</constraints>

<acceptance-criteria>
- [x] dotnet build 0/0 + Mcp test project green
- [x] Six tools registered in IssuesTools.cs; OvermindPlanningTools.cs untouched
- [x] Descriptions: protocol wording, template recommendation, echo shapes
- [x] Contract tests: tool list + descriptions + happy-path roundtrip
</acceptance-criteria>

<refs>
- Spec: 1fd1454c (MCP tool surface + Onboarding protocol).
- Task 02 (order 2) — HTTP endpoints the tools call.
</refs>

<obstacles>
- Self-contained file: Guarded/EchoResult helpers duplicated from OvermindPlanningTools rather than promoted to a shared class — keeps the god file untouched per the constraint (15 lines of duplication, the file is the unit of self-containment the task asks for). Architect 028/03: Guarded/EchoResult extraction is now 8 copies across the adapter — microbatch candidate, accepted here.
- Echo shapes: create → {id, status, title}; append → {id, authorProjectId, text, createdAt}; set_status → {id, status, source, closedAt}; promote → {id, status, artifactId, featureId, artifactVersion}; list → rows array passthrough; get → full card passthrough. HTTP responses from task 02 match all of these.
- issues_set_status logNote: the spec signature includes logNote but the Issue domain has no log — the tool accepts it for protocol symmetry, does NOT forward it, and the description says so («issues have no log, the source marker is the provenance»). Recorded here so nobody "fixes" it by inventing an issue log.
- source normalization: agent/ui accepted case-insensitively, normalized to Agent/Ui for the HTTP enum binding; anything else → client-side McpException listing allowed values (no HTTP call). Blank status/source → named-field McpException («status is required...» / «source is required...») instead of «Invalid source ''» (review 028/03 nit).
- Blank-field pre-validation per review 028/01: title/body/text blank → client-side McpException naming the field, zero HTTP calls (pinned by tests asserting the stub saw no matching request). No length caps exist in the Issue domain, so no cap checks here.
- Minimal-API gotcha documented: nullable enum / nullable Guid query params return body-less framework 400s on binding failure — the HTTP List (task 02) already takes strings and parses in-handler; the MCP list tool passes the raw strings through so the server owns the error text.
- REVIEW 028/03 BLOCKER FIX (touches task 02's surface, noted for closeout): the four id-keyed tools originally called /api/agent/planning/issues/{id}[...] — a namespace invented by the coder that exists nowhere in the host (task 02 shipped only project-scoped /api/agent/projects/{projectId}/issues[...]); contract tests stubbed the phantom paths and pinned tool↔stub fiction (16/16 green while tool↔host was dead — same failure mode as 099/02's priority). Fixed per reviewer recommendation: AgentIssuesApi gained the projectId-less agent namespace /api/agent/issues (GET /{id}, POST /{id}/append|status|promote) as thin ById handlers over the same IssueService id-keyed methods — no cross-board guard on these (no route projectId; cross-board append is allowed by the provenance model; GetById wraps PlanningErrors for 404 — the first cut leaked a naked PlanningException through TestServer). MCP tools re-pointed; stub routes and path asserts re-pinned to the real host paths; host-side integration test ProjectIdLessAgentRoutes_FullLifecycle_Work exercises the four real routes end-to-end (append/status/card/promote + unknown-id 404). Lesson recorded: contract tests must pin tool↔HOST linkage — a stub for a route the host never registered proves nothing; verify the route exists in the host's route table before pinning it.
- issues_list: whitespace-only status filter counts as absent (Trimmed returns null, filter skipped) instead of a 400 naming an empty status (review 028/03 nit).
</obstacles>

<log>
- 2026-10-03 [agent] — orchestrator brief input from review 028/01: pre-validate identity/blank fields in the MCP layer BEFORE the HTTP call — entity guards throw ArgumentException which surfaces as 500; issues_create/append must map blank title/text or over-length fields into clean client-side errors (same pattern as the 099 identity cap). Also: CreateAsync currently hardcodes StatusSource=Agent — task 02 owns the source parameter on the doors; the tool layer should pass it through, not assume.
- 2026-10-03 [agent] — dispatched to dotnet-coder (same session), task 02 done; brief carries the pre-validation requirement from review 028/01
- 2026-10-03 [agent] — Executed by dotnet-coder. New src/Overmind.Mcp/IssuesTools.cs: six issues_* tools (create/list/get/append/set_status/promote) over the task-02 HTTP doors, [McpServerToolType] class registered in Program.cs (.WithTools<IssuesTools> + AddSingleton). Descriptions carry the foreign-project protocol, the Repro/Expected/Actual body-template recommendation, 409/404 recovery pointers and echo shapes. Pre-validation client-side per review 028/01: blank title/body/text → McpException naming the field (zero HTTP), status/source strings parsed+normalized client-side (invalid → McpException listing allowed values, zero HTTP), set_status logNote accepted for protocol symmetry but intentionally not forwarded (issues have no log — documented in description + obstacles). issues_list passes raw status/authorProjectId strings through so the server owns the 400 error text. Echo shapes: create {id,status,title}, append {id,authorProjectId,text,createdAt}, set_status {id,status,source,closedAt}, promote {id,status,artifactId,featureId,artifactVersion}; OvermindPlanningTools.cs untouched. Tests: new IssuesToolsContractTests 16 (tool-list six present, description protocol/template asserts, create echo+blank-field-no-HTTP theory, list filter passthrough + metadata-only, get card+trail, append echo+blank refusal, set_status posting+invalid status/source refusals, promote echo+gated-transition server detail + blank slug refusal). Handshake exact tool list + count test updated (34→40, six issues_* inserted alphabetically). Gates: dotnet build --no-incremental 0 errors / 0 warnings; sequential full run 1859 passed / 0 failed (Core 274, Infra 565, Api 580, Mcp 435); format clean outside Migrations.
- 2026-10-03 [agent] — task 03 executed to review by dotnet-coder: six issues_* tools in new IssuesTools.cs (protocol-first descriptions, 037 compact echoes, client-side blank/status/source pre-validation per review 028/01, logNote accepted-not-forwarded documented), registered in Program.cs, OvermindPlanningTools untouched; IssuesToolsContractTests 16 + handshake/tool-list updates. Gates: dotnet build --no-incremental 0 errors / 0 warnings; sequential full run 1859 passed / 0 failed (Core 274, Infra 565, Api 580, Mcp 435); format clean outside Migrations.
- 2026-10-03 [agent] — 2026-10-03 [agent] — reviewed by dotnet-code-reviewer: FAIL — 1 BLOCKER (issues_get/append/set_status/promote call /api/agent/planning/issues/* which exists nowhere in the host; stub pins the phantom paths so 16/16 green; root cause: spec id-tools are projectId-less vs projectId-scoped 028/02 doors — add projectId-less host routes), 2 nits (Guarded copy already diverged — extract to microbatch; whitespace status filter); build 0/0, contract tests 16/16, Mcp 435/435; report audits/review-028-03.md. Fix loop dispatched.
- 2026-10-03 [agent] — Fix round for review 028/03 (BLOCKER: four tools called the phantom /api/agent/planning/issues namespace that exists nowhere in the host — contract tests pinned tool↔stub fiction). HOST (touches task 02's surface, recorded for closeout): AgentIssuesApi gained the projectId-less agent namespace /api/agent/issues — GET /{id}, POST /{id}/append, POST /{id}/status, POST /{id}/promote — as thin GetById/AppendById/SetStatusById/PromoteById handlers over the same IssueService id-keyed methods (no cross-board guard: no route projectId, cross-board append is allowed by the provenance model; GetById wraps PlanningErrors for 404). TOOLS: issues_get/append/set_status/promote re-pointed from the phantom paths to /api/agent/issues/{id}[...]. TESTS: stub + path asserts re-pinned to the real host paths (contract tests now pin tool↔HOST linkage); new host-side integration test ProjectIdLessAgentRoutes_FullLifecycle_Work exercises the four real routes end-to-end against the ApiFactory host (append/status/card/promote + unknown id 404) and caught a real getById missing-try/catch leak (PlanningException pierced TestServer as a naked 500) — fixed. NIT: NormalizeStatus/NormalizeSource blank inputs now throw named-field McpException («status is required...» / «source is required...») instead of «Invalid source ''»; issues_list whitespace-only status filter counts as absent (Trimmed returns null, filter skipped). Gates: dotnet build --no-incremental 0 errors / 0 warnings; sequential full run 1859 passed / 0 failed (Core 274, Infra 565, Api 581, Mcp 435); format clean outside Migrations.
- 2026-10-03 [agent] — 2026-10-03 [agent] — re-reviewed fix round 1 (028/03) by dotnet-code-reviewer: PASS — four id-tools re-pointed to real /api/agent/issues routes added in the host, host-side lifecycle integration test proves the linkage, all ById handlers wrapped, nit applied; build 0/0, 16/16 + 10/10 re-run; report audits/review-028-03.md.
- 2026-10-03 [agent] — delta review PASS (audits/review-028-03.md, fix round 1): phantom routes replaced with real projectId-less host namespace + live integration pin; AC 4/4; 1859/1859. Closing as done.
</log>