---
feature: 093
task: 01
slug: approval-deep-link-toast
status: done
review: light
created: 2026-09-28
brief: Host stamps absolute deep-link url on agent notification-create response (BrowserUri origin), Api pins
---

# 093/01 — Host: stamp deep-link url on notification create

<goal>
The agent-door create response carries a ready-to-paste absolute URL resolving to the approval/question's feature tab. POST /api/agent/notifications response gains `url` when the notification has featureSlug; no url field otherwise.
</goal>

<context>
Spec 3748e264 (validated), plan 98cb3e9a — task 1 of 3, parallel with 02 (disjoint files). The host knows its public browser origin via BrowserUri.FromLoopback (src/Overmind.Host/BrowserUri.cs) — Program.cs computes it twice already (lines ~47 and ~197, from the bound app URLs). The web builds its own relative href (web/src/widgets/app-shell/lib/notification-href.ts): /dashboard?project=&feature=&tab= — the host url must match that param format exactly (project only when projectId set, feature always when featureSlug set, tab only when set).
</context>

<scope>
- src/Overmind.Host/Api/Contracts/NotificationContracts.cs — the agent create response record gains url (nullable or omitted-when-null; follow the file's JSON conventions).
- src/Overmind.Host/Api/NotificationsApi.cs — agent create handler stamps url. Origin resolution: a small singleton computed once at startup from the bound URL via BrowserUri.FromLoopback; if app.Urls is empty (WebApplicationFactory/test host), derive from the current request (scheme + Host header) as fallback — document the chosen mechanism in <obstacles>. No origin heuristics beyond BrowserUri.
- Human-door list/read/reply responses stay untouched (web builds its own href); do NOT add url there.
- tests/Overmind.Api.Tests/NotificationApiTests.cs — pins: create with featureSlug+tab+projectId → url absolute, /dashboard path, all three params; create with featureSlug and no projectId → url without project param; create without featureSlug → no url (or null, per contract choice).
- Verify: dotnet build + dotnet test (Overmind.Api.Tests green).
</scope>

<anti-scope>
- MCP adapter (task 02), web (task 03), list/read/reply contracts, data model, notifications service logic.
</anti-scope>

<constraints>
- Static conventions of src/Overmind.Host/Api (static Map<>, method-group handlers, HttpErrors.*). No comments. English-only. No new packages. Match surrounding style.
</constraints>

<acceptance-criteria>
- [x] Agent create response includes url for featureSlug'd notifications (absolute, /dashboard?project=&feature=&tab= shape matching notification-href.ts).
- [x] Create without featureSlug returns no url.
- [x] Human-door responses unchanged (no url on list/read/reply).
- [x] Api.Tests pins added; dotnet build + dotnet test green.
- [x] Work logged via planning_append_log; obstacles patched if origin-resolution needed a fallback.
</acceptance-criteria>

<refs>
- Spec 3748e264 (validated); plan 98cb3e9a.
- src/Overmind.Host/BrowserUri.cs — FromLoopback.
- web/src/widgets/app-shell/lib/notification-href.ts — param format source of truth.
</refs>

<obstacles>
- Origin resolution (documented per scope): singleton BrowserOrigin (src/Overmind.Host/BrowserOrigin.cs) bound once in Program.cs right after app.StartAsync from the first bound URL via BrowserUri.FromLoopback; per-request fallback in BrowserOrigin.Resolve derives scheme + Host header and passes it through FromLoopback too (one heuristic total, no others).
- Quirk vs task premise: under WebApplicationFactory app.Urls is NOT empty — the test host reports a placeholder `http://127.0.0.1:0`. The bind guard therefore skips URLs with port 0 (`new Uri(bound) is { Port: not 0 }`); port 0 is never a usable public origin. In tests the request fallback fires: Host `localhost` → FromLoopback → `http://overmind.localhost`, same convention as the tray openUri path.
- Contract choice: url is omitted-when-null on a new agent-door record AgentNotificationResponse (JsonPropertyIgnore WhenWritingNull) instead of being added to the shared NotificationResponse — human-door list/read/reply payloads stay byte-identical, and the goal's "no url field otherwise" is literal.
- Discarded alternative: deriving AgentNotificationResponse positionally from NotificationResponse — a probe build showed serialization through the base static type drops the derived Url property and the generated props shadow base ones.
- Link composition uses `new Uri(origin, "/dashboard?...")`: UriBuilder carried a spurious `:0` port into the output when the origin had no explicit port.
</obstacles>

<log>
- 2026-09-28 [agent] — task sliced from validated plan; parallel with 02.
- 2026-09-28 [agent] — coder picked up task 01
- 2026-09-28 [agent] — task 01 implemented: agent create response stamps absolute dashboard deep-link. New files: src/Overmind.Host/BrowserOrigin.cs (origin singleton: bound once after StartAsync from app.Urls via BrowserUri.FromLoopback, port-0 placeholder skipped, request scheme+Host fallback), src/Overmind.Host/Api/NotificationLinks.cs (/dashboard?project=&feature=&tab= builder matching web notification-href.ts param order; null when no featureSlug). Edited: NotificationContracts.cs (AgentNotificationResponse, url omitted when null), NotificationMapper.cs (ToAgentResponse), NotificationsApi.cs (Create takes BrowserOrigin + HttpContext and stamps url), DependencyInjection.cs (singleton registration), Program.cs (bind after StartAsync), NotificationApiTests.cs (4 pins: absolute url with project+feature+tab; no-project omits project param; no featureSlug → no url field in JSON; human-door list/read/reply carry no url). Evidence: dotnet build 0 errors / 0 warnings; NotificationApiTests 25/25; full Overmind.Api.Tests 496/496 green; dotnet format --verify-no-changes clean on both touched projects. src/Overmind.Mcp/** and tests/Overmind.Mcp.Tests/** untouched (task 02 owner works there). Origin-resolution mechanism documented in obstacles.
- 2026-09-28 [agent] — AC0 ticked: Create stamps absolute url — pin Create_WithFeatureSlugTabAndProject_ReturnsAbsoluteDashboardUrl asserts http://overmind.localhost/dashboard?project=&feature=&tab=
- 2026-09-28 [agent] — AC1 ticked: no featureSlug → no url field in create JSON (pin Create_WithoutFeatureSlug_OmitsUrlField; omission via JsonIgnore WhenWritingNull)
- 2026-09-28 [agent] — AC2 ticked: human-door list/read/reply payloads unchanged — pin HumanDoor_ListReadReply_DoNotCarryUrl checks raw JSON for the url property
- 2026-09-28 [agent] — AC3 ticked: 4 pins added to NotificationApiTests; dotnet build 0 err/0 warn, NotificationApiTests 25/25, full Overmind.Api.Tests 496/496, dotnet format verify clean
- 2026-09-28 [agent] — AC4 ticked: work logged; obstacles patched with the origin-resolution fallback and the port-0 WebApplicationFactory quirk
- 2026-09-28 [agent] — coder done: task 093/01 complete — host stamps deep-link url on agent create. All 5 ACs ticked with evidence; build 0/0, Overmind.Api.Tests 496/496; obstacles carry the origin-resolution mechanism (BrowserOrigin singleton + request fallback, port-0 placeholder guard). Ready for review.
- 2026-09-28 [agent] — light review PASS-WITH-NITS (audits/review-093-01.md): 1 SUGGESTION (agent-запись дублирует NotificationResponse — sync-burden, микробатч #130) + 3 NIT; reviewer перепроверил build 0/0, Api.Tests 496/496, format чист; AC 5/5
</log>