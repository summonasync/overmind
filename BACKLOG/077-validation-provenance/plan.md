---
feature: 077
slug: validation-provenance
status: validated
created: 2026-09-26
validated: 2026-09-26
---

# Plan 077 — Validation provenance: planning_validate requires proof of human approval (token or explicit human path)

<goal>
Ship R1–R6 of the validated spec as two sequential tasks: the backend proof mechanism (search + 409 + sourced log line), then the surface work (MCP description, bootstrap, contract pins). Closes with the live notify → reply → validate dogfood.
</goal>

<approach>
- Proof search lives in PlanningArtifactService.ValidateAsync (single mutation path, both doors already flow through it with their door source from 068). The service already holds the DbContext — AgentNotification is a plain entity query (AsNoTracking read): kind=Approval, FeatureSlug == artifact's feature slug, ReplyText != null, RepliedAt >= artifact.UpdatedAt. Feature slug resolve follows the existing GetProjectIdAsync-style path (load the feature by artifact.FeatureId).
- Typed 409 rides the 039/078 PlanningFailureCode pattern: ValidationProofRequired + PlanningException factory + ProblemDetails mapping — no new HTTP machinery.
- The sourced log line closes microbatch #43 for ALL validations: human door writes "[ui] — validated via UI"; agent door writes "[agent] — validated with human proof: notification <id> replied <ts>"; asMigrated keeps its existing explicit logNote line (055).
- Gate-on precedence: the existing humanGate STOP fires before any proof logic (056 contract untouched).
</approach>

<task-breakdown>
1. (R1–R4, R6-api) Backend proof mechanism — Core: PlanningFailureCode.ValidationProofRequired + PlanningException factory (append-at-end, 034 precedent); Infrastructure: proof search in ValidateAsync + mandatory sourced log line on every validation path + typed 409; Api tests: proof-found green, no-notification 409 verbatim, stale artifact (updated after RepliedAt) 409, wrong-feature notification 409, question-kind reply does not count, human door free without notification, gate-on STOP precedence over proof logic, log line content pinned. Files: src/Overmind.Core (PlanningErrors/PlanningException), src/Overmind.Infrastructure/Planning/PlanningArtifactService.cs, tests/Overmind.Api.Tests + Infrastructure.Tests. Review: light.
2. (R5, R6-mcp) Surface: MCP planning_validate description documents the proof mechanism + 409 (CallToolResult passthrough precedent from set_current), session bootstrap line next to the existing validation text, Canned 409 fixture, Mcp contract pins (description text, 409 surfacing). Files: src/Overmind.Mcp/OvermindPlanningTools.cs, bootstrap prompt file (locate the SessionBootstrapPrompt home), tests/Overmind.Mcp.Tests. Review: light.
</task-breakdown>

<verification>
- dotnet build 0 warnings + dotnet test green after each task; reviewer per task (light).
- Live dogfood on the isolated host (scratch HOME, port 4799+, kill by PID): validate spec → 409 verbatim; notify(approval, featureSlug) → human reply → validate green + proof log line; patch spec (demote) → revalidate → 409 (stale proof). Evidence into the result artifact.
- 068 grep gates stay green.
</verification>

<refs>
- Spec 55333889 (validated) — R1–R6, D1–D3.
- 068 door sources; 039/078 failure-code pattern; 055 gate/asMigrated; 020 notification entity; microbatch #43 absorbed.
</refs>

<log>
- 2026-09-26 — plan drafted after spec validation; two sequential tasks, backend first (surface pins its texts against the live 409).
</log>