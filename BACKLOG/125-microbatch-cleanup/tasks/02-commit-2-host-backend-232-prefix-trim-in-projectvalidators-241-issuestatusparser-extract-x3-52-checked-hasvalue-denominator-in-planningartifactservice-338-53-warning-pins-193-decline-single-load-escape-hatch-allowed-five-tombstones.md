---
feature: 125
task: 02
slug: microbatch-cleanup
status: done
review: hard
created: 2026-10-06
brief: Commit 2 (Host/backend): #232 prefix trim in ProjectValidators, #241 IssueStatusParser extract x3, #52 Checked.HasValue denominator in PlanningArtifactService:338, #53 warning pins, #193 decline single-load (escape hatch allowed). Five tombstones.
---

<goal>
Commit 2 of the wave: Host/backend batch — #232 number-prefix trim, #241 status-parse extraction, #52 warning denominator, #53 warning pins, #193 decline single-load. Five ledger items, one reviewable commit.
</goal>

<scope>
- **#232:** `src/Overmind.Host/Api/Validation/ProjectValidators.cs` — trim the number-prefix input in the FluentValidation rule (066 numbering styles) so validator and entity agree on `" OM "`-style prefixes. Pin test next to the existing project-validator tests (tests/Overmind.Api.Tests): the previously-400 untrimmed form now passes.
- **#241:** extract the duplicated issues status-parsing block from `src/Overmind.Host/Api/IssuesApi.cs`, `src/Overmind.Host/Api/AgentIssuesApi.cs`, `src/Overmind.Host/Api/CrossProjectIssuesApi.cs` into one shared helper (new small file `src/Overmind.Host/Api/Contracts/IssueStatusParser.cs` or a co-located static class — coder's call). Endpoint contracts and behavior unchanged.
- **#52:** `src/Overmind.Infrastructure/Planning/PlanningArtifactService.cs:338` — the warning denominator must count only checkbox-bearing items: `items.Count(i => i.Checked.HasValue)` instead of `items.Count`. Numerator (`item.Checked == false` at :336) and warning text untouched; zero behavior change on real bodies.
- **#53:** two pin tests — (a) pollution: all-ticked acceptance items + logNote containing `[ ]` → no warnings (service level, PlanningServiceTests); (b) human door: artifact upsert via the API door surfaces the same warning (tests/Overmind.Api.Tests). Test-only item.
- **#193:** `src/Overmind.Infrastructure/Planning/PlanningArtifactService.cs` decline path (~:462 Kind-check load, ~:484 second load inside `AppendLogAsync`) — reuse the loaded entity for the log append instead of a second query, IF it does not complicate the service seam. If it does — skip and record the rationale in this task's obstacles (pre-approved escape hatch); the ledger line then stays open with a dated note instead of a tombstone.
- Commit: one focused commit, conventional English message. Tombstones for every landed item immediately after the commit.
</scope>

<anti-scope>
- No issues-endpoint contract changes (payload shapes stay byte-identical).
- No public-method additions to PlanningArtifactService (at its documented 7-method ceiling, STRUCTURE).
- No ride-along items: #233–#239 untouched; the #195(b) race stays out; no migrations, no schema changes.
</anti-scope>

<constraints>
- Strictly sequential: starts only after T1's commit landed.
- `dotnet test` scoped to affected classes; full-suite run at wave close (serialized, infra #158).
- Tombstone mechanics: `[CLOSED 2026-10-06, коммит <hash> — <what landed>]` in `/home/johan-boolman/.claude/projects/-mnt-Ubuntu-Dev-Projects-AI-Tools-Overmind/memory/overmind-microbatch-queue.md` right after the commit, same work-step.
- No `git push`. English-only. Load `dotnet-best-practices` — review is hard and cites BP-IDs.
</constraints>

<acceptance-criteria>
- [x] #232: validator trims; pin covers the previously-400 form; entity and validator agree.
- [x] #241: one shared parser serves all three issues list endpoints; no behavior change.
- [x] #52: denominator counts only `Checked.HasValue`; numerator and warning text unchanged.
- [x] #53: pollution pin and human-door warning pin exist and pass.
- [x] #193: single artifact load in the decline path, OR the skip is recorded in obstacles with the seam rationale and the ledger line notes it.
- [x] Ledger tombstones written for every landed item with this commit's hash.
- [x] `dotnet test` on the touched test classes green; issues-endpoint shapes unchanged.
</acceptance-criteria>

<refs>
- Spec: cdb6854b-01ac-410c-82fc-8f13e706cb7d — fresh-wave + #52–53 items.
- Plan: e3cbeb50-ac62-4d36-8fc8-d4cbb379dd3d — Commit 2.
- Ledger: `/home/johan-boolman/.claude/projects/-mnt-Ubuntu-Dev-Projects-AI-Tools-Overmind/memory/overmind-microbatch-queue.md` — #52, #53, #193, #232, #241.
- Sources: `audits/review-056-01.md` (#52–53); review-066-01 (#232) and review-122-01 (#193) exist only via the ledger.
</refs>

<obstacles>
- #193 escape hatch is pre-approved: the ledger itself notes the single-load alternative may complicate the seam. A documented skip here is a valid outcome — this obstacles section is non-empty, so planning_patch_artifact works on it (patching an EMPTY obstacles section is a known 400; fallback planning_append_log).
- Line refs :462/:484 and :336–338 may have drifted — locate by symbol (decline path; the unticked-count warning string).
- #193 implemented, not skipped: private AppendLogAsync(PlanningArtifact, ...) overload shared by the Guid-door and the decline path — single load, no duplicated save/dispatch sequence, public surface untouched (7-method ceiling holds). The ledger's seam concern does not materialize in this shape.
- #52 got its own service pin beyond the two #53 pins (markerless AC line excluded from the denominator, asserts "1 of 1") — the AC demands a verifiable counting change; judgment call, droppable if review objects.
- Ledger file drifted on disk between read and edit (main session rewrote #137 per briefing) — all five tombstone edits applied cleanly.
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan (Commit 2); review: hard (pin semantics + service-seam decision on #193).
- 2026-10-06 [agent] — T2 coding round started
- 2026-10-06 [agent] — coding round complete — hard review gate
- 2026-10-06 [agent] — hard review APPROVED — 0 blockers. Both judgment calls upheld: #193 private AppendLogAsync overload (honest seam, public surface unchanged 8→8); #52 extra pin kept — it is the only denominator-discriminating pin. W1 (STRUCTURE.md:67 ceiling drift) filed to microbatch ledger. Commit 2fa454c, spot-checks green.
</log>