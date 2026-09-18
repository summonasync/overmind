---
feature: 009
task: 02
slug: mcp-registration
status: done
review: light
created: 2026-09-18
---

# 009/02 — Config upsert engines (JSON + TOML)

<goal>
Idempotent, backup-guarded upsert/remove/dry-run engines for both config formats: only the `overmind` key is ever written; everything else in the user's config file survives byte-for-byte as far as the format allows.
</goal>

<context>
Task 009/01 delivered the agent registry (paths, formats, detection). This task implements the write side. Real user configs are sacred — a corrupted ~/.claude.json or config.toml is a disaster, so backups and preservation are the point of the task. The executing subagent reads ONLY this file.
</context>

<scope>
- src/Overmind.Mcp (Registration/ area):
  * IConfigUpsertEngine or equivalent seam with two implementations
  * JSON engine: System.Text.Json dom (JsonNode/JsonObject) — read file (missing → treat as empty object), ensure `mcpServers` object, set/remove the `overmind` entry `{"command": "<path>"}`, write back with the same indentation style the file uses (detect 2/4 spaces, default 2)
  * TOML engine: Tomlyn — parse, upsert/remove the `[mcp_servers.overmind]` table with `command = "<path>"`, preserve other tables; document in obstacles how much formatting survives
  * Backup: before the first mutation of a file in a run, copy to `<file>.<yyyyMMddHHmmss>.bak`; unchanged result → no write, no backup
  * Result model: per-agent outcome enum (Added / Updated / Unchanged / Removed / NotPresent / Failed) + error text
  * Dry-run: compute the plan (what WOULD change per file) without any writes
- NuGet: add Tomlyn to Directory.Packages.props (central pin, latest stable, MIT — pre-approved by user 2026-09-18) and reference it from src/Overmind.Mcp/Overmind.Mcp.csproj ONLY
- tests/Overmind.Mcp.Tests — fixtures: realistic ~/.claude.json (top-level mcpServers + unrelated project state — untouched), codex config.toml (other mcp_servers + unrelated sections — untouched), idempotency (second run = Unchanged, no new backup), remove, dry-run writes nothing, missing file creation, malformed file → Failed with error, no partial write
</scope>

<anti-scope>
- No Program.cs / CLI surface (task 03)
- No agent CLI invocation anywhere
- No changes to other projects
</anti-scope>

<constraints>
- Atomic-ish writes: write temp file + move, never truncate-in-place
- dotnet build Overmind.sln && dotnet test Overmind.sln --no-build — green
- Flake: Internal CLR error 0x80131506 → dotnet build-server shutdown, retry
</constraints>

<acceptance-criteria>
- [ ] JSON upsert preserves unknown keys and unrelated structure (pinned by fixture test)
- [ ] TOML upsert preserves other tables (pinned)
- [ ] Re-run convergence: Unchanged + no write + no backup
- [ ] Remove deletes only the overmind key (both formats)
- [ ] Malformed input → Failed, original file untouched
- [ ] Tomlyn centrally pinned, referenced only by Overmind.Mcp
</acceptance-criteria>

<refs>
- tasks/01-agent-registry.md — registry (done)
- @Directory.Packages.props — central pinning convention
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-18 — Coder done: JSON+TOML upsert engines, backups, atomic writes; 651/651; awaiting review
- 2026-09-18 — Done: JSON+TOML upsert engines with backups/atomic writes; 651/651; review no-blockers (Tomlyn pinned 0.20.0 DOM line by design, license actually BSD-2-Clause OSI-approved; IO-error model deferred to 03 boundary)
</log>
