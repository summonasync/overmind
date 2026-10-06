---
feature: 126
task: 02
slug: readonly-resolver-no-create
status: done
review: light
created: 2026-10-06
brief: Adapter: consolidate the three private ResolveProjectIdAsync copies into one injected ProjectResolver with a create flag and per-door error nouns — behavior-neutral, no door flips in this slice.
---

# Task 02 — Adapter: one shared resolver (behavior-neutral consolidation)

<goal>
Replace the three near-identical private `ResolveProjectIdAsync` copies with one injected resolver helper carrying an explicit create parameter. This slice is behavior-neutral: every door still resolves exactly as today (create=true, cwd-only body) — the flips are task 03.
</goal>

<scope>
- New `src/Overmind.Mcp/ProjectResolver.cs` — helper over `OvermindApiClient`: `Task<Guid> ResolveAsync(Guid? projectId, bool create, string errorNoun, CancellationToken ct)`. Order: argument → `OVERMIND_PROJECT_ID` env override → onboard POST (`{ cwd }` when create; the create=false shape lands in task 03). Failure wrap keeps today's catch set and message shape.
- `src/Overmind.Mcp/Program.cs` — register the resolver singleton next to the tool singletons (~:96–105); tool classes receive it via constructor.
- `src/Overmind.Mcp/OvermindTools.cs` (:280), `src/Overmind.Mcp/FeatureCodeTools.cs` (:212), `src/Overmind.Mcp/OvermindNotificationTools.cs` (:76) — delete the private copies; call sites route through the shared resolver with create=true and their existing error noun ("for the entry" / "for the working directory" / "for the notification").
</scope>

<anti-scope>
- NO create=false anywhere in this slice — no door flips, no description edits, no contract-test changes.
- `delete_entry` and `overmind_notify` keep resolving exactly as today.
</anti-scope>

<constraints>
- Per-door error nouns preserved — existing failure messages and any pins on them survive.
- If the copies turn out NOT to be byte-identical in their catch sets or messages (plan risk note), stop and record the difference in `<obstacles>` instead of silently unifying it.
- English-only, no comments except non-obvious decisions.
</constraints>

<acceptance-criteria>
- [x] One shared resolver serves all five cwd-resolving doors; the three private copies are gone.
- [x] `dotnet build` green.
- [ ] Full `Overmind.Mcp.Tests` green with NO test edits in this slice — proves consolidation neutrality.
</acceptance-criteria>

<refs>
- Spec: 29b2c8fe-bcb2-4ca8-904c-371a8fbc1288 (scope item 2, error-noun constraint).
- Plan: 3e6b2cec-1e6b-4868-a844-a25367481f71, slice 02.
- Copy locations: src/Overmind.Mcp/OvermindTools.cs:280, src/Overmind.Mcp/FeatureCodeTools.cs:212, src/Overmind.Mcp/OvermindNotificationTools.cs:76; DI: src/Overmind.Mcp/Program.cs:96-105.
</refs>

<obstacles>
- Plan risk: the three copies were verified near-identical, not byte-identical — differences go to obstacles, not silent unification.
- Plan risk resolved: the differences were non-behavioral — catch-set type ORDER (`is A or B or C` is order-insensitive) and literal vs const access to the same OVERMIND_PROJECT_ID value. Per-door nouns ("for the entry" / "for the working directory" / "for the notification") preserved verbatim; the shared tail message was already byte-identical across all three copies.
- AC-3 deviation: FeatureCodeToolContractTests.Tool() constructs FeatureCodeTools directly, so the new constructor dependency forced a one-line mechanical test edit — new(_api, git, new ProjectResolver(_api)). No contract pin touched: tool descriptions, error messages, routes and assertions are unchanged; neutrality is evidenced by the full serial run (513/513 passed).
- AC-3 left unticked by the letter of "NO test edits"; the edit is DI wiring only. Reviewer call: accept as satisfying the intent (proves consolidation neutrality) or require a follow-up.
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan slice 02; blockedBy: none (independent of 01).
- 2026-10-06 [agent] — 126/02 coding round started
- 2026-10-06 [agent] — Coding round complete — ProjectResolver.ResolveAsync(projectId, create, errorNoun, ct) serves all five doors (delete_entry, read_project_manifest, planning_feature_code, overmind_notify, overmind_replies); three private copies deleted. Copy deltas were non-behavioral (catch order, literal vs const) — per-door nouns verbatim. Gates: dotnet build 0 warn/0 err; Mcp.Tests -m:1 → 513/513. One-line DI test edit documented in obstacles; AC-3 left unticked for reviewer call. Commit d39cb54.
- 2026-10-06 [agent] — coding round complete — light review gate
- 2026-10-06 [agent] — AC-3 ticked per light-review verdict (tick-worthy): the test edit is one mechanical DI-wiring line (FeatureCodeToolContractTests.cs:207), zero assertion/pin changes — the anti-scope ban targets contract weakening, not constructor arithmetic. Obstacle note preserved.
- 2026-10-06 [agent] — Correction by main session: un-ticking AC-3 — its letter says «NO test edits in this slice» and the slice carries one mechanical DI-wiring edit, so the letter is not met even though the light review judged the edit tick-worthy (intent). The honest state: unticked AC + obstacle note + review verdict in the log. The wave result carries this as a recorded deviation.
- 2026-10-06 [agent] — light review APPROVED — 0 blockers, 2 SUGGESTIONs (inert create param consumed by 126/03 — don't let it dangle; errorNoun preposition-in-string → ledger). Neutrality verified across all 5 doors (nouns/tail/resolution-order byte-preserved, catch-order claim valid), DI singleton correct, AC-3 left unticked per the letter (one DI-wiring test edit — deviation recorded, review verdict tick-worthy on intent). Spot-check 6/6 + build. Commit d39cb54.
</log>