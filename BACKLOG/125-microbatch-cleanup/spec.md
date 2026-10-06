---
feature: 125
slug: microbatch-cleanup
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Spec 125 — Microbatch cleanup wave: close the accumulated review-findings ledger as one batch

<goal>
Close one groomed slice of the accumulated microbatch ledger (crew memory `overmind-microbatch-queue`, ~243 items) as a single batch wave of small, focused fixes: the priority invariant #139, the fresh-wave items enumerated in the feature body, and a bounded set of top-legacy items pulled at the coder's judgment. No behavior change beyond the pinned fixes. Every ledger item the wave closes is tombstoned with its commit's hash in the same commit, so the ledger and the tree never disagree.
</goal>

<scope>
**Priority item (lands first, own commit):**
- **#139 (priority; transitively closes #137).** Pin the «hidden reply is still a valid proof» invariant on the artifact validation-proof path. The proof search in `PlanningArtifactService` (~:422–433; the same approval-reply predicate is duplicated at :441–450 per #239) has no test with a hidden notification row — a future innocent `HiddenAt == null` filter would pass the entire suite and silently kill 077 validation for every project with hidden proofs. Fix: one test in the 077 validation-proof suite — approval → reply → `Hide()` → `ValidateAsync` still finds the proof row. `Hide()` gains its first caller, dropping #137's dead-code candidate.

**Fresh wave (enumerated in the feature body):**
- **#232 — number-prefix trim desync.** FluentValidation rejects `" OM "` (400) where the entity would accept the trimmed value; endpoint trim is alive only for trailing `\n`. The layers disagree on what an untrimmed-but-valid prefix is. Fix: trim in the validator + pin test for the previously-400 form.
- **#241 — status-parse ×3.** The issues status-parsing block is duplicated across the three issues list endpoints. Fix: extract one shared helper; behavior unchanged.
- **#242 — dead client field.** `CrossProjectIssueRow.projectName` is unused by the web (ProjectNameBadge resolves names itself via `useProjectName`). Fix: drop the field from the Host DTO/projection and the web type; update affected wire-shape pins.
- **#243 — attention prime gate.** `useAttentionSurfaces`: a failed first sync followed by a successful hidden refetch leaves a stale prime. Fix: gate the prime on `isPending || error`.
- **#195(a) — stale approveError across tabs.** `validateArtifact.error` is not reset on tab change (`feature-workspace.tsx`), so a spec failure lights the banner on the plan panel. Fix: reset in `setTab` (~5 lines). Only (a); the (b) race is out of scope.
- **#193 — decline double-load.** The decline path loads the artifact twice (`PlanningArtifactService`: Kind-check load + second load inside `AppendLogAsync`). Fix: reuse the already-loaded entity — but only if it does not complicate the service seam (ledger-noted trade-off); a documented skip in `<obstacles>` is an accepted outcome for this item.

**Top legacy (coder's judgment within one wave's budget; anything outgrowing a one-commit fix goes back to the ledger):**
- **#127 — required-arg MCP error text.** A planning_* tool call missing a required argument returns the bland wrapper error with neither the argument name nor contract text (field case: `planning_task_queue` / `planning_list_features` without projectId). Fix: explicit required-arg checks in the adapter with contract text naming the argument («projectId is required …»), following the existing `ContractViolation` pattern (`QueueMoveContractError` precedent). Pin test for the field case.
- **#159 — kind-uniqueness diagnostics for S/P/R aliases.** `ResolveRosterMatch` silently takes the first match when duplicate S/P/R documents exist (spec@0 + spec@1 is data-legal; the write path then misses its target). Fix direction pinned by this spec: multi-match diagnostics (explicit error naming the candidates) — NOT a schema-level uniqueness constraint.
- **#52 — warning-count denominator.** The denominator `items.Count` includes marked-without-checkbox lines (`Checked == null`); it must count only `i.Checked.HasValue`. Denominator correction only — zero effect on real bodies.
- **#53 — warning pins.** Pin tests: pollution case (all-ticked body + logNote containing `[ ]` → no warnings) and a warning test at the human door (shim is identical; the pin is the point). Test-only.
</scope>

<anti-scope>
- **#240 (unbounded /api/issues pagination)** — dropped at groom: the ledger item prescribes no fix («consistent with 028, volumes small; pagination when the board grows» is a growth-trigger note, not a defect). Stays in the ledger; reopen when the board-size trigger fires.
- **#233–#239** — inside the ledger's 232–243 window but not enumerated in the feature body. Remain in the ledger for a future wave; do NOT pull them as ride-alongs.
- **DesktopPermission barrel** — the body lists it as «(done)»: closed upstream with 095. Provenance only, no action.
- **User-gate track** — ADRs #218/#230 and design call #221 (already closed upstream with user approval, commit 9e61dc4; kept here for provenance), plus any new ADR/palette/policy work.
- **#195(b)** — the decline/validate double-click race: harmless, log-only, not this wave.
- Anything exceeding a one-commit fix: back to the ledger or its own feature. No refactors beyond the listed items — in particular no `ArtifactAliasResolver` extraction (#165a) even though #127/#159 touch the same adapter area.
</anti-scope>

<constraints>
- **#139 lands first, standalone** — the priority invariant rides in its own commit with nothing else in it.
- The wave is a **small set of focused commits** (per-item or per-area: Host/Core, adapter/MCP, web), never one mega-commit. Commits are work-preservation/landing checkpoints per the standing commit policy; `git push` stays human-only.
- **Ledger tombstones ride the same commit** that closes an item: in `overmind-microbatch-queue.md`, mark the item's line closed with the commit hash (ledger-native `[CLOSED YYYY-MM-DD, <hash> — …]` marker or `<removed …>` tombstone) inside the fix commit. Only the memory ledger file is edited for tombstones — board mirrors and other features' BACKLOG artifacts are not touched by this wave.
- **#52–53 keep their pin semantics:** #52 is a denominator correction only; #53 is test-only. Neither changes warning behavior on real bodies.
- **#193 escape hatch:** if the single-load fix complicates the service seam, skip it and record the decision in the task's obstacles — the ledger itself flagged the trade-off.
- **#127 scope bound:** required-arg checks for planning_* tools' required arguments, minimum the projectId-bearing reads from the field case — not an exhaustive re-audit of every tool parameter.
- Full suites green before close: `dotnet test` (solution) plus web `npm run lint`, `npx tsc --noEmit` and web tests.
- English-only code, messages and commits; conventional commit messages.
</constraints>

<acceptance-criteria>
- [ ] #139: a test in the 077 validation-proof suite — approval → reply → Hide() → ValidateAsync finds the proof row; it fails if a `HiddenAt == null` filter is introduced on the proof path. Hide() has its first caller (closes #137).
- [ ] #139 is committed first, standalone, with its ledger tombstone in the same commit.
- [ ] #232: the number-prefix validator trims its input; a pin covers the previously-400 untrimmed form; validator and entity agree.
- [ ] #241: one shared status-parse helper serves all three issues list endpoints; no behavior change.
- [ ] #242: `projectName` is gone from `CrossProjectIssueRow` (Host projection) and the web type; affected wire pins updated; web still resolves names via `useProjectName`.
- [ ] #243: the attention surfaces prime is gated on `isPending || error`; the failed-first-sync → hidden-refetch gap is covered.
- [ ] #195(a): changing tabs resets `validateArtifact.error`; a spec failure no longer renders on the plan panel.
- [ ] #193: the decline path issues a single artifact load, OR the skip is recorded in obstacles with the seam rationale.
- [ ] #127: planning_* calls with a missing required argument return an explicit contract-violation error naming the argument; pin test covers task_queue/list_features without projectId.
- [ ] #159: duplicate S/P/R documents produce explicit multi-match diagnostics instead of a silent first-match; no schema change.
- [ ] #52: the warning denominator counts only items with `Checked.HasValue`; behavior on real bodies unchanged.
- [ ] #53: the pollution pin (all-ticked + `[ ]` in logNote → no warnings) and the human-door warning pin exist and pass.
- [ ] Every ledger item closed by the wave carries its tombstone with the commit hash in the same commit as its fix.
- [ ] Full `dotnet test` green; web lint + `tsc --noEmit` + web tests green.
- [ ] No behavior change beyond the pinned fixes; the diff shows no ride-along edits (#233–#239 untouched).
</acceptance-criteria>

<refs>
- Ledger, source of all item details and the tombstone target: `/home/johan-boolman/.claude/projects/-mnt-Ubuntu-Dev-Projects-AI-Tools-Overmind/memory/overmind-microbatch-queue.md` — items #52–53, #127, #137, #139, #159, #193, #195(a), #232, #240–243.
- Materialized audits: `audits/architect-review-2026-09-29-097.md` (#139/#137), `audits/review-056-01.md` (#52–53), `audits/review-090-03.md` (#159), `audits/review-115-01.md` (#240/#241), `audits/review-122-02-web.md` (#195a).
- Reviews that exist only via the ledger (no materialized file): review-066-01 (#232), review-115-02 (#242), review-122-01 (#193), delta-review-095 (#243).
- Board features the items came from: 077 (validation proof), 115 (cross-project issues), 122 (artifact validate/decline), 095 (attention surfaces), 066 (numbering).
</refs>

<obstacles>
- Groom finding: the body's «Fresh wave #232–243» is a ledger-window label, not an item list — only the enumerated subset is scoped (#232, #241, #242, #243, #195(a), #193, plus #240 which was evaluated and dropped). #233–#239 stay in the ledger, recorded in anti-scope.
- #240 dropped at groom: no executable change exists behind the item (growth-trigger note). Flagged here rather than silently inherited from the body's range label.
- #193 carries a ledger-noted trade-off («the alternative would complicate the seam») — in scope with an explicit documented-skip escape hatch; a forced fix that ugly-fies the service is worse than the double load.
- #159's ledger offers two directions (backend kind-uniqueness vs multi-match diagnostics); the feature body picks diagnostics — schema-level uniqueness is deliberately out of this wave.
- Ledger line numbers for the proof path differ between #139 (:422–433) and #239 (:441–450) — locate by symbol, not by line.
- #137 closes transitively through #139 (first caller); do not hunt for additional Hide() callers.
</obstacles>

<log>
- 2026-10-06 [agent] — spec v1 drafted from the feature body + microbatch ledger groom; scope = #139 + fresh wave (#232/#241/#242/#243/#195a/#193) + legacy pull set (#127/#159/#52–53); #240 dropped to anti-scope (no executable fix), #233–#239 and done/user-gate items fenced off; status draft, awaiting human validation tap.
- 2026-10-06 [agent] — validated with human proof: notification 01b119c7-7a9c-493f-b144-d5a804f31ff8 replied 2026-10-06 11:13:35 UTC
</log>
