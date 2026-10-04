---
feature: 103
task: 01
slug: ac-tick-ux
status: done
review: light
created: 2026-10-04
brief: Core: TickAc 1-based, UntickAc op, FormatAcItems 1-based, tick/untick echo, matrix tests
---

# Task 01 — Core + service: 1-based ticks + un-tick + echo

<goal>
Move AC item indexing to 1-based in the domain and add the un-tick operation with a tick echo.
</goal>

<scope>
- TickAc: index semantics 1-based (text path unchanged).
- New UntickAc: by acText (primary) or 1-based acIndex; clears [x]→[ ]; already-unticked → no-op (honest «already unticked»); markerless item → TickItemNotCheckable.
- FormatAcItems renders 1-based indices (400 diagnostics).
- Patch result carries tickedIndex + tickedText echo (both tick and untick).
- Matrix tests: tick by 1-based index/text, retick no-op, untick all paths, markerless 400, out-of-range 400 with 1-based listing.
</scope>

<anti-scope>
- Doors and descriptions (task 02); parser (105).
</anti-scope>

<acceptance-criteria>
- [x] TickAc resolves index 1 as the FIRST item; out-of-range 400 lists items 1-based.
- [x] UntickAc: text, index, already-unticked no-op, markerless 400 — pinned.
- [x] Patch echo carries tickedIndex+tickedText for tick and untick.
- [x] Full matrix green.
</acceptance-criteria>

<refs>
- Spec 3f42d84f; plan 4a117b99 (task 1).
</refs>

<log>
- 2026-10-04 [agent] — [agent] Claimed by Van (claude), taken by orchestrator — dispatching dotnet-coder (sonnet).
- 2026-10-04 [agent] — [agent] Core+service landed (c16de1a): TickAc 1-based, UntickAc with echo, FormatAcItems 1-based, 301+615 green; 4 red door tests are the expected task-02 worklist (0-based pins to rewrite, spec-sanctioned). Review dispatching in parallel with task 02 (disjoint files). All 4 AC confirmed by code inspection pending light review.
- 2026-10-04 [agent] — [orchestrator] Light review PASS-WITH-NITS (0 blockers, commit c16de1a): build 0/0, Core 301/301, Infra 615/615 confirmed; the 4 red Api tests classified as exactly the expected 0-based door worklist of task 02 (plus 2 pre-existing environment-dependent fails, ledgered #187). 1-based coherence sweep, UntickAc paths, echo semantics and compile-compat all confirmed. Nits → microbatch #185 (ambiguous message op-neutral wording) / #186 (ResolveAcTarget -1 pirouette cosmetics).
- 2026-10-04 [agent] — [agent] Review passed (PASS-WITH-NITS, 0 blockers) — closing task 01; all 4 AC ticked, code in c16de1a. Task 02 (doors) continues in parallel per plan; result stays draft per pipeline mandate.
</log>
