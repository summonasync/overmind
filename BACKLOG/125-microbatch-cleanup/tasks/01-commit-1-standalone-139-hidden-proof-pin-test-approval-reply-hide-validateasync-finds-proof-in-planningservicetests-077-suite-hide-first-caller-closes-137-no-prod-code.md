---
feature: 125
task: 01
slug: microbatch-cleanup
status: done
review: light
created: 2026-10-06
brief: Commit 1 (standalone): #139 hidden-proof pin — test approval→reply→Hide()→ValidateAsync finds proof in PlanningServiceTests 077 suite; Hide() first caller closes #137. No prod code.
---

<goal>
Commit 1 of the wave (standalone, lands first): pin the «hidden reply is still a valid proof» invariant on the artifact validation-proof path so any future `HiddenAt == null` filter there fails loudly. Hide() gains its first caller, transitively closing #137.
</goal>

<scope>
- File: `tests/Overmind.Infrastructure.Tests/PlanningServiceTests.cs` — the 077 validation-proof suite (`ValidateAsync_WithRepliedApproval_ValidationCarriesProofLine` and siblings, ~:1736).
- Add the hidden sibling test: approval notification → reply → `Hide()` on the notification → `ValidateAsync(spec.Id, source: PlanningLogSource.Agent, requireHumanProof: true)` must still succeed and carry the proof line (mirror the :1756 assertion shape). Suggested name: `ValidateAsync_WithHiddenRepliedApproval_StillFindsProofLine`.
- Local sanity check (NOT committed): temporarily add a `HiddenAt == null` filter to the proof predicate in `PlanningArtifactService`, watch the new test fail, revert. Report the result in the task log.
- Commit: standalone — nothing else in the diff. Conventional English message, e.g. `test(125): pin hidden-reply-still-valid-proof invariant on artifact validation (closes #137)`.
</scope>

<anti-scope>
- No production code changes in this commit (the sanity filter is temporary and reverted).
- No additional Hide() callers beyond the test; #137 closes via the test caller.
- No other wave items ride in this commit.
</anti-scope>

<constraints>
- First task of the wave; T2–T4 wait for this commit to land.
- `dotnet test` scoped to the affected class (solution-wide parallel runs OOM-flake on this host, infra note #158); full-suite run happens at wave close.
- Ledger tombstones: immediately after the commit lands, mark #139 and #137 in `/home/johan-boolman/.claude/projects/-mnt-Ubuntu-Dev-Projects-AI-Tools-Overmind/memory/overmind-microbatch-queue.md` as `[CLOSED 2026-10-06, коммит <hash> — <what landed>]`, in the same work-step, never batched later. The file lives outside the repo — editing it is not a git operation.
- No `git push`. English-only code and messages. Load `dotnet-best-practices` with this task.
</constraints>

<acceptance-criteria>
- [x] The hidden-proof test exists in the 077 suite and passes.
- [x] Sanity check performed: temporary HiddenAt filter turns the test red; the committed diff contains no production changes.
- [x] Hide() has its first caller; ledger #137 closed.
- [x] The commit is standalone (only the test file) and is the wave's first commit.
- [x] Ledger #139 and #137 tombstoned with the commit hash in the same work-step.
- [x] `dotnet test --filter PlanningServiceTests` green.
</acceptance-criteria>

<refs>
- Spec: artifact cdb6854b-01ac-410c-82fc-8f13e706cb7d (feature 125-microbatch-cleanup), scope item #139.
- Plan: artifact e3cbeb50-ac62-4d36-8fc8-d4cbb379dd3d, Commit 1.
- Ledger: `/home/johan-boolman/.claude/projects/-mnt-Ubuntu-Dev-Projects-AI-Tools-Overmind/memory/overmind-microbatch-queue.md` — items #139, #137.
- Source: `audits/architect-review-2026-09-29-097.md`.
</refs>

<obstacles>
- Ledger line numbers for the proof path drifted between #139 (:422–433) and #239 (:441–450) — locate the predicate by symbol (approval-reply proof search inside `PlanningArtifactService.ValidateAsync`), not by line.
- The ledger memory file lives outside the git repo; tombstones are plain file edits done right after `git commit` returns the hash.
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan (Commit 1); self-contained execution contract; obstacles pre-seeded so first-quirk patches work (empty-section patch is a known 400).
- 2026-10-06 [agent] — T1 coding round started
- 2026-10-06 [agent] — Sanity check confirmed: temporary `&& n.HiddenAt == null` on the proof predicate (BuildHumanProofLineAsync) turned the new test red (ValidationProofRequired); filter reverted, committed diff contains only PlanningServiceTests.cs. Gates: dotnet build 0w/0e; PlanningServiceTests 127/127; full serial runs Core 365, Api 661, Mcp 494, Infrastructure 690 — 2337 passed, 0 failed, no #158 OOM flake. Commit 86e8eed; ledger #139/#137 tombstoned.
- 2026-10-06 [agent] — coding round complete — light review gate
- 2026-10-06 [agent] — light review APPROVED — 0 blockers, 1 non-blocking SUGGESTION (ledger tombstone #137 wording; fixed by main session). Commit 86e8eed, spot-check 127/127.
</log>