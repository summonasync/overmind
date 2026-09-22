---
feature: 039
task: 03
slug: artifact-patch-write
status: done
review: light
created: 2026-09-22
brief: Agent /patch endpoint + planning_patch_artifact tool with slim D7 echo; cost-tier descriptions on all three write tools + bootstrap routing line, pinned by tests
---

# 039/03 — Agent surface: /patch endpoint + planning_patch_artifact + cost-transparent descriptions

<goal>
The agent door for patches: `POST /api/agent/planning/artifacts/{id}/patch` on the Host and the `planning_patch_artifact` MCP tool with a slim echo. The three write tools state their cost tier in their descriptions (D9) and the session bootstrap carries the pick-by-cost routing line (R7) — so the DMT-style "wrong tool" failure stops being possible.
</goal>

<context>
Task 02 delivered `PatchAsync`. The agent API group lives under `/api/agent/planning/...` in the Host; the MCP adapter mirrors it in `OvermindPlanningTools.cs` (`planning_append_log` at :211 is the closest shape: `Guarded(() => _api.PostAsync(...))` + compact string echo). Spec D7 fixes the echo; D9 fixes discovery. The 032 feature established the description pin-test pattern (live `ListToolsAsync`, assert descriptions verbatim).
</context>

<scope>
- Host agent endpoint `POST /api/agent/planning/artifacts/{id}/patch`, request `{ op: "replace_section" | "tick_ac", section?, content?, acIndex?, acText?, logNote? }`; planning errors map to 400 ProblemDetails carrying the diagnostics from task 02; success returns the D7 projection `{ artifactId, kind, section, version, bodyChars }`.
- MCP tool `planning_patch_artifact` in `OvermindPlanningTools.cs`: mirrors the endpoint, `Destructive = false`; tool-level `Idempotent` stays `false` because the replace branch is not idempotent — tick idempotency is semantic (documented in the description) and tested.
- Echo per D7 — one compact line/string, no body, no feature payload.
- D9 descriptions, verbatim tiers: `planning_append_log` — cheapest write: one dated line, body is not resent; `planning_patch_artifact` — middle tier: replaces one XML section or ticks one acceptance item, rest of the body untouched; `planning_upsert_artifact` — most expensive write: resends the full body (creating or replacing whole artifacts).
- `SessionBootstrapPrompt` (:18, :59): add the routing line "line → planning_append_log; one section / one AC item → planning_patch_artifact; whole body → planning_upsert_artifact" next to the existing mentions.
- Tests: Api endpoint test (both ops, 400 diagnostics, D7 projection); Mcp description pin-test for all three write tools (032 pattern); echo compactness pin (response contains no body substring).
</scope>

<anti-scope>
- No Web UI surface.
- No renames or signature changes to existing tools — descriptions only (D9).
- No read-side symmetry — that is 040's spec.
</anti-scope>

<constraints>
- Agent door only (`/api/agent/*`); the human door is untouched.
- No new packages; follow the `Guarded` + `PostAsync` adapter idiom.
- All user-facing strings in English (house rule).
</constraints>

<acceptance-criteria>
- `dotnet build` + full `dotnet test` green.
- Endpoint contract per R1; 400s carry searchable diagnostics.
- Description pin-test locks the three cost-tier texts; bootstrap pin-test locks the routing line.
- Echo contains the D7 projection and zero body content.
</acceptance-criteria>

<refs>
- Spec 039 D1, D2, D6, D7, D9, R1, R2, R7.
- OvermindPlanningTools.cs:211-223 (append_log shape), SessionBootstrapPrompt.cs:18/:59.
- 032-status-model-self-documentation — pin-test pattern.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-22 — Carry-over from review 039/02 (NIT-2, audits/review-039-02.md): the PatchAsync dispatcher checks only null — blank Section (" ") / TickText ("") reach domain guards and surface as raw ArgumentException, which the Host maps to 500, not 400. Close it in this task: whitespace validation at the boundary (or catch ArgumentException → InvalidPatchRequest) so R1's 400 contract holds.
- 2026-09-22 — Execution starts: coder sub-agent takes the agent-door surface contract (endpoint + MCP tool + cost-tier descriptions + bootstrap routing), including the NIT-2 boundary fix carried from review 039/02.
- 2026-09-22 — Light review PASS-WITH-NITS (0 BLOCKER / 0 IMPORTANT / 2 NIT), gates re-verified: build 0W/0E, tests 1102/1102. NIT-2 closure proven (Never500 matrix + RejectedWithoutHttpCall); tasks 01/02 files confirmed untouched by line-ref audit. Report: audits/review-039-03.md.
- 2026-09-22 — Accepted: NIT-1 (trim patch fields) → microbatch queue #37; NIT-2 (generic 400 text) accepted as-is — the MCP door, the primary one, gives specific fragments. Agent surface is complete; feature moves to the e2e closeout.
</log>
