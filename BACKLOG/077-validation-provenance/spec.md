---
feature: 077
slug: validation-provenance
status: validated
created: 2026-09-26
validated: 2026-09-26
---

# Spec 077 — Validation provenance: planning_validate requires proof of human approval (token or explicit human path)

<goal>
planning_validate for spec/plan artifacts carries structural proof of human approval instead of the agent's word. With humanGate=off (the default), an agent could validate both spec and plan with zero human involvement — "validated by the user" rested on prompt fidelity (BobHouse B4). Now the agent door requires a human approval reply bound to the feature, found server-side; the ceremony costs the agent one canonical notify call and zero new parameters.
</goal>

<decisions>
- D1 (user, 2026-09-26): token mechanics; humanGate default stays off (the standing default-flip sign-off remains open, out of scope here).
- D2 (user, 2026-09-26): do not complicate agents — NO new tool parameters, NO polling; the server finds the proof itself. Feature binding happens server-side via the notification's featureSlug deep link, not via agent-supplied tokens.
- D3 (user, 2026-09-26): in-chat flow = notify(approval) → human bell-reply → validate; the chat "ок" keeps working with one click of human evidence behind it.
</decisions>

<requirements>
- R1 (proof search): agent-door validate on kind spec/plan searches AgentNotification for: kind=Approval, FeatureSlug = the artifact's feature slug, ReplyText non-empty, RepliedAt >= artifact.UpdatedAt (the artifact was not modified after the human's reply — an edit after approval voids the proof and the demote rule already resets status). Proof found → validate proceeds and writes a mandatory sourced log line: "[agent] — validated with human proof: notification <id> replied <ts>" (closes microbatch #43: validate never wrote a sourced line).
- R2 (no proof → typed 409): PlanningFailureCode ValidationProofRequired; detail is actionable: "Human approval required: send overmind_notify(kind=approval, featureSlug=…) and wait for the human's reply in the notification center, or validate via the UI." Both agent-door transports surface it.
- R3 (doors unchanged otherwise): human door validate stays free (human by construction); humanGate=on STOP path unchanged (UI click remains the proof there); asMigrated keeps its explicit logNote provenance (055 contract, untouched).
- R4 (scope): only spec/plan on the agent door are gated; task/result validation unaffected (non-goal: gating them).
- R5 (discoverability): planning_validate description documents the proof mechanism and the 409; session bootstrap mentions it next to the existing validation line.
- R6 (contract pins): Api tests (proof found / stale artifact after reply → 409 / wrong-feature notification → 409 / question-kind reply does not count / human door unaffected / gate-on STOP precedence), Mcp contract tests (description text, 409 passthrough), Canned fixture for the 409; live dogfood: notify → human reply → validate green; validate-before-reply → 409 verbatim.
</requirements>

<non-goals>
- Flipping the humanGate default (stays off; standing sign-off remains open and separate).
- Gating task/result artifacts; UI changes (the bell already supports replies); notification model changes (existing fields suffice); batch validation.
- Explicit token parameters on planning_validate — the server resolves proof (D2).
</non-goals>

<acceptance-ideas>
- Agent validates spec without any notification → 409 with the actionable text; after notify + human reply "lgtm" the same validate call succeeds and the artifact log carries the proof line with the notification id.
- Human replies, agent patches the spec (demote), edits, re-validates → 409 again (artifact newer than the reply).
- A reply on a notification targeting a different feature does not validate this feature's spec.
- UI validate button works with no notification present; gate-on project: agent validate still STOPs before any proof logic.
- dotnet build + test green; grep gates from 068 stay green.
</acceptance-ideas>

<context>
Code facts: PlanningArtifactService.ValidateAsync(artifactId, logNote?, source?, ct) — no proof concept today (068 added the door source). AgentNotification entity: Id, Kind (Approval/Question/Info), FeatureSlug?, Tab?, Text, ReplyText?, RepliedAt?, ReadAt? — one reply per notification (overwrites), human reply door POST /api/notifications/{id}/reply, agent read door GET /api/agent/notifications/replies. Notification kinds validated via IsEnumName; FeatureSlug is a free string set by the notifying agent — the proof check compares it to the artifact's feature slug. humanGate=on STOP precedes any validation (056 contract); validation matrix: draft→validated, demote on edit (078/068 chain).
</context>

<refs>
- BobHouse 020 note B4 (source problem; note deleted after intake — provenance in this body).
- 055 (humanGate default + asMigrated), 043 (sourced log lines), 068 (door markers, demote rule, quickstart validation line), 020 (notification entity + both doors).
- Microbatch #43 (validate without sourced log line — absorbed here, remove from the queue when this lands).
</refs>

<log>
- 2026-09-26 — spec drafted; D1–D3 decided by user in chat (token mechanics, no agent complexity, notify→bell-reply flow); design: server-side proof search, no token parameter.
</log>