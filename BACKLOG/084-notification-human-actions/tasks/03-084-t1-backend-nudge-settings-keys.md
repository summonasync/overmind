---
feature: 084
task: 03
slug: notification-human-actions
status: done
review: light
created: 2026-09-28
brief: Backend settings keys notificationNudgeMode + notificationNudgeIntervalMinutes with validation and round-trip tests
---

# 084/T1 — Backend: nudge settings keys

<goal>
Two new host-level settings keys backing the 084 bubble discipline: notificationNudgeMode (quiet|nag|mini, default quiet) and notificationNudgeIntervalMinutes (int > 0, default 10). KV AppSettings — no migration.
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
- [x] GET /api/settings returns quiet + 10 defaults on an empty store.
- [x] PUT accepts all three modes; invalid mode / minutes ≤ 0 / non-int → 400.
- [x] SettingsApiTests round-trip facts green (dotnet test).
- [x] dotnet build 0 warnings / 0 errors.
</acceptance-criteria>

<refs>
- Plan 084 (validated 1b7c0c3b), spec 084 (validated eb34ae7a).
- QdrantEndpoint settings keys — the extension precedent.
</refs>

<log>
- 2026-09-28 [agent] — [agent] taken into work — T1 backend settings keys, coder dispatched (sonnet)
- 2026-09-28 [agent] — [agent] implemented — KV keys notification.nudgeMode/notification.nudgeIntervalMinutes in SettingKeys, defaults quiet/10 + NotificationNudgeModes array in SettingDefaults, contract fields on Settings/UpdateSettings request+responses, validation (mode case-insensitive → lowercase store, minutes ≥ 1) + persistence + read in SettingsApi, 10 new test cases in SettingsApiTests. dotnet build 0w/0e; Api.Tests 488/488 green; dotnet format clean on touched files (pre-existing migration IDE0161s untouched).
- 2026-09-28 [agent] — added <obstacles> section (absent at dispatch) — non-int 400 comes from body binding on int?, KV keys use dotted naming
- 2026-09-28 [agent] — [agent] tooling slip — a full-body upsert without `order` created duplicate task d07f060a (order 8); tombstoned and closed with provenance, delete via UI if desired. All T1 evidence stays on this artifact.
- 2026-09-28 [agent] — [agent] done — all 4 AC ticked and verified: build 0w/0e, settings tests 33/33 in filter, full Overmind.Api.Tests 488/488, format clean on touched files. Changed: SettingKeys.cs, SettingDefaults.cs, SettingsContracts.cs, SettingsApi.cs, SettingsApiTests.cs.
- 2026-09-28 [agent] — [agent] review APPROVED (0 blockers/nits, 3 notes): contract round-trip with web T4 matches 1:1 (field names, union values, defaults), partial-update preservation pinned by tests. Notes accepted: non-int minutes → plain 400 from model binding (documented in obstacles, FE does not parse error shape); read-path silently falls back to default 10 on a corrupted store value (KV precedent-consistent); tombstone duplicate d07f060a awaits UI deletion.
</log>