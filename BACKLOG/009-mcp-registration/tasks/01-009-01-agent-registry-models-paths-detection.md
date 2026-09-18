---
feature: 009
task: 01
slug: mcp-registration
status: done
review: light
created: 2026-09-18
---

# 009/01 — Agent registry: models, paths, detection

<goal>
Overmind.Mcp has a pure, fully unit-tested agent registry: six agent definitions with per-OS config paths, format (json/toml), detection hints, and path resolution including env overrides and the stable-install-link preference.
</goal>

<context>
Feature 009 adds `overmind-mcp install` — self-registration of the MCP adapter into agent configs. This task is the data/resolution core; upsert engines (task 02) and the command (task 03) build on it. The executing subagent reads ONLY this file.
</context>

<scope>
- New namespace area in src/Overmind.Mcp (e.g. Registration/): AgentDefinition record (Name, ConfigFormat json|toml, per-OS user-scope path template, project-scope relative path, detection hints: config dir candidates + binary names for PATH lookup), AgentRegistry with the six agents exactly per the spec table:
  * kimi-code — user `$KIMI_CODE_HOME/mcp.json` else `~/.kimi-code/mcp.json`; project `<gitroot>/.mcp.json`
  * claude — user `~/.claude.json`; project `<gitroot>/.mcp.json`
  * codex — user `$CODEX_HOME/config.toml` else `~/.codex/config.toml`; project `<gitroot>/.codex/config.toml`
  * gemini — user `~/.gemini/settings.json`; project `<gitroot>/.gemini/settings.json`
  * qwen — user `~/.qwen/settings.json`; project `<gitroot>/.qwen/settings.json`
  * antigravity — user `~/.gemini/config/mcp_config.json`; project `<gitroot>/.agents/mcp_config.json` (NOTE: shares ~/.gemini with gemini but a different file — detection must not confuse them)
- Path resolution helpers: `~` expansion per-OS (HOME / USERPROFILE), env var overrides, per-OS variants (win: %USERPROFILE%\.claude.json etc.)
- Stable-link preference: given the running binary path, if a stable link exists at ~/apps/overmind/mcp/overmind-mcp and resolves to the running binary, return the LINK path, else the real path. Do NOT string-rewrite blindly — verify the link actually points at us (File.ResolveLinkTarget or equivalent).
- Detection: agent is "detected" when any config-dir candidate exists OR its binary resolves on PATH (which/where semantics, per-OS extension .exe on windows)
- Project root resolution: walk up from cwd to nearest .git (for --project scope)
- tests/Overmind.Mcp.Tests — unit tests: per-agent user path resolution (linux + simulated env overrides), stable-link preference with real symlinks in temp dirs, antigravity vs gemini disambiguation, detection truth table, git-root walk
</scope>

<anti-scope>
- No JSON/TOML writing (task 02), no Program.cs/CLI changes (task 03)
- No new NuGet packages in this task
- No changes outside src/Overmind.Mcp + tests/Overmind.Mcp.Tests
</anti-scope>

<constraints>
- Overmind.Mcp must keep referencing NO other project
- net10.0, C# 14, no comments, match existing style
- dotnet build Overmind.sln && dotnet test Overmind.sln --no-build — green (baseline 558)
- Flake: Internal CLR error 0x80131506 → dotnet build-server shutdown, retry
</constraints>

<acceptance-criteria>
- [ ] All six agents in the registry with exactly the paths from the spec
- [ ] Env overrides (KIMI_CODE_HOME, CODEX_HOME) honored; ~ expands per-OS
- [ ] Stable-link preference verified with a real symlink fixture
- [ ] Detection disambiguates gemini vs antigravity
- [ ] Tests green, no external I/O beyond temp dirs
</acceptance-criteria>

<refs>
- @src/Overmind.Mcp/Program.cs — entry shape (context only)
- tasks/02-config-upsert.md — consumer of this registry
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-18 — Started: agent registry core
- 2026-09-18 — Done: six-agent registry + path/env/symlink resolution + detection; 617/617 (+59); review PASS (4 NIT to hardening)
</log>
