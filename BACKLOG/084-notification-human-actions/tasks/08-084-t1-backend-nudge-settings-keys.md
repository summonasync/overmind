---
feature: 084
task: 08
slug: notification-human-actions
status: done
review: light
created: 2026-09-28
---

# 084/T1 — Backend: nudge settings keys

<goal>
SUPERSEDED — accidental duplicate of 084/T1 (task 3395e5a8, order 3), created 2026-09-28 by a coder upsert that omitted `order` and appended a new task instead of updating the original. All real work, obstacles and acceptance evidence live on 3395e5a8. Safe to delete via UI.
</goal>

<scope>
- src/Overmind.Host/Api/Contracts/SettingsContracts.cs — extend the settings contract by the QdrantEndpoint precedent.
- src/Overmind.Host/Api/SettingsApi.cs — validation: mode must be quiet|nag|mini (case-insensitive, normalized), minutes int > 0; invalid → 400 ValidationProblem.
- tests/Overmind.Api.Tests/SettingsApiTests.cs — defaults when the store is empty (quiet/10), PUT/GET round-trip for all three modes, invalid mode and minutes rejected.
</scope>

<anti-scope>
- Web settings UI (T4); bubble consumer (T3); per-project settings — nudge is global.
</anti-scope>

<obstacles>
- 2026-09-28 [agent] — "non-int minutes → 400" is satisfied by ASP.NET Core body binding, not the ValidationProblem path: NotificationNudgeIntervalMinutes is typed int? in UpdateSettingsRequest, so a JSON type mismatch throws during deserialization and the framework returns a plain 400. Unknown mode and minutes ≤ 0 still go through InvalidSetting (ValidationProblem). KV keys follow the dotted precedent (notification.nudgeMode / notification.nudgeIntervalMinutes), not the camelCase JSON names.
</obstacles>

<acceptance-criteria>
- [ ] GET /api/settings returns quiet + 10 defaults on an empty store.
- [ ] PUT accepts all three modes; invalid mode / minutes ≤ 0 / non-int → 400.
- [ ] SettingsApiTests round-trip facts green (dotnet test).
- [ ] dotnet build 0 warnings / 0 errors.
</acceptance-criteria>

<refs>
- Plan 084 (validated 1b7c0c3b), spec 084 (validated eb34ae7a).
- QdrantEndpoint settings keys — the extension precedent.
</refs>

<log>
- 2026-09-28 [agent] — [agent] taken into work — T1 backend settings keys, coder dispatched (sonnet)
- 2026-09-28 [agent] — [agent] closing as tombstone — accidental duplicate of task 3395e5a8 (order 3) created by a coder upsert that omitted `order`; zero code behind it, all T1 work and AC evidence tracked on 3395e5a8; orchestrator may delete this artifact via UI
</log>