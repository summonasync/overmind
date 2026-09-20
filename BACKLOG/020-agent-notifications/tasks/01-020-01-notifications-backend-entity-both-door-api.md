---
feature: 020
task: 01
slug: agent-notifications
status: done
review: light
created: 2026-09-20
---

# 020/01 — Notifications backend (entity + both-door API)

<goal>
The AgentNotification entity + SQLite migration + NotificationsApi serving both doors: agents create and read replies; humans list (with unread count), mark read, and reply.
</goal>

<context>
Feature 020, layer 1 of 3. Tasks 02 (MCP) and 03 (web) consume this contract. Entity/migration template: FrontmatterKey (20260919164533). Both-doors pattern per STRUCTURE.md:33. Exploration facts: OvermindDbContext sets :14-29, DI UseSqlite :79, service-registration precedent :89, Map<> registration site Program.cs:139-155.
</context>

<scope>
- src/Overmind.Core/Entities/AgentNotification.cs — sealed entity per the spec (Id, ProjectId?, Kind enum approval/question/info in Core/Enums, FeatureSlug?, Tab?, Text, Agent, CreatedAt, ReadAt?, ReplyText?, RepliedAt?); Create with validation (kind valid, text required), Restore twin; MarkRead()/Reply(text) mutations setting timestamps.
- EF: DbSet + config (Text max length 2000, index ProjectId+CreatedAt) + migration AddAgentNotifications (FrontmatterKey template).
- src/Overmind.Host/Api/NotificationsApi.cs (static Map<NotificationsApi>): agent door POST /api/agent/notifications {kind, text, projectId?, featureSlug?, tab?, from?} (201 + created body); GET /api/agent/notifications/replies?projectId=&includeRead= (replied only, newest-first, RepliesResponse shape); human door GET /api/notifications (newest-first, cap 50, {items, unreadCount}); POST /api/notifications/{id}/read (idempotent); POST /api/notifications/{id}/reply {text} (one reply — a second reply overwrites, MVP). Unknown id → 404 HttpErrors.
- Contracts + FluentValidation (create: kind enum, text 1..2000, tab in spec/plan/tasks/result when present; reply: text 1..2000).
- A small NotificationsService in Infrastructure (scoped, DI) owning the queries/mutations — follow how the codebase splits Api/Service for planning.
- Tests in tests/Overmind.Api.Tests/NotificationApiTests.cs: create (agent door) → list (human door, unreadCount 1) → read (idempotent) → reply → replies (agent door) returns ReplyText/RepliedAt; includeRead filter; validation 400s (bad kind, empty text, 2001 chars, bad tab); 404s.
</scope>

<anti-scope>
- No MCP tools (02), no web (03), no retention/cleanup, no auth (loopback boundary), no realtime push.
</anti-scope>

<constraints>
- .NET 10 / C# 14; no new packages; no comments; English-only; static Map<ClassName> convention.
- dotnet build + test green (baseline 831; record counts).
</constraints>

<acceptance-criteria>
- [ ] Full lifecycle green through both doors (create-agent → list-human → read → reply → replies-agent).
- [ ] unreadCount accurate; read idempotent; second reply overwrites the first.
- [ ] Validation 400s and 404s pinned.
- [ ] dotnet test green incl. new tests.
</acceptance-criteria>

<refs>
- Spec: feature 020, spec artifact (validated 2026-09-20) — storage/routes/read-model decisions.
- Pattern refs: FrontmatterKey.cs + 20260919164533 migration, AgentPlanningApi.cs:12-27 (door routing), PlanningArtifactsApi (Api→Service split), HttpErrors, DependencyInjection.cs:79-89.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Execution started — dotnet-coder subagent.
- 2026-09-20 — Coder done: AgentNotification + NotificationKind enum + AddAgentNotifications migration (FK ProjectId→Projects, index ProjectId+CreatedAt), NotificationsService (5 methods, AsNoTracking reads), NotificationsApi both doors, contracts/validators/mapper, 15 tests (lifecycle both doors, idempotent read, reply overwrite, includeRead, cap, 400/404). Suite 831 → 846. Decisions: includeRead defaults TRUE on replies (default-false would hide a human-early-read reply from the agent forever — task 02 must mirror); Kind validated via IsEnumName (clean 400); unknown projectId → 404. To review.
- 2026-09-20 — Review PASS (audits/review-020-01.md): 0 blockers, 7 INFO + 1 process note (stage the 018 result-flip consciously with this feature's commit). Contract mirror-exact; ReplyText⇔RepliedAt invariant per the PlanningArtifact template; idempotent read pinned by timestamp compare; includeRead=true decision upheld (human click must not hide a reply from the agent forever — task 02 mirrors it); unknown-projectId 404 friendlier than FK-500; FK cascade = codebase posture. INFO → microbatch (Location header on missing route, tab check in Restore, two unpinned test cases). Done.
</log>
