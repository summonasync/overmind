---
feature: 009
task: 03
slug: mcp-registration
status: done
review: light
created: 2026-09-18
---

# 009/03 — Install command surface in Program.cs

<goal>
`overmind-mcp install` works end-to-end: flag parsing, detection-driven agent selection, per-agent result table, sane exit codes — and it must run with the Overmind host DOWN (install intercepts before the health check).
</goal>

<context>
Tasks 009/01-02 delivered the registry and the upsert engines. This task wires the CLI surface in src/Overmind.Mcp/Program.cs (currently top-level statements feeding args into Host.CreateApplicationBuilder after a health check). The executing subagent reads ONLY this file.
</context>

<scope>
- src/Overmind.Mcp/Program.cs — intercept `args[0] == "install"` FIRST, before ApiBaseUrl.Resolve/health check/Host builder; install returns an exit code without starting the MCP server
- Flag parsing (hand-rolled, consistent with the file's simplicity — no CLI package): --all, --agents <csv>, --project, --remove, --dry-run; unknown flag → usage to stderr + exit 2
- Selection: --agents wins; else --all → all six; else detected-only (registry detection)
- Scope: default user; --project resolves git root from cwd (walk up to .git; none → clear error)
- Output: per-agent lines `name: added|updated|unchanged|removed|skipped (not detected)|failed — <path>`; summary line; dry-run prefixes with `would `
- Exit codes: 0 = everything ok or nothing to do; 1 = any failure; 2 = usage error
- The registered command path comes from the registry's stable-link preference (task 01)
- tests/Overmind.Mcp.Tests — command-level tests if a seam exists cheaply (extract the install runner into a testable class with injected home/env/cwd; keep Program.cs a thin shell); pin: host-down dry-run works, --agents subset, --remove flow, usage error path
</scope>

<anti-scope>
- No changes to MCP tool/resource registration for the normal server path
- No new packages
- No docs (task 04)
</anti-scope>

<constraints>
- dotnet build Overmind.sln && dotnet test Overmind.sln --no-build — green
- Program.cs stays readable top-level style; if it grows past comfort, extract Registration/InstallCommand.cs
</constraints>

<acceptance-criteria>
- [ ] `overmind-mcp install --dry-run` with host down exits 0 and prints the plan
- [ ] Real run writes configs (temp HOME in tests), second run reports unchanged
- [ ] --remove cleans the overmind key only
- [ ] Unknown flag → usage + exit 2
- [ ] MCP server path (no args) unchanged — existing MCP contract tests still green (15 tools)
</acceptance-criteria>

<refs>
- @src/Overmind.Mcp/Program.cs — current entry
- tasks/01-agent-registry.md, tasks/02-config-upsert.md — dependencies (done)
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-18 — Coder done: install surface + parser + runner; 675/675; live smoke ok; awaiting review
- 2026-09-18 — Done: install surface complete; fix-loop closed review WARNING-1 (per-agent IO isolation); 676/676; re-review PASS
</log>
