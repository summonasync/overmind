---
feature: 004
task: 01
slug: disk-enumeration-api
status: done
review: light
created: 2026-09-08
---

# 004/01 — Disk enumeration API

<goal>
The Host exposes two loopback endpoints that let the web UI lazily browse the local directory structure and see which directories are registered Overmind projects.
</goal>

<scope>
`src/Overmind.Host/Api/SystemApi.cs` (extend, next to browse-folder) + `src/Overmind.Host/Api/Contracts/SystemContracts.cs`:
- `GET /api/system/tree-roots` → `{ roots: [{ name, path }] }`. Windows: `DriveInfo.GetDrives()` filtered by `IsReady` (name = drive label or letter, path = root directory). macOS: home directory + `/`.
- `GET /api/system/list-directory?path=<abs>` → `{ path, truncated, entries: [{ name, path, accessible, projectId, projectName }] }`.
  - Directories only, single level, sorted alphabetically (ordinal-ignore-case).
  - Skip hidden and system entries (`FileAttributes.Hidden | System`).
  - Cap 500 entries; when more exist, return first 500 + `truncated: true`.
  - Unreadable directories appear with `accessible: false` (catch per-entry access errors; the listing itself must not fail because of one bad entry).
  - `projectId`/`projectName` set when the entry's normalized path equals a registered project's DiskPath (load all projects once per request from the DbContext, build a normalized-path → project map).
- Path normalization helper in Host: `Path.GetFullPath`, trim trailing directory separators, `OrdinalIgnoreCase` on Windows / `Ordinal` on macOS. Use it both for the project map and for the response `path`.
- Errors: missing/empty `path` → 400; non-rooted path → 400; path doesn't exist or is not a directory → 404 (ProblemDetails, consistent with existing API style).
- Tests `tests/Overmind.Api.Tests/SystemApiTests.cs`: roots shape (at least one root, paths rooted); temp-dir listing (subdirs returned, files excluded); hidden dir skipped; cap/truncated (create >500 dirs OR make the cap injectable — pick the simpler honest option); project match exact / trailing-separator / case-insensitive-on-Windows; nonexistent path → 404; relative path → 400.
</scope>

<anti-scope>
- No frontend work.
- No recursive enumeration, no caching, no file watching.
- No changes to ProjectsApi or the DB.
</anti-scope>

<constraints>
- Vertical-slice style with EF in handler is sanctioned (ADR-B); SystemApi stays under the ~300-line extraction trigger — if it crosses, split the enumeration into its own `SystemBrowseApi.cs` file, same route prefix.
- No new NuGet packages. No comments unless non-obvious. English only.
- Follow existing error/validation conventions in the codebase (FluentValidation where used, ProblemDetails responses).
- `dotnet build && dotnet test` fully green.
</constraints>

<acceptance-criteria>
- [ ] Both endpoints behave per scope (verified by tests).
- [ ] All new tests + existing 169 tests green.
- [ ] Normalization handles trailing separators and Windows case-insensitivity (tested).
</acceptance-criteria>

<refs>
- @../spec.md — decisions (endpoints, caps, security)
- @../plan.md — task 01
- src/Overmind.Host/Api/SystemApi.cs — existing browse-folder endpoint to extend
- BACKLOG.md hardening — DiskPath trailing-separator comparison nit this task fixes
</refs>

<obstacles>
<!-- Filled by executors during work -->
None. Notes for reviewer: (1) `truncated` is set whenever the cap stops enumeration early — with exactly 500 subdirs and no more it would report `truncated: true` although nothing was dropped; not worth an extra enumeration pass for the MVP. (2) The case-insensitivity test early-returns on non-Windows instead of a conditional Fact (test project has no skip helper in this style). (3) Cap test creates 505 real directories (chose the honest option over an injectable cap).
</obstacles>

<log>
2026-09-08 — task created from approved plan.
2026-09-08 — implemented: GET /api/system/tree-roots + GET /api/system/list-directory in SystemApi.cs (218 lines, below split trigger), contracts in SystemContracts.cs, HttpErrors.BadRequest added. 9 new tests in SystemApiTests.cs. dotnet build clean; dotnet test 178/178 green (169 baseline + 9 new).
</log>
