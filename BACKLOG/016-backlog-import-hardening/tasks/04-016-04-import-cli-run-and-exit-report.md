---
feature: 016
task: 04
slug: backlog-import-hardening
status: done
review: light
created: 2026-09-19
---

# 016/04 — Import CLI: run-and-exit + --report

<goal>
`--import-backlog` is a thin run-and-exit command: it imports against the SQLite store and exits, never boots Kestrel/tray, never touches host.port, and can duplicate its report to a file via --report <path>.
</goal>

<context>
Feature 016, fourth task. Dogfood findings B1/B2/B3 from the Squeezr agent: a second instance running --import-backlog overwrote ~/.overmind/host.port with its own fallback port (new MCP sessions then went to the wrong port); the import CLI boots as a full Host instead of exiting (awkward for scripts); the import report goes only to stdout. The executing subagent reads ONLY this file.

Entry point: src/Overmind.Host/BacklogImportCommand.cs + Program.cs wiring (find where --import-backlog branches off the normal host boot). The boot-time BACKLOG import (host startup import) is a DIFFERENT path and must keep working.
</context>

<scope>
- The --import-backlog path: parse args (existing parser if any), run the import via the existing importer services against the profile DB, print the report, Environment-exit with 0/non-zero — without constructing/booting the web host (no Kestrel, no Avalonia tray, no port resolution, no host.port write). If today's implementation reuses the host composition, build a minimal service provider for the import path instead (db context + importer + registry); keep the boot-import path intact.
- --report <path>: write the exact stdout report text to the file (UTF-8, no BOM unless the codebase already does BOM elsewhere); parent dir must exist (clean error if not).
- Smoke-level proof (do it, log results in the task <log>): run the CLI against a THROWAWAY profile copy (find the profile-path knob — env var or default ~/.overmind; never the live one), with the live host on :4761 running: assert host.port mtime/content unchanged, process exits by itself, exit code 0, report file == stdout.
- If the CLI currently locks/migrates the DB, keep the same EF migration behavior (it is the documented boot behavior) — but no web concerns.
</scope>

<anti-scope>
- No changes to the boot-time import semantics (what gets imported at host startup stays identical)
- No importer logic changes (tasks 02/03 own those)
- No MCP/API surface changes
</anti-scope>

<constraints>
- .NET 10 / C# 14; no new NuGet packages; no comments
- Do not disturb the live host on :4761 or ~/.overmind/host.port — throwaway profiles/ports only; verify via curl, never via pkill exit codes (bogus on this machine)
- dotnet build + dotnet test green; if CLI paths are untestable in-process today, a script-level proof in the log is acceptable (note it)
</constraints>

<acceptance-criteria>
- [ ] --import-backlog exits after the report (no Kestrel/tray boot — proof in log)
- [ ] host.port untouched during CLI runs (proof in log)
- [ ] --report writes stdout-identical text to the given path
- [ ] Boot-time import at host startup unchanged (host boot still imports BACKLOG)
- [ ] dotnet build + test green
</acceptance-criteria>

<refs>
- @src/Overmind.Host/BacklogImportCommand.cs — the CLI entry
- @src/Overmind.Host/Program.cs — boot wiring + port file (see how host.port gets written to avoid it)
- @src/Overmind.Infrastructure/Planning/BacklogImporter.cs — the service to call
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-19 — Created from validated plan 016
- 2026-09-19 — Started: import CLI run-and-exit + --report (coder dispatched)
- 2026-09-19 — Review PASS-WITH-NITS (audits/review-016-04.md), IMPORTANT (DI duplication) fixed via shared AddBacklogImportPipeline; re-smoke green; 735/735
</log>
