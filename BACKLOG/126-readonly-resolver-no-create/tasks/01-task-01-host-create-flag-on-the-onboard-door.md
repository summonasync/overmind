---
feature: 126
task: 01
slug: readonly-resolver-no-create
status: done
review: light
created: 2026-10-06
brief: Host: optional create flag (default true) on POST /api/onboard — create=false + unregistered cwd returns 404 guidance and inserts nothing; 3 test pins in OnboardApiTests including count-before==after.
---

# Task 01 — Host: create flag on the onboard door

<goal>
Give POST /api/onboard explicit create-free semantics: an optional `create` flag defaulting to true; when `create=false`, no projectId was given and no DiskPath match exists, the door returns 404 with registration guidance and inserts nothing. Every other combination stays byte-for-byte today's behavior.
</goal>

<scope>
- `src/Overmind.Host/Api/Contracts/OnboardContracts.cs` — `OnboardRequest(string Cwd, Guid? ProjectId, bool Create = true)`.
- `src/Overmind.Host/Api/OnboardApi.cs` — in the no-projectId branch: `!request.Create` + no DiskPath match → `HttpErrors.NotFound` with guidance naming `overmind_onboard` and an explicit `projectId` ("No project is registered for the working directory '…'. Call overmind_onboard to register it, or pass projectId explicitly."). No insert. The match path and the explicit-projectId path unchanged; `created` stays false on matches.
- `src/Overmind.Host/Api/Validation/OnboardValidators.cs` — verify no rule is needed for the flag; leave alone.
- Tests in `tests/Overmind.Api.Tests/OnboardApiTests.cs`, three additions:
  1. `create=false` + unregistered cwd → 404, detail contains "overmind_onboard" and "projectId", projects count before == count after.
  2. `create=false` + registered cwd → 200, existing project, `created=false`, response shape unchanged.
  3. Body without `create` + unregistered cwd → still creates (default-true regression guard).
</scope>

<anti-scope>
- No adapter files — the adapter consumes the flag in task 03.
- No behavior change on the create=true path; existing OnboardApiTests must pass untouched.
- No new resolve endpoint, no schema/migration, no claim/lock semantics.
</anti-scope>

<constraints>
- The 404 guidance text is authored here and becomes the single source surfaced by the adapter verbatim (spec constraint).
- English-only, no comments except non-obvious decisions, no `region`.
- `dotnet build` green after edits.
</constraints>

<acceptance-criteria>
- [x] `create=false` + unregistered cwd → 404 with guidance containing "overmind_onboard" and "projectId"; projects count unchanged (count before == count after).
- [x] `create=false` + registered cwd → 200 with the existing project and `created=false`.
- [x] Omitted `create` + unregistered cwd → still creates; all pre-existing OnboardApiTests pass without edits.
- [x] `dotnet build` + `dotnet test tests/Overmind.Api.Tests` green.
</acceptance-criteria>

<refs>
- Spec: 29b2c8fe-bcb2-4ca8-904c-371a8fbc1288 (scope item 1, constraints, AC 1–3).
- Plan: 3e6b2cec-1e6b-4868-a844-a25367481f71, slice 01.
- Upsert branch today: src/Overmind.Host/Api/OnboardApi.cs (~:41–78); contract: src/Overmind.Host/Api/Contracts/OnboardContracts.cs.
</refs>

<obstacles>
- None known. If the ApiFactory test base makes row-count assertions awkward, record the workaround here instead of loosening the pin.
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan slice 01; blockedBy: none (entry slice).
- 2026-10-06 [agent] — 126/01 coding round started
- 2026-10-06 [agent] — coding round complete, commit af6d052 (build 0/0, Api.Tests 677/677 serial). Guidance detail text verbatim (source for task 03): "No project is registered for the working directory '{normalizedCwd}'. Call overmind_onboard to register it, or pass projectId explicitly." — normalized cwd is quoted, not the raw request value, since it is the canonical DiskPath match key. Validator untouched: bool flag needs no FluentValidation rule. Count pin rides GET /api/projects (length before == after); no ApiFactory workaround needed.
- 2026-10-06 [agent] — coding round complete — light review gate
- 2026-10-06 [agent] — light review APPROVED — 0 blockers, 1 SUGGESTION (S-01 pin strength: ShouldContain(cwd) can't distinguish raw-vs-normalized quoting on Linux — production correctly quotes normalizedCwd; microbatch candidate, next ledger number at append time). Pure-insertion diff, miss-path returns before any SaveChanges, mutation-checked pins incl. real count-before==after via GET /api/projects, spot-check 677/677 serial. Commit af6d052.
</log>