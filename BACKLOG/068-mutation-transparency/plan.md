---
feature: 068
slug: mutation-transparency
status: validated
created: 2026-09-26
validated: 2026-09-26
---

# Plan 068 — Mutation transparency: маркеры на всех путях записи, statusChanged в ответах, current_task различает result-состояния (OM-11/12/13)

<goal>
Ship R1–R7 of the validated spec as four sequential tasks: markers → echo/term → feature demote → honest current_task. Each task lands green (build + tests) and pinned by contract tests before the next one starts.
</goal>

<approach>
- Marker plumbing rides the existing 043 channel: PlanningLog.Append/Parse already carry PlanningLogSource with legacy tolerance — no format change, only call-site routing (Core entity signature + Infrastructure service doors + MCP adapter picks Agent).
- statusChanged rides the compact echo added by 037/039 responses — a new optional field, not a new response shape.
- The feature demote (D1) lives in the same Infrastructure mutation branch that demotes the artifact (UpdateAsync), mirroring TransitionOnArtifactCreate's 023/06 pattern (same-SaveChanges, [auto] line, done never resurrects).
- current_task work stays in its physical home (Host/Api AgentPlanningApi.Current — per 030/04 finding it lives there, not in Infrastructure); result states, held state and warnings are pure resolver logic + text, no new endpoints. The overmind:// current resource mirrors the same reasons (PlanningResourceContractTests pins them).
- Term unification (D2) is a machine-surface sweep: tool descriptions, 409 detail, quickstart/bootstrap lines — enforced by a grep gate test.
</approach>

<task-breakdown>
1. (R1) Log markers on every write path — Core: PlanningArtifact.AppendLog gains source; Infrastructure: AppendLogAsync routes door source (MCP→Agent, HTTP→Ui); patch logNote and upsert logNote lines stamped by door. Files: src/Overmind.Core/Entities/PlanningArtifact.cs, src/Overmind.Infrastructure/Planning/PlanningArtifactService.cs, src/Overmind.Mcp (append_log/patch/upsert tools), HTTP artifacts API. Review: light.
2. (R2+D2) statusChanged echo + single term — response carries status/statusChanged on demotion with [auto] log line; "frozen" swept from tool texts/errors/quickstart (409 ChangeNoteRequired says validated); grep-gate test. Files: PlanningArtifactService.cs, response DTOs, Mcp tool descriptions, SessionBootstrapPrompt, tests (StatusModelContractTests + new grep gate). Review: light.
3. (R3+D1) Feature demote on spec/plan invalidation — review feature whose spec/plan drops below validated → inProgress in the same SaveChanges; [auto] line; feature status surfaced in the artifact response; quickstart states the invariant (review ⟺ spec+plan validated + all tasks done); done untouched. Files: PlanningArtifactService.cs, quickstart text, PlanningServiceTests. Review: hard (lifecycle semantics).
4. (R4+R5+R6) Honest current_task — three distinct result states; resolver picks among todo/inProgress only; state "held" with naming reason when only review/blocked remain; soft warnings ["spec missing","plan not validated"] on task payloads; resource + tool texts updated. Files: Host/Api AgentPlanningApi.cs, Mcp current-task tool + resource texts, AgentPlanningApiTests, PlanningResourceContractTests, Canned. Review: light.
</task-breakdown>

<verification>
- Sequential gates: dotnet build + dotnet test green after each task; reviewer run per task review label; architect-review (hard) closes the feature per Complex-task rule.
- Contract pins: MCP response shapes in Canned/McpTests; API behaviors in AgentPlanningApiTests; PlanningLog source round-trips in Core tests; grep gate proves no "frozen" in machine strings.
- End-to-end dogfood: patch the validated spec of a review feature through MCP and observe statusChanged + feature demote + [auto] line in one flow.
</verification>

<refs>
- Spec 9f1fa31e (validated) — requirements R1–R7, decisions D1–D3.
- 043 marker channel; 023/06 demote pattern; 030/04 resolver-location finding; 037/039 echo shape.
</refs>

<log>
- 2026-09-26 — plan drafted after spec validation; resolver location pinned to Host/Api per 030/04.
</log>