---
feature: 126
slug: readonly-resolver-no-create
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Spec 126 — ReadOnly resolver: read tools never create project rows (create-free resolve path)

<goal>
A read against an unregistered working directory must fail honestly, not mint a project row. Today every cwd-resolving MCP tool reaches the host through a resolve step that POSTs `/api/onboard` — and that handler upserts: no `DiskPath` match means `Project.Create` + `SaveChanges`. The field case: `read_project_manifest` from an unregistered directory silently inserted a project while the tool descriptor claims `ReadOnly = true`. This feature gives the resolve path explicit create-free semantics so read doors return an honest not-found with registration guidance, and only doors whose job is creating keep minting rows.
</goal>

<scope>
**Ground truth (verified in code 2026-10-06):**
- Host upsert lives in `src/Overmind.Host/Api/OnboardApi.cs` (`Onboard`, ~:41–78): explicit `ProjectId` → lookup-or-404; else normalize cwd (`NormalizeCwd` — symlink resolution + trailing-separator trim) → `DiskPath` match, else create with a unique name and `created = true`.
- The adapter's resolve step is THREE near-identical private copies of `ResolveProjectIdAsync` — `src/Overmind.Mcp/OvermindTools.cs:280`, `src/Overmind.Mcp/FeatureCodeTools.cs:212`, `src/Overmind.Mcp/OvermindNotificationTools.cs:76` (they differ only in the error noun: "entry" / "working directory" / "notification"). All three POST `{ cwd }` to `/api/onboard` when neither the argument nor `OVERMIND_PROJECT_ID` yields a project id.
- Doors that actually resolve from cwd (complete list): `read_project_manifest` (ReadOnly), `planning_feature_code` (ReadOnly), `overmind_replies` (ReadOnly), `delete_entry` (Destructive), `overmind_notify` (mutating). `search` passes `projectId` through without resolving; `list_entries`/`read_entry`/`write_entry`/`update_entry` take a required `Guid projectId`; all `planning_*` tools require explicit projectId by contract ("entries tools resolve the project from cwd, planning tools do not").

**Changes:**
1. **Host — create-free mode on the onboard door.** `OnboardRequest` (`src/Overmind.Host/Api/Contracts/OnboardContracts.cs`) gains an optional `Create` flag defaulting to `true`. When `create=false`, no `projectId` was given and no `DiskPath` match exists, `OnboardApi.Onboard` returns `HttpErrors.NotFound` with guidance text (names `overmind_onboard` to register and passing `projectId` explicitly) and inserts nothing. Every other combination is byte-for-byte today's behavior.
2. **Adapter — one shared resolver.** The three private copies consolidate into one injected helper (`src/Overmind.Mcp/ProjectResolver.cs`, registered in `src/Overmind.Mcp/Program.cs` next to the tool singletons) taking the per-door error noun plus a create flag. Read doors pass create-free; the helper posts `{ cwd, create = false }` and surfaces the host's 404 detail as the tool error.
3. **Read doors flip.** `read_project_manifest`, `planning_feature_code`, `overmind_replies` resolve create-free. Unregistered cwd → `McpException` carrying the honest guidance, no row minted, no retry.
4. **Mutating doors unchanged.** `delete_entry` and `overmind_notify` keep the cwd-only create-on-miss body — they are write doors; if their resolve mints a row that is at least a write-shaped surprise, not a `ReadOnly` lie.
5. **Description pins.** The three read-door descriptions state the no-create contract ("never creates a project row; an unregistered directory returns guidance instead").

**What the read doors return instead of a row:** an honest error result — host-authored 404 detail ("No project is registered for the working directory …"), wrapped by the adapter into the door's usual `McpException` shape with the same escape hatches the current failure path already names: run `overmind_onboard`, pass `projectId` explicitly, or set `OVERMIND_PROJECT_ID`.
</scope>

<anti-scope>
- **`overmind_onboard` keeps create-on-miss** — creating the project row is that tool's job; its description already says "creating it if needed".
- **Registration/install flows** (`src/Overmind.Mcp/Registration/*`) are untouched — the default `create=true` keeps them wire-compatible without edits.
- **`planning_*` tools, `search`, entry tools** — none of them resolves from cwd; nothing to flip.
- **No schema changes, no EF migration** — the flag rides the existing request contract.
- **No new resolve endpoint** — no `GET /api/projects/resolve?cwd=…`; a second endpoint would duplicate the `NormalizeCwd` round-trip that `OnboardApi` already owns.
- **No claim/lock semantics changes** — not-found errors for non-existent projects stay not-found everywhere (feature body non-goal).
- **No adapter-side DiskPath matching** — client-side path normalization (symlinks!) would drift from the host's; the host stays the single matching truth.
- **No changes to the `created` response flag semantics** — `true` still means "a row was minted this call", which after this feature can only happen on `create=true` calls.
</anti-scope>

<constraints>
- **Registered-project round-trip preserved.** A read from a REGISTERED cwd resolves exactly as today: same `NormalizeCwd` (symlink resolution, trailing-separator trim) → `DiskPath` match → `200` with `created=false`. The normalization lives server-side only, unchanged.
- **Wire compatibility.** `Create` defaults to `true`, so every existing caller — `overmind_onboard`, `overmind_notify`, install flows, the adapter's mutating doors — keeps working without changes; the request body for mutating doors stays cwd-only (no `create` key).
- **One guidance text.** The not-found guidance is authored host-side (the 404 detail) and surfaced verbatim by the adapter — no diverging copies of the sentence across doors.
- **Error-noun preservation.** The consolidated resolver keeps per-door error nouns ("for the entry", "for the working directory", "for the notification") so existing failure messages and any pins on them survive the consolidation.
- **Full suites green before close:** `dotnet build` and the full serial `dotnet test` (solution).
- English-only code, messages and commits; no comments except non-obvious decisions.
</constraints>

<acceptance-criteria>
- [ ] Host: `POST /api/onboard` with `create=false` against an unregistered cwd returns 404 with guidance naming `overmind_onboard`/`projectId`, and the projects table is unchanged (count before == count after) — pinned in `tests/Overmind.Api.Tests/OnboardApiTests.cs`.
- [ ] Host: `create=false` against a registered cwd returns the existing project with `created=false`; response shape identical to today.
- [ ] Host: omitted `create` defaults to true — onboard against an unregistered cwd still creates; all existing OnboardApiTests pass untouched (default-path regression guard).
- [ ] Adapter: `read_project_manifest` without projectId posts `create=false`; against a 404 host response it errors with the honest guidance and issues no second resolve call — pinned in `tests/Overmind.Mcp.Tests/ProjectManifestToolContractTests.cs` (the existing `ReadProjectManifest_NoExplicitProject_ResolvesThroughTheOnboardCwd` body pin is deliberately updated from cwd-only to `create=false`).
- [ ] Adapter: `planning_feature_code` and `overmind_replies` carry the same create-free pins (request body + honest 404 error) in `FeatureCodeToolContractTests.cs` / `NotificationToolContractTests.cs`.
- [ ] Adapter: `delete_entry` and `overmind_notify` still post cwd-only resolve bodies (no `create` key) — pinned, so the mutating path cannot silently flip.
- [ ] One shared resolver serves all five cwd-resolving doors; the three private `ResolveProjectIdAsync` copies are gone; build and full serial test suite green.
- [ ] The three read-door descriptions state the never-creates contract; description contract tests updated accordingly.
</acceptance-criteria>

<refs>
- Feature body: 126-readonly-resolver-no-create (the field case and the create-free requirement).
- Host door: `src/Overmind.Host/Api/OnboardApi.cs` (upsert branch), `src/Overmind.Host/Api/Contracts/OnboardContracts.cs` (`OnboardRequest`), `src/Overmind.Host/Api/Validation/OnboardValidators.cs` (`Cwd` required only when `projectId` is null — no rule needed for the flag).
- Adapter resolve copies: `src/Overmind.Mcp/OvermindTools.cs:280`, `src/Overmind.Mcp/FeatureCodeTools.cs:212`, `src/Overmind.Mcp/OvermindNotificationTools.cs:76`; DI registration `src/Overmind.Mcp/Program.cs:96-105`.
- Host tests: `tests/Overmind.Api.Tests/OnboardApiTests.cs` (ApiFactory + real context — the count-before/after pin lives here).
- Adapter tests: `tests/Overmind.Mcp.Tests/ProjectManifestToolContractTests.cs`, `tests/Overmind.Mcp.Tests/NotificationToolContractTests.cs`, `tests/Overmind.Mcp.Tests/FeatureCodeToolContractTests.cs`, stub base `tests/Overmind.Mcp.Tests/McpSessionTestBase.cs` (`RegisterDefaultRoutes` stubs `POST /api/onboard` → `Canned.OnboardJson`, which stays valid for happy paths since the stub ignores the flag).
</refs>

<obstacles>
- The batch context labeled the field incident "066", but board 066 is feature-number-styles — no board feature documents this onboard-create incident. The mechanism was therefore verified directly in code (files and lines above) rather than through an incident record; the constraint "registered-project round-trip preserved" is interpreted as the `NormalizeCwd` round-trip semantics, which is what makes registered cwds resolve at all.
- Existing test `ReadProjectManifest_NoExplicitProject_ResolvesThroughTheOnboardCwd` pins the resolve body as cwd-only; this feature deliberately changes that contract — the test update is part of the change, not a regression.
- `Environment.CurrentDirectory` as the resolve input is today's behavior and stays — cwd identity problems (symlinked worktrees) are already handled by the `OVERMIND_PROJECT_ID` override and are not this feature's scope.
</obstacles>

<log>
- 2026-10-06 [agent] — spec v1 drafted from the feature body + code verification (three resolver copies found, host upsert branch located, complete door inventory fixed in scope). Create-free rides the existing onboard door via a default-true `create` flag; status draft, awaiting human validation tap.
- 2026-10-06 [agent] — validated with human proof: notification b42cf547-1cf2-49e7-9ae4-2a0007fe4b8e replied 2026-10-06 20:18:55 UTC
</log>
