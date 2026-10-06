---
feature: 126
slug: readonly-resolver-no-create
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Plan 126 — ReadOnly resolver: read tools never create project rows (create-free resolve path)

> Grooming 2026-10-06. 4 slices, sequential: 01 → 02 → 03 → 04. Slices 01 and 02 are independent of each other (host contract vs adapter consolidation); 03 consumes both; 04 is the closing gate.

## Slice chain

**01 — Host: create flag on the onboard door**
- `src/Overmind.Host/Api/Contracts/OnboardContracts.cs` — `OnboardRequest(string Cwd, Guid? ProjectId, bool Create = true)`.
- `src/Overmind.Host/Api/OnboardApi.cs` — in the no-projectId branch: when `!request.Create` and no `DiskPath` match, return `HttpErrors.NotFound` with the guidance detail ("No project is registered for the working directory '…'. Call overmind_onboard to register it, or pass projectId explicitly.") instead of `Project.Create` + save. The match path and the explicit-projectId path stay byte-for-byte identical; `created` stays false on matches.
- `src/Overmind.Host/Api/Validation/OnboardValidators.cs` — no new rule (bool defaults true; nothing to validate). Confirm and leave alone.
- Tests — `tests/Overmind.Api.Tests/OnboardApiTests.cs`, three additions:
  - `create=false` + unregistered cwd → 404, guidance contains "overmind_onboard" and "projectId", and `db.Projects` count is unchanged (count before == count after — the feature's core pin).
  - `create=false` + registered cwd → 200, returns the existing project, `created=false`.
  - body without `create` + unregistered cwd → still creates (default-true regression guard; asserts the "Created" behavior survives).
- Verify: `dotnet build` + `dotnet test tests/Overmind.Api.Tests`.

**02 — Adapter: one shared resolver (behavior-neutral consolidation)**
- New `src/Overmind.Mcp/ProjectResolver.cs` — injected helper over `OvermindApiClient`: `Task<Guid> ResolveAsync(Guid? projectId, bool create, string errorNoun, CancellationToken ct)`. Argument → env override → onboard POST (`{ cwd }` when create, `{ cwd, create = false }` when not). The create-free path maps `ApiNotFoundException` to an `McpException` carrying the host's 404 detail (the honest guidance); other failures keep today's wrap. Per-door error nouns preserved.
- `src/Overmind.Mcp/Program.cs` — register the resolver singleton next to the tool singletons (~:96–105); tool classes take it via constructor.
- `src/Overmind.Mcp/OvermindTools.cs`, `src/Overmind.Mcp/FeatureCodeTools.cs`, `src/Overmind.Mcp/OvermindNotificationTools.cs` — delete the three private `ResolveProjectIdAsync` copies (:280 / :212 / :76), call sites route through the shared resolver with create=true and their existing noun. No behavior change in this slice.
- Verify: `dotnet build` + full `Overmind.Mcp.Tests` green unchanged (proves the consolidation is neutral before any contract flips).

**03 — Read doors flip + contract test pins**
- `src/Overmind.Mcp/OvermindTools.cs` — `read_project_manifest` resolves with create=false; description gains the never-creates sentence.
- `src/Overmind.Mcp/FeatureCodeTools.cs` — `planning_feature_code` same flip + description sentence.
- `src/Overmind.Mcp/OvermindNotificationTools.cs` — `overmind_replies` same flip + description sentence. `delete_entry` and `overmind_notify` stay create=true (no code change; pinned by tests).
- Tests:
  - `tests/Overmind.Mcp.Tests/ProjectManifestToolContractTests.cs` — update `ReadProjectManifest_NoExplicitProject_ResolvesThroughTheOnboardCwd` to pin `create=false` in the resolve body; new test: stub 404 on `POST /api/onboard` → tool `isError`, text carries the guidance, exactly one resolve call (no retry).
  - `tests/Overmind.Mcp.Tests/FeatureCodeToolContractTests.cs` — same pair of pins for `planning_feature_code`.
  - `tests/Overmind.Mcp.Tests/NotificationToolContractTests.cs` — create=false pin + 404 honest error for `overmind_replies`; a pin that `overmind_notify`'s resolve body has NO `create` key (mutating door cannot silently flip).
  - Description pins updated where the three doors' descriptions are asserted.
- Verify: `dotnet build` + `dotnet test tests/Overmind.Mcp.Tests`.

**04 — Closing gate**
- `dotnet build` (solution), then the full serial suite `dotnet test` — all projects green, nothing skipped silently.
- AC sweep against the spec: every acceptance item has a named pin or a visible no-change proof.
- Report changed files; no commit unless the user asks (standing rule — though this feature's landed convention has been per-slice conventional commits as work-preservation checkpoints, on the user's word).

## Risks / notes
- The `create=false` 404 rides the same `HttpErrors.NotFound` shape the explicit-projectId path already returns, so the adapter's existing `ApiNotFoundException` plumbing carries it without new error machinery.
- `Canned.OnboardJson` and `RegisterDefaultRoutes` (McpSessionTestBase) keep stubbing a 200 onboard — happy-path tests stay untouched; only the resolve-body pins change.
- If the consolidation in 02 surfaces an unexpected behavioral difference (the three copies' catch sets differ slightly), stop and record it in the task's `<obstacles>` rather than papering over it — the copies were verified near-identical but not byte-identical.

<log>
- 2026-10-06 [agent] — plan v1 drafted: 4 sequential slices (host flag → neutral resolver consolidation → door flips + contract pins → full-suite gate), exact files and per-slice verification; statuses draft, awaiting human validation tap.
- 2026-10-06 [agent] — validated with human proof: notification b42cf547-1cf2-49e7-9ae4-2a0007fe4b8e replied 2026-10-06 20:18:55 UTC
</log>
