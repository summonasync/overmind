# AGENTS — Overmind

<!-- squeezr:project-instructions -->
## Squeezr - Project Code Navigation
Managed by squeezr v1.0.4+f4aad83b43aec65653f60430690d93422c8abace - do not edit inside the markers.

This project is indexed by squeezr: a local MCP code index with 6 tools -
repo_map, code_search, symbol_lookup, find_references, expand_span, index_status.

Pick the tool by scenario:
- Unfamiliar repo - repo_map first: projects, folders, key symbols.
- Exact symbol or signature - symbol_lookup: budgeted, returns the method, not the file.
- About to edit a symbol - find_references first: call-graph, no false positives on name matches.
- Paraphrase or domain question - code_search: hybrid, matches exact identifiers and semantics.
- Read a span of code - expand_span: reads the live file from disk.
- Exact string search, or a repo not in the index - grep/read is correct, not a violation.

Cross-repo work: sibling repos join the same workspace with `squeezr add <path> --name <repo>`.
If a repo you need is missing from index_status, say so instead of silently grepping.

Commands:
- `squeezr add <path> --name <repo>` - add a sibling repo to the workspace.
- `squeezr index` - refresh the index now.
- `squeezr watch` - keep the index fresh automatically.
- `squeezr services status` - check qdrant and ollama health.
- `squeezr stats` - show token savings and usage.

Rules:
- index_status reports a non-empty staleFiles list - tell the user explicitly and suggest `squeezr index` or checking the watch daemon.
- A repo missing from index_status - say so instead of silently grepping.
- Tool output is token-budgeted: respect maxTokens and narrow the query when a result is truncated.

To refresh this block, re-run `squeezr mcp install --project`.
<!-- /squeezr:project-instructions -->

Project-level rules for AI coding agents. System design: `CONTEXT.md`. Conventions and repo layout: `STRUCTURE.md`. Decision history (ADRs): `TECHNICAL-SOLUTIONS.md`. Work tracking: the Overmind board (web UI / MCP — the live index) + `BACKLOG/` (materialized feature folders and task contracts; no `BACKLOG.md` index exists). Read them before changing anything — this file is the entry point, not a replacement.

## What this is

Overmind — a win/mac/linux desktop app (.NET 10) giving AI coding agents a shared, persistent context store: one local Host process (Kestrel loopback API, SQLite + FTS5 + optional Qdrant, Avalonia tray icon), a static-export Next.js SPA served by the Host, and a standalone stdio MCP adapter. Full topology: `CONTEXT.md`.

## Stack

- Backend: .NET 10 / C# 14, ASP.NET Core minimal APIs (vertical slices), EF Core (SQLite WAL + FTS5), Serilog, FluentValidation, Avalonia (tray only, no windows)
- Tests: xUnit + Shouldly + NSubstitute — `tests/` mirrors `src/`, each test project references only its target
- Frontend: Next.js 16 static export (`output: "export"` — no Next server in production), React 19, TypeScript strict, Tailwind 4, React Query 5, FSD layering
- MCP adapter: ModelContextProtocol C# SDK; references no other project

## Commands

- Build: `dotnet build Overmind.sln`
- Test: `dotnet test Overmind.sln` (add `--no-build` after a fresh build)
- Format check: `dotnet format --verify-no-changes`
- Run host: `dotnet run --project src/Overmind.Host` (`--no-tray` for headless; UI opens at `http://overmind.localhost:<port>`, bound port in `~/.overmind/host.port`, default 4761)
- EF migrations: `dotnet ef migrations add <Name> --project src/Overmind.Infrastructure` (design-time factory present; migrations apply automatically at Host startup)
- Web (run inside `web/`): `npm run lint && npx tsc --noEmit`, `npm run build` (static export into `web/out/`, served by the Host)

Known machine flake: transient `Internal CLR error 0x80131506` on cold dotnet invocations → `dotnet build-server shutdown`, then retry the same command.

## Hard rules

- Dependency direction is one-way: `Core ← Infrastructure ← Host`; `Overmind.Mcp` references nothing. Never add a reverse or skip-level reference.
- Kestrel binds loopback only — loopback is the security boundary. Never bind a public interface without a new ADR.
- The Host↔MCP contract is additive-only: no renames or removals of DTO fields the adapter reads.
- All planning writes go through the `Infrastructure/Planning` services; planning failures translate only in `PlanningErrors.Map`.
- No new NuGet/npm packages without OSI license vetting and central version pinning (`Directory.Packages.props`).
- No git commits or pushes from agents unless the user explicitly asked in the current session.
- Structural nits discovered but not fixed get recorded (BACKLOG hardening / `audits/`), never silently "fixed" mid-task.

## Working process

- Complex work lives in feature folders `BACKLOG/nnn-slug/`; the executing agent reads ONLY its `tasks/NN-foo.md` contract. `spec.md`/`plan.md` are read-only after validation.
- Templates for feature folders: `BACKLOG/_TEMPLATE/`.
- Reviews run per task (light by default, hard when the task frontmatter says `review: hard`); review artifacts land in `audits/` with a skill prefix.
- Update `CONTEXT.md`/`STRUCTURE.md` in the same change that alters what they describe — doc drift between reviews is how counters rot.
- Removed requirements/decisions get tombstones (`<removed YYYY-MM-DD — reason>`), never silent deletion.
