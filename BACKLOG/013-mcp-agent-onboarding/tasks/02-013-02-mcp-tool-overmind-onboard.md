---
feature: 013
task: 02
slug: mcp-agent-onboarding
status: done
review: light
created: 2026-09-19
---

# 013/02 — MCP tool overmind_onboard

<goal>
The stdio adapter exposes a new MCP tool `overmind_onboard` (no required args) that reports its own working directory to the host's onboard endpoint and returns the rich payload as readable text — so any agent seeing the overmind server can onboard in one call.
</goal>

<context>
Feature 013, door #2. The adapter inherits cwd from the spawning agent process (stdio) — that cwd IS the repo the agent works in. The Squeezr dogfood lesson: available ≠ used — the tool description must open with "Call this first". Depends on task 01 (endpoint contract).
</context>

<scope>
- New tool class `src/Overmind.Mcp/OvermindOnboardingTools.cs` ([McpServerToolType], follows OvermindPlanningTools conventions: Guarded wrapper, OvermindApiClient), registered in `src/Overmind.Mcp/Program.cs` via `.WithTools<OvermindOnboardingTools>()`.
- Tool `overmind_onboard`: resolves `Environment.CurrentDirectory`; env `OVERMIND_PROJECT_ID` (valid guid) wins and is sent as `projectId` instead of cwd-matching; POSTs to `/api/onboard`; formats the response as text: project name + id, "created project X" when created, board counts, current feature, then the quickstart text VERBATIM from the payload.
- Tool description opens with "Call this first" and explains the OVERMIND_PROJECT_ID override for symlinked/worktree layouts.
- Update `ServerInstructions` in Program.cs: one line telling agents to call `overmind_onboard` before other tools.
- Tests in `tests/Overmind.Mcp.Tests/`: tool registration (count grows by exactly one), description text contract, env-override branch, unreachable-host error path.
</scope>

<anti-scope>
- No host changes (task 01 owns the endpoint); no installer changes (task 03).
- No renames/removals of existing tools or DTO fields — additive only.
- Do NOT duplicate the quickstart text in the adapter — it passes through from the host payload.
</anti-scope>

<constraints>
- Overmind.Mcp references NO other project — keep it that way.
- ModelContextProtocol C# SDK idioms already in the file set; no new packages.
- `dotnet build Overmind.sln` + `dotnet test Overmind.sln --no-build` green. Known flake: port race in Mcp.Tests → rerun once before suspecting code.
</constraints>

<acceptance-criteria>
- [ ] MCP tools/list shows `overmind_onboard` with a description starting with "Call this first".
- [ ] With cwd = a known project path, the tool returns that project's id + board summary + quickstart (integration via the existing Mcp.Tests host harness).
- [ ] OVERMIND_PROJECT_ID set to a valid guid → request carries projectId, not cwd.
- [ ] Host down → clean McpException-style error, no stack dump to the agent.
- [ ] Total tool count = previous + 1; dotnet test green.
</acceptance-criteria>

<refs>
- tasks/01-onboard-endpoint.md — endpoint contract this tool consumes.
- Pattern refs: `src/Overmind.Mcp/OvermindPlanningTools.cs`, `src/Overmind.Mcp/OvermindApiClient.cs`, `src/Overmind.Mcp/ApiBaseUrl.cs`, `src/Overmind.Mcp/Program.cs`.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-19 — Execution started — handed to dotnet-coder subagent (parallel with 01 review).
- 2026-09-19 — Coder done: overmind_onboard tool + ServerInstructions line + contract tests, suite 752/752 green. To review.
- 2026-09-19 — Review PASS WITH NOTES (audits/review-013-02.md); no fix-loop required — one cosmetic dead-condition suggestion left. Done.
</log>