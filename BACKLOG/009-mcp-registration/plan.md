---
feature: 009
slug: mcp-registration
status: validated
created: 2026-09-18
validated: 2026-09-18
---

# Plan 009 — MCP registration command for existing agents

<approach>
Pure core first (registry + upsert engines as testable units, no I/O beyond the given paths), then the command surface in Program.cs, then docs + live dogfood. All of it lives in Overmind.Mcp (references nothing, stays that way) plus its test project. Tomlyn is added to Overmind.Mcp only and pinned centrally in Directory.Packages.props (approved 2026-09-18).
</approach>

<tasks>
- [ ] 01-agent-registry — AgentDefinition model (name, format, per-OS user path, project path template, detection hints); AgentRegistry with the six agents; path resolution incl. KIMI_CODE_HOME/CODEX_HOME env overrides and ~ expansion; stable-link preference (if the running binary resolves under ~/apps/overmind, register that symlink path, not the real versioned one); detection (config dir exists or binary on PATH) → tasks/01-agent-registry.md. Verify: dotnet test
- [ ] 02-config-upsert — format engines: JSON upsert via System.Text.Json dom (preserve unknown keys, only mcpServers.overmind touched), TOML upsert via Tomlyn ([mcp_servers.overmind] table, preserve other tables/comments as far as Tomlyn allows); backup (<file>.<timestamp>.bak before first mutation); remove; dry-run plan (no writes); upsert result enum (added/updated/unchanged/removed) → tasks/02-config-upsert.md. Verify: dotnet test
- [ ] 03-install-command — Program.cs intercepts args[0]=="install" BEFORE the health check/host boot (install must work with the host down); flag parsing (--all/--agents/--project/--remove/--dry-run); per-agent result table; exit codes; Directory.Packages.props pin + csproj reference → tasks/03-install-command.md. Verify: dotnet build + dotnet test
- [ ] 04-docs-dogfood-closeout — CONTEXT.md: install command + registry table; live run: overmind-mcp install --dry-run then real (kimi-code user-global + detected); verify written configs parse and point at ~/apps/overmind; materialize; result artifact; feature done → tasks/04-docs-dogfood-closeout.md. Verify: live run + dotnet test
</tasks>

<risks>
- Tomlyn comment/format preservation is best-effort — mitigate: codex config is usually simple; backup covers worst case; unit test pins the common shape.
- Program.cs currently feeds args to Host.CreateApplicationBuilder — the intercept must come first and return without touching the MCP host (smoke: `overmind-mcp install --dry-run` with host DOWN must exit 0).
- Symlink resolution: Process.MainModule/Environment.ProcessPath returns the REAL path, not the symlink — the stable-link preference must check whether a stable-link candidate exists and points at the running binary, not just string-rewrite the path.
- Claude user scope lives in ~/.claude.json top-level mcpServers — the file also holds per-project state; upsert must not disturb unrelated keys (pinned by test with a realistic fixture).
- Detection false-positives: ~/.gemini shared by gemini AND antigravity (different files) — detect antigravity by its config dir/binary, not by ~/.gemini alone.
</risks>

<verification>
- dotnet build Overmind.sln && dotnet test Overmind.sln — green incl. registry/upsert/command tests
- Live: overmind-mcp install --dry-run (host down) prints the plan; real run registers kimi-code user-global at ~/apps/overmind path; second run reports unchanged; --remove cleans up; configs stay valid JSON/TOML
- BACKLOG/009-mcp-registration materializes clean
</verification>