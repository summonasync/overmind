---
feature: 009
slug: mcp-registration
status: validated
created: 2026-09-18
validated: 2026-09-18
---

# Result 009 — MCP registration command for existing agents

<summary>
Shipped: `overmind-mcp install` — the MCP adapter self-registers into agent configs. Six-agent registry (kimi-code, claude, codex, gemini, qwen, antigravity) with per-OS path resolution, env overrides and detection; idempotent file-based upsert engines (JSON via STJ dom, TOML via Tomlyn 0.20 DOM line) writing only the `overmind` key, timestamped backups, atomic temp+move writes; CLI surface with --all/--agents/--project/--remove/--dry-run, per-agent result table, exit codes 0/1/2; install intercepts before the host health check (works host-down). Registered path prefers the stable ~/apps/overmind link so registrations survive upgrades.
</summary>

<evidence>
- Tests: 677/677 green (Mcp 180; baseline was 558 before the feature).
- Reviews: 01 PASS / 02 no-blockers / 03 PASS after fix-loop (per-agent IO isolation) — audits/review-009-03.md.
- Live dogfood on 0.1.3 (aa839b5): dry-run with host DOWN printed the full 6-agent plan, exit 0; real install added kimi-code/claude/qwen (codex/gemini/antigravity correctly skipped-not-detected); re-run all unchanged; remove+re-add on qwen clean; ~/.claude.json kept its 38 unrelated top-level keys.
- Live-found bug fixed mid-closeout: the stable-link preference missed directory-symlink chains (~/apps/overmind -> versioned dir) because ResolveLinkTarget resolves only leaf links — first install wrote the real versioned path; fixed with component-wise chain walking + a directory-symlink test, repacked, re-installed, configs converged to the stable link via the update path.
</evidence>

<deferred>
- Dev-loop caveat (recorded in CONTEXT.md): install from `dotnet exec overmind-mcp.dll` would register the muxer path — packaged binary only.
- Review NITs to hardening: GitRootResolver logical-path walk vs symlinked cwd; PathEquals Ordinal on macOS; PATH executable-bit check; JSON upsert escapes non-ASCII and drops BOM/comments (JSONC configs fail closed); full-replace of an existing overmind entry silently drops user args/env — surface louder in output if it ever bites.
- Tomlyn pinned at 0.20.0 (last DOM API line; 1.x/2.x switched to source-generated serializers) — revisit if security/maintenance pressure appears; license is BSD-2-Clause (OSI-approved), not MIT as the task text assumed.
- Binary-name detection for kimi/antigravity/qwen on PATH is an assumption — verify on real installs.
</deferred>