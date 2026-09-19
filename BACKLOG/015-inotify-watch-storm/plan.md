---
feature: 015
slug: inotify-watch-storm
status: validated
created: 2026-09-19
validated: 2026-09-19
---

# Plan 015 — Bug: host + mcp adapter inotify-watch the whole cwd tree

<approach>
Both binaries get their content root pinned to AppContext.BaseDirectory in one mechanical task, with the host's webRoot fallback re-based in the same change (the pin and the fallback are one logical unit — splitting them breaks dev mode in between). The regression guard lands next as a Linux-only smoke script riding the existing build/smoke.sh shape, proven against a deep dummy tree. Closeout packs 0.1.5, deploys live, and measures the fleet's inotify pressure before/after — that drop is the dogfood evidence for the result artifact.
</approach>

<tasks>
- [ ] 01-content-root-pin — Overmind.Mcp: HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory }; Overmind.Host: WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory }; webRoot resolution re-based: OVERMIND_WEB_ROOT → <BaseDirectory>/../web → bounded upward walk (max ~8 levels) from BaseDirectory for web/out; no appsettings.json exists in src or pack layout (verified) so nothing else moves; local strace proof: adapter from a 300-dir tree registers ≤ ~5 inotify watches, host from $HOME serves UI and watches only the install tree → tasks/01-content-root-pin.md. Verify: dotnet build Overmind.sln && dotnet test Overmind.sln + manual strace/fdinfo numbers
- [ ] 02-watch-guard-smoke — Linux-only guard script (new build/watch-guard.sh, wired into build/smoke.sh next to the existing cells): builds nothing, runs the packed/published adapter from a generated deep dummy tree (e.g. 300 dirs), counts inotify watches via /proc/<pid>/fdinfo, fails above threshold (~25); same check for the host (start, probe /api/health, count, kill); skips cleanly on non-Linux → tasks/02-watch-guard-smoke.md. Verify: bash build/smoke.sh green on this machine; guard demonstrably fails when run against an unpatched binary (prove once against the 0.1.4 install, then delete that proof run)
- [ ] 03-docs-live-closeout — CONTEXT.md note (content root no longer follows cwd; adapters spawn from any repo at zero watch cost), run.sh comment if it still says anything cwd-related; full gates; pack 0.1.5 (bundle-version 2), deploy via the tar+symlink+restart ritual, live measure: per-process watch counts of the new host + a fresh adapter vs the 0.1.4 baseline (43312 / 2955), Squeezr-agent note that starvation pressure is gone; materialize BACKLOG, result artifact, close feature, commit+push → tasks/03-docs-live-closeout.md. Verify: live numbers + dotnet test Overmind.sln
</tasks>

<risks>
- webRoot upward walk: must be bounded (max ~8 levels) and stop at filesystem root; an unbounded walk could escape into a parent that happens to contain web/out (e.g. a dev home dir) and serve the wrong UI — order matters: OVERMIND_WEB_ROOT always wins, then pack-relative, then the walk.
- Host startup in dev: dotnet run sets cwd to the project dir, so pre-pin dev worked by accident; after the pin, BaseDirectory is bin/Debug/net10.0 — the walk must cover that depth (net10.0 → Debug → bin → Overmind.Host → src → repo root = 5 levels) or dev loses the UI. Acceptance has an explicit dev-mode check.
- The guard counts watches of the whole process tree it spawns — count only the target pid, not strace/shell children; use the fdinfo method (no strace dependency in CI).
- pkill -f Overmind.Host returns a bogus exit code on this machine — the guard and closeout must verify host state via curl, not exit codes.
</risks>

<verification>
- dotnet build Overmind.sln && dotnet test Overmind.sln — green (677 baseline)
- bash build/smoke.sh — green incl. the new watch-guard cell on Linux
- Live 0.1.5: host from any cwd watches only its install tree; adapter from the Overmind repo root shows ≤ ~5 watches (was 2955); fleet total drops from ~64k to < 5k of 65536
- BACKLOG/015-inotify-watch-storm materializes clean
</verification>
