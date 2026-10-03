---
feature: 028
task: 02
slug: cross-project-bug-intake
status: done
review: light
created: 2026-10-02
depends_on: 01
brief: Agent+human doors over IssueService, contracts, foreign-create notification with deep-link, source markers
---

# Task 02 — Host API + foreign-create notification

<goal>
Both doors over IssueService (agent + human), contracts, and the notification-center entry on foreign create with a deep-link.
</goal>

<scope>
- HTTP endpoints per repo conventions: agent door (POST create/append/status/promote, GET list/get) + human door (full lifecycle for the owner team) — split across files the way AgentPlanningApi/PlanningFeaturesApi split.
- Contracts (issue rows, full card with append trail, echoes) — metadata-only list (no append texts in list rows).
- Foreign-create notification: when issues_create's authorProjectId differs from the board project, post a notification-center entry on the owner project with a deep-link to the issue — reuse the existing notification infrastructure (AgentNotification/020 pattern + notification-href deep-link format); the tool call stays thin (service does it).
- Source markers on lifecycle moves (agent/ui) per 043.
- Tests: both doors happy paths, provenance 404, status moves carry source, foreign create produces the notification with deep-link, list is metadata-only.
</scope>

<anti-scope>
No MCP tools (task 03), no web (task 04), no docs.
</anti-scope>

<constraints>
English-only, no comments by default, no auto-commits, dotnet build + full test with real numbers.
</constraints>

<acceptance-criteria>
- [x] dotnet build 0/0 + full dotnet test green
- [x] Agent + human doors complete over IssueService
- [x] Foreign create posts a notification-center entry with deep-link; own-project create does not
- [x] List endpoint is metadata-only; get returns the full card + append trail
- [x] Lifecycle moves record source markers
</acceptance-criteria>

<refs>
- Spec: 1fd1454c (MCP tool surface section describes the semantics both doors share; Onboarding protocol — notify).
- Task 01 (order 1) — IssueService this task exposes.
</refs>

<obstacles>
- DEEP-LINK SHAPE DECISION (task 04 must match): the notification carries Tab="issues" + FeatureSlug=<issueId> + ProjectId=<board project>; web task 04 must extend notification-href.ts so tab=="issues" routes to the Issues Board detail — conventional route chosen: /issues?project={projectId}&issue={issueId}. Backend issue-related changes for that shape: AgentNotification.AllowedTabs gained "issues" (tab validation). Notification: Kind=Info, agent="issues", text = "Project '{authorName}' filed issue «{title}» on your board — it needs a fix in your code." (authorName resolved from the author project row).
- List query params: status and authorProjectId are accepted as STRINGS and parsed in-handler (PlanningFeaturesApi.TryParseEnum / Guid.TryParse) with 400 on garbage — minimal-API binding of nullable enum / nullable Guid from query returns body-less 400s on unparseable values (caught live: IssueStatus? and Guid? query params produced framework 400s), so the doors own their parsing and error text.
- Agent door get hides cross-board issues by id (issue.ProjectId != route projectId → 404) — issues_get by id cannot be used to probe other boards' issues.
- Service surface per repo layering: ListAsync returns domain entities, AppendCountsAsync returns a dictionary of per-issue append counts — the DTO assembly (IssueRow metadata rows with AppendCount, full IssueCard with trail) lives in the Host handlers (AgentIssuesApi.ToRow/ToCard), matching PlanningQueueApi's pattern.
- Create/append echoes: create → {id, status, title} (037 compact pattern); append → the new IssueAppendItem; status → {id, status, source, closedAt}; promote → {id, status, artifactId, featureId, artifactVersion}.
</obstacles>

<log>
- 2026-10-03 [agent] — dispatched to dotnet-coder (same session — owns IssueService from task 01), task 01 done
- 2026-10-03 [agent] — Executed by dotnet-coder. New files: AgentIssuesApi.cs (agent door: POST create/append/status/promote, GET list/get under /api/agent/projects/{projectId}/issues), IssuesApi.cs (human door: same six over /api/projects/{projectId}/issues, all lifecycle moves Ui-sourced), IssueContracts.cs (CreateIssueRequest/AppendIssueRequest/SetIssueStatusRequest/PromoteIssueRequest + IssueRow metadata row with AppendCount + IssueCard with trail + echoes). Service: ListAsync/AppendCountsAsync/GetAsync added, CreateAsync now posts the foreign-create notification via NotificationsService (Kind=Info, Tab="issues", FeatureSlug=issueId, agent="issues", text names the author project) — own-project create posts nothing; AgentNotification.AllowedTabs gained "issues". Agent door passes source=Agent, human door source=Ui. DEEP-LINK SHAPE for task 04: notification Tab="issues", FeatureSlug=issueId, ProjectId=board project → web /issues?project={projectId}&issue={issueId} (recorded in obstacles). Program.cs maps both groups. Tests: IssueDoorApiTests 8 (agent foreign-create notification with link + own-create none, agent full lifecycle source=Agent, human full lifecycle source=Ui incl. closed ClosedAt, provenance 404 through both doors, list metadata-only with field-list pin + appendCount + no body/trail text + status filter, get card with trail). Gates: dotnet build --no-incremental 0 errors / 0 warnings; full suite 1833 passed / 0 failed (Core 274, Infra 565, Api 579, Mcp 419).
- 2026-10-03 [agent] — task 02 executed to review by dotnet-coder: both doors over IssueService (agent Agent-sourced, human Ui-sourced), metadata-only list rows + full card with trail, foreign-create notification with Issues-Board deep-link (Tab=issues, FeatureSlug=issueId — web route shape recorded in obstacles for task 04), own-create posts nothing. Gates: dotnet build --no-incremental 0 errors / 0 warnings; full suite 1833 passed / 0 failed (Core 274, Infra 565, Api 579, Mcp 419).
- 2026-10-03 [agent] — 2026-10-03 [agent] — reviewed by dotnet-code-reviewer: FAIL — 1 BLOCKER (both list doors seed parse defaults into nullable filters — unfiltered/status-only list always empty; hidden by a test that never reads the unfiltered response), 3 nits (cross-board guard asymmetry on append/status/promote, interim href /dashboard?feature={issueGuid} until task 04 lands, ListAsync tie-break); doors/notification/counts otherwise clean, build 0/0, door tests 8/8; report audits/review-028-02.md. Fix loop dispatched.
- 2026-10-03 [agent] — Fix round for review 028/02: BLOCKER — ListAsync parse defaults leaked into the nullable filters (parsedStatus=default→Open, authorFilter=Guid.Empty were passed as values, so every unfiltered list call filtered Status==Open AND AuthorProjectId==Guid.Empty and matched nothing; the old test only read the author-filtered response and hid it). Both doors now keep parsedStatus/authorFilter as true nullables — assigned only on successful parse; List_IsMetadataOnly strengthened to assert no-query and status-only responses are non-empty with correct rows (the two responses the old test never read). NIT — append/status/promote now cross-check the issue belongs to the route's projectId via shared EnsureIssueOnBoardAsync (404, same oracle as Get); cross-board pinning test AgentDoor_CrossBoardIssueId_Returns404 covers append/status/promote/get. Gates: dotnet build --no-incremental 0 errors / 0 warnings; full suite 1833 passed / 0 failed (Core 274, Infra 565, Api 579, Mcp 419).
- 2026-10-03 [agent] — 2026-10-03 [agent] — re-reviewed fix round 1 (028/02) by dotnet-code-reviewer: PASS — nullable list filters in both doors with regression asserts that red on old code, cross-board guard on all four issue-id ops; build 0/0, door tests 9/9; report audits/review-028-02.md.
- 2026-10-03 [agent] — delta review PASS (audits/review-028-02.md, fix round 1): nullable filters + regression asserts + cross-board guard; AC 5/5; 1833/1833. Closing as done.
</log>