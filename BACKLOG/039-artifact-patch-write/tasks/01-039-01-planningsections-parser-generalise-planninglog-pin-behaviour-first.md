---
feature: 039
task: 01
slug: artifact-patch-write
status: done
review: light
created: 2026-09-22
brief: Pin PlanningLog behaviour with characterization tests, then extract PlanningSections (find/replace/list + AC parse) and rebase PlanningLog on it
---

# 039/01 — PlanningSections parser (generalise PlanningLog, pin behaviour first)

<goal>
A shared `PlanningSections` parser in Core that finds, replaces and lists anchored `<name>...</name>` sections in artifact bodies, with `PlanningLog` rebased onto it. The current `PlanningLog` behaviour is pinned by characterization tests BEFORE any refactor, so the extraction provably changes nothing for its three live consumers.
</goal>

<context>
`PlanningLog` (src/Overmind.Core/PlanningLog.cs) holds the anchored-pair logic today: `FindLastAnchoredPair` picks the last `<name>` open tag with a matching close tag after it, both on their own lines. Its consumers are `PlanningArtifact.UpdateBody`/`SetStatus`/`AppendLog` and the service `AppendLogAsync` — all live paths. Spec D3 generalises this into `PlanningSections` so task 02 can patch any section, not just `<log>`.

TDD applies: characterization tests first (red is not expected — they document current behaviour), then the extraction, then the new parser suite.

Executor reads ONLY this file.
</context>

<scope>
- Characterization tests for the CURRENT `PlanningLog`: `Append` inserts before `</log>` of the last anchored pair; creates a trailing `<log>` block when absent (with the exact separator logic: no trailing newline → `\n\n`, non-empty body → `\n`, empty body → none); `Parse` reads dated and undated `- ` lines, skips non-bullet lines, takes the LAST anchored pair only.
- New `src/Overmind.Core/PlanningSections.cs`: `Find(name, body)` / `TryFind`, `Replace(name, body, content)` (errors or returns null when the section is absent — no silent creation), `List(body)` returning section names in order; same anchored-pair semantics as today.
- `ParseAcceptanceCriteria(body)`: read `<acceptance-criteria>` content as a list of items with their raw text, index, checkbox state — this feeds task 02's tick.
- Rebase `PlanningLog.Append`/`Parse` on `PlanningSections` internals; public surface of `PlanningLog` unchanged.
- Parser suite per spec R3: single section, missing section, duplicate names (last anchored pair wins), content containing blank lines, CRLF bodies, indented tags (leading spaces/tabs per the existing regex tolerance).
- Fixtures: at least three real bodies copied from existing `BACKLOG/*/tasks/*.md` files (with their `<log>`, `<acceptance-criteria>`, nested inline code) — not synthetic minimums.
</scope>

<anti-scope>
- No `PatchSection`/`TickAc` on the entity, no service changes — task 02.
- No API/MCP surface — task 03.
- No changes to `PlanningLog`'s public contract or its callers.
- No persistence/migration work.
</anti-scope>

<constraints>
- .NET 10 / C# 14; xUnit + Shouldly; no new packages.
- Keep the `[GeneratedRegex]` pattern style already used in `PlanningLog`.
- No comments in code except non-obvious decisions (house rule).
</constraints>

<acceptance-criteria>
- `dotnet build` and `dotnet test` green across the solution.
- Characterization tests written before the refactor and unchanged by it.
- New parser suite covers every R3 case plus the real-file fixtures.
- `PlanningLog` behaviour identical (pinned by tests, not by assertion in prose).
</acceptance-criteria>

<refs>
- Spec 039 D3, R3, R4 (AC parsing shape).
- src/Overmind.Core/PlanningLog.cs — the logic being generalised.
- src/Overmind.Core/Entities/PlanningArtifact.cs:131-209 — consumers.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-22 — Execution starts: coder sub-agent takes the characterization-first parser contract (user mandate "стартуем 039", plan gate passed).
- 2026-09-22 — Coded (dotnet-coder): PlanningSections.cs new (strict Replace, AC parse), PlanningLog rebased with public contract untouched; 7 characterization pins + 31 parser tests, 3 real fixtures; build 0W/0E, tests 1029/1029. One deliberate deviation: CRLF tolerance (\r? patterns) — Append on CRLF bodies now inserts inside the existing pair (pinned). Off to light review.
- 2026-09-22 — Light review PASS-WITH-NITS (0 BLOCKER / 0 IMPORTANT / 4 NIT), gates re-verified independently: build 0W/0E, tests 1029/1029. Report: audits/review-039-01.md.
- 2026-09-22 — Accepted with the 4 NITs on record; NIT-2 (OpenEnd arithmetic) carried into 039/02's task log for the executor. Parser is the stable base for the domain work.
</log>
