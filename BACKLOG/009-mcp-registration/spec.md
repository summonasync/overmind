---
feature: 009
slug: mcp-registration
status: validated
created: 2026-09-18
validated: 2026-09-18
---

# Spec 009 — MCP registration command for existing agents

<decisions>
- File-based config upsert, not agent CLIs: parse JSON/TOML, rewrite only the `overmind` key, preserve everything else, timestamped backup before first write. Idempotent at all six agents; no dependency on agent CLIs being installed (user decision 2026-09-18).
- The command lives on the MCP adapter itself: `overmind-mcp install` — the binary self-registers (user decision 2026-09-18).
- Default scope is user-global; `--project` switches to project scope (user decision 2026-09-18).
- Codex TOML handled with Tomlyn (MIT), added to Overmind.Mcp only, central-pinned (user decision 2026-09-18).
- Registered command path prefers the stable install link (~/apps/overmind/mcp/overmind-mcp) when the binary runs under it, so registrations survive version upgrades (install-layout decision, 0.1.1 dogfood 2026-09-17).
- Server name is `overmind` at every agent (gemini's policy parser breaks on underscores — we are safe).
- Default agent set = detected agents (config dir exists or binary on PATH); `--all` forces all six.
</decisions>

<open-questions>
- none
</open-questions>

<requirements>
Command surface (on the adapter binary):
- `overmind-mcp install` — register at detected agents, user scope
- `overmind-mcp install --all` — all six agents regardless of detection
- `overmind-mcp install --agents kimi,claude` — explicit subset
- `overmind-mcp install --project` — project scope instead of user scope
- `overmind-mcp install --remove` — unregister (delete only the `overmind` key, same scope rules)
- `overmind-mcp install --dry-run` — print the plan, write nothing
- Exit codes: 0 all ok / nothing to do; non-zero on any write failure; per-agent result table printed (added / updated / unchanged / skipped-not-detected / failed)
- No args at all = current behavior (run the stdio server) — install must not hijack the default path

Agent registry (per agent: name, format json/toml, per-OS user-scope path, project-scope path, detection hints):
- kimi-code: user `$KIMI_CODE_HOME/mcp.json` (fallback `~/.kimi-code/mcp.json`), JSON `mcpServers`; project `<gitroot>/.mcp.json`
- claude: user `~/.claude.json` (top-level `mcpServers` key — user scope only, do NOT touch the per-project nesting); project `<gitroot>/.mcp.json`
- codex: user `$CODEX_HOME/config.toml` (fallback `~/.codex/config.toml`), TOML `[mcp_servers.overmind]`; project `<gitroot>/.codex/config.toml`
- gemini: user `~/.gemini/settings.json`, JSON `mcpServers`; project `<gitroot>/.gemini/settings.json`
- qwen: user `~/.qwen/settings.json`, JSON `mcpServers`; project `<gitroot>/.qwen/settings.json`
- antigravity: user `~/.gemini/config/mcp_config.json` (note: shares the ~/.gemini dir with gemini but a DIFFERENT file), JSON `mcpServers`; project `<gitroot>/.agents/mcp_config.json`

Entry written (stdio, minimal): `{ "command": "<stable path to overmind-mcp>" }` — no args/env by default.

Implementation:
- Registry + upsert logic in Overmind.Mcp (it references no other project — stays that way)
- JSON via System.Text.Json (dom mutation preserving unknown keys), TOML via Tomlyn
- Windows/mac path variants resolved per-OS (%USERPROFILE% etc.); live-tested on linux only, others by unit tests on path resolution
- Unit tests in tests/Overmind.Mcp.Tests: upsert idempotency per format, key preservation, backup creation, remove, dry-run, detection
- CONTEXT.md gains the install command + registry table; STRUCTURE.md if conventions emerge
</requirements>

<non-goals>
- Agent launcher / onboarding wizards (P1/P3)
- Runtime skill delivery via MCP (P2)
- HTTP-transport registration, GUI, MCP Store integration
- Touching any non-`overmind` key in user configs
</non-goals>