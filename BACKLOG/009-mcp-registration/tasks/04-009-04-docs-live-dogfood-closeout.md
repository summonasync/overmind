---
feature: 009
task: 04
slug: mcp-registration
status: done
review: light
created: 2026-09-18
---

# 009/04 — Docs + live dogfood closeout

<goal>
009 is documented (CONTEXT.md), proven live on the real machine (install/dry-run/remove cycles against real agent configs), and the feature is closed with a result artifact.
</goal>

<context>
Tasks 009/01-03 are done and reviewed. The executing agent orchestrates the closeout: docs, repack/reinstall, live runs, materialization, result. Installed layout: ~/apps/overmind symlink → versioned dir; host at 127.0.0.1:4761; project id ff313f29-8144-488c-94e6-5ea5dde57bfd.
</context>

<scope>
- CONTEXT.md — new section: `overmind-mcp install` command, flags, agent registry table (agent → user path / project path / format), backup + idempotency notes
- Repack 0.1.3 (bash build/pack.sh --version 0.1.3 --bundle-version 2 --rids linux-x64), install to ~/apps/overmind-0.1.3-linux-x64, flip ~/apps/overmind symlink, restart host headless
- Live scenario:
  1. `~/apps/overmind/mcp/overmind-mcp install --dry-run` with host DOWN (stop it first) → plan prints, exit 0; restart host
  2. Real install (default detected agents) → inspect written configs (they parse; overmind key points at the ~/apps/overmind path; unrelated keys intact)
  3. Re-run → all unchanged
  4. `--remove` on one agent → key gone, file valid; re-install that agent back
  5. Existing repo .kimi-code/mcp.json (project scope) untouched by the user-scope run
- Materialize planning; BACKLOG/009-mcp-registration clean in git status
- Result artifact (kind=result) with evidence + deferred; validate it; feature → done via human door PUT
</scope>

<anti-scope>
- No code changes beyond trivial fixes (bigger → new task file)
- No git push if the remote is down (report instead)
- Do NOT commit unless the user asked in this session (they did: user delegates commits)
</anti-scope>

<constraints>
- Snapshot ~/.overmind before the new host boots (upgrade ritual)
- pack.sh via `bash build/pack.sh` (the +x bit is flaky)
</constraints>

<acceptance-criteria>
- [ ] dotnet build + dotnet test green
- [ ] Live dry-run/install/re-run/remove cycle clean on real configs
- [ ] CONTEXT.md documents the command
- [ ] result artifact validated; feature done
</acceptance-criteria>

<refs>
- @../plan.md — verification section
- tasks/01..03 — implementation (done)
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-18 — Started: docs + live dogfood closeout
- 2026-09-18 — Closeout done: CONTEXT.md, 0.1.3 live, dry-run/install/re-run/remove all verified on real configs, stable-link bug found+fixed live; result validated
</log>
