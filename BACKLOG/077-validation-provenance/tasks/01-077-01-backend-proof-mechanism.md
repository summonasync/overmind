---
feature: 077
task: 01
slug: validation-provenance
status: done
review: light
created: 2026-09-26
brief: Agent-door validate requires a replied Approval notification bound to the feature; typed 409 + sourced log line on every validation (absorbs #43)
---

# 077/01 — Backend proof mechanism

<goal>
Agent-door validate on spec/plan requires server-side human-approval proof (spec R1–R4): a replied Approval notification bound to the feature, with the artifact untouched since the reply. No proof → typed 409. Every validation now writes a sourced log line (closes microbatch #43).
</goal>

<scope>
- Core: PlanningFailureCode.ValidationProofRequired (append at the end — 034 numeric-shift precedent) + PlanningException factory, 039/078 pattern.
- Infrastructure: proof enforcement only on the agent door — either a requireProof flag on ValidateAsync or a dedicated service method; the agent-door handler checks the existing humanGate STOP first, then proof. Proof query (AsNoTracking): AgentNotification kind=Approval, FeatureSlug == artifact's feature slug, ReplyText non-empty, RepliedAt >= artifact.UpdatedAt. Feature slug resolved via the artifact's FeatureId.
- Sourced log line on EVERY validation path through the existing artifact.Validate(logNote, source): human door → "validated via UI"; agent door → "validated with human proof: notification <id> replied <ts>". asMigrated (055) untouched.
- Typed 409 with the actionable detail text from the spec (notify → reply → retry, or UI).
- Api/Infra test pins: proof-found green + log line content; no-notification 409 verbatim; artifact edited after the reply → 409 (stale proof); wrong-feature notification → 409; question-kind reply does not count; human door free without notification; gate-on STOP precedence over proof logic.
</scope>

<anti-scope>
- MCP descriptions, bootstrap, Canned (task 02). Gate STOP logic itself, asMigrated semantics, task/result gating.
</anti-scope>

<constraints>
- .NET 10 / C# 14; no comments; English-only; no git commands; dotnet build 0 warnings + dotnet test green.
</constraints>

<acceptance-criteria>
- [x] Agent validate with replied approval → green, log carries "[agent] — validated with human proof: notification <id> replied <ts>"
- [x] No notification → 409 ValidationProofRequired with the actionable detail (pinned verbatim)
- [x] Stale/wrong-feature/question-kind proofs → 409 (three pins)
- [x] Human door validate → green without notification, log carries "[ui] — validated via UI"
- [x] Gate-on project → STOP fires before proof logic (existing pin stays green)
- [x] dotnet build 0 warnings + dotnet test green
</acceptance-criteria>

<refs>
- Spec 55333889 (validated) R1–R4; 068 door sources; 039/078 failure-code pattern; 055 gate/asMigrated; 020 notification entity.
</refs>

<obstacles />

<log />

<log>
- 2026-09-26 [agent] — execution started: backend proof mechanism — typed ValidationProofRequired 409 on agent door, sourced log lines on every validation path
- 2026-09-26 [agent] — 077/01 implemented. Core: PlanningFailureCode.ValidationProofRequired appended at the end (034 precedent) + PlanningException.ValidationProofRequired(featureSlug) factory — detail verbatim per spec R2: "Human approval required: send overmind_notify(kind=approval, featureSlug={slug}) and wait for the human's reply in the notification center, or validate via the UI." Infrastructure: ValidateAsync gained requireHumanProof (default false, asMigrated untouched — explicit logNote bypasses proof search); BuildHumanProofLineAsync does the AsNoTracking AgentNotification search (kind=Approval, FeatureSlug == artifact's feature slug via PlanningFeatures lookup, ReplyText non-empty, RepliedAt >= artifact.UpdatedAt, newest first) and builds "validated with human proof: notification <id> replied <ts> UTC"; no proof → typed 409. Host: agent door passes requireHumanProof only for kind spec/plan (!asMigrated && documentGated) AFTER the existing humanGate STOP (precedence preserved); UI door now writes the sourced line "validated via UI" (source Ui) — closes microbatch #43 on every validation path. Pins: 7 new Infra tests (proof-found + exact log line incl. notification id and ts; no-notification verbatim 409; stale artifact after reply; wrong-feature; question-kind; asMigrated-style explicit note bypasses proof) + 7 Api tests (proof-found green with log pin; no-notification 409 verbatim via HTTP; stale after patch; wrong-feature; question-kind; human door free + "[ui] — validated via UI"; gate-on STOP precedes proof — HumanValidationRequired title even with a valid proof present). Honest knock-on updates (UI validate now writes a line, so stale-body resends hit LogHistoryNotAppendable — clients must refetch before resend): FrozenEdit_WithLogNote (refetch frozen body; log now ui-line + unfreezing + [auto] demote), FrozenEdit_WithoutLogNote, Upsert_FrozenSpecWithoutLogNote/WithLogNote, Upsert_WithLogNote_WritesTheAgentMarker (all refetch frozen body / Replace v1→v2), Validate_ProjectOffBeatsGlobalOn (gate-off still honored — added notify+reply so the 200 assertion survives under proof enforcement). Evidence: dotnet build 0 warnings / 0 errors; dotnet test 1495 passed / 0 failed (Core 215, Infrastructure 490, Api 438, Mcp 352); format clean on touched files.
- 2026-09-26 [agent] — Obstacles (intended for the <obstacles> section; the full-body upsert carrying them was rejected by the 078 append-only guard on a hand-copied log — noting here instead): (1) closing #43 means every validation appends a log line — a client that fetched a body before a validation and resends it after hits LogHistoryNotAppendable until it refetches; five existing tests relied on stale-body resends and were updated to refetch the frozen body first (FrozenEdit_WithLogNote/WithoutLogNote, Upsert_FrozenSpecWithoutLogNote/WithLogNote, Upsert_WithLogNote_WritesTheAgentMarker); (2) Validate_ProjectOffBeatsGlobalOn needed notify+reply added — gate-off still suppresses STOP but proof applies regardless of gate state, precedence unchanged and still pinned; (3) requireHumanProof is scoped to spec/plan at the agent-door handler, the service flag is kind-agnostic — result-kind stays free per R4; (4) proof timestamp format "yyyy-MM-dd HH:mm:ss UTC" was unspecified in task/spec; (5) an approval reply predating the artifact's last update never satisfies RepliedAt >= UpdatedAt — covered by the stale pin.
- 2026-09-26 [agent] — 077/01 done: all 6 AC ticked. Agent-door spec/plan validate requires a replied Approval notification bound to the feature with RepliedAt >= artifact.UpdatedAt (AsNoTracking search); no proof → typed 409 ValidationProofRequired with the spec-verbatim actionable detail; proof found → mandatory "[agent] — validated with human proof: notification <id> replied <ts> UTC" line; human door free with "[ui] — validated via UI" (closes #43 on every path); asMigrated semantics untouched (explicit note bypasses proof); gate-on STOP precedes proof logic (pinned with a valid proof present). Five stale-body test knock-ons updated honestly (refetch before resend). Evidence: dotnet build 0 warnings / 0 errors; dotnet test 1495 passed / 0 failed (Core 215, Infrastructure 490, Api 438, Mcp 352); format clean on touched files. Review label: light.
- 2026-09-26 [agent] — Fix-round for review PASS-WITH-NITS (audits/review-077-01.md). W-1: the proof search is now project-bound — BuildHumanProofLineAsync loads {Slug, ProjectId} from the feature and adds n.ProjectId == feature.ProjectId, so an approval reply in project A can no longer prove project B's spec with the same feature slug. New pin ValidateAsync_ApprovalReplyInOtherProject_ThrowsValidationProofRequired (second project created with the same "feat" slug + replied approval there → still 409). N-1: new pin ValidateAsync_UnrepliedApproval_DoesNotCountAsProof (approval notification with correct project/slug but no reply → 409). W-2 (Reply semantics) left untouched per instruction — goes to the user. Evidence: dotnet build 0 warnings / 0 errors; dotnet test 1497 passed / 0 failed (Core 215, Infrastructure 492, Api 438, Mcp 352); format clean on touched files.
</log>
