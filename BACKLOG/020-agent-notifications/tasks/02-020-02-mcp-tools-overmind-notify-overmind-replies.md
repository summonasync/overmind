---
feature: 020
task: 02
slug: agent-notifications
status: done
review: light
created: 2026-09-20
---

# 020/02 — MCP tools overmind_notify + overmind_replies

<goal>
Agents get the channel: overmind_notify pushes a notification through the agent door (project resolved from OVERMIND_PROJECT_ID/cwd when projectId omitted), overmind_replies reads the human's replies. Tool count 16 → 18 with the contract pin grown.
</goal>

<context>
Feature 020, layer 2 of 3. Depends on task 01 (routes live). Tool class pattern: [McpServerToolType] + Guarded + OvermindApiClient (OvermindPlanningTools/OvermindOnboardingTools). HandshakeContractTests pins exactly 16 names (:49-55) — grows to 18. McpSessionTestBase.RegisterDefaultRoutes (:39-68) needs the new stubs (regex-path precedent in RegisterPlanningRoutes :70-99).
</context>

<scope>
- src/Overmind.Mcp/OvermindNotificationTools.cs — new tool class: overmind_notify(kind, text, projectId?, featureSlug?, tab?, from?) → POST /api/agent/notifications (project resolution when projectId omitted: OVERMIND_PROJECT_ID valid guid wins, else POST /api/onboard {cwd} reusing the 013 pattern — resolve, do not create junk: pass the resolved id; if resolution fails, a clear error telling the agent to onboard); overmind_replies(projectId?, includeRead?) → GET /api/agent/notifications/replies (same resolution rule). Descriptions: notify says "tell the human" + kinds; replies says "poll for the human's answers".
- Program.cs: DI singleton + .WithTools<OvermindNotificationTools>(); ServerInstructions unchanged.
- tests/Overmind.Mcp.Tests: HandshakeContractTests array → 18 names; tool contract tests (notify posts the right JSON incl. optional-field omission; replies GET with query); stub routes in McpSessionTestBase; project-resolution branch (env var) covered.
- Verify: dotnet build + dotnet test green.
</scope>

<anti-scope>
- No host changes (01 owns routes), no web, no prompts/resources additions.
</anti-scope>

<constraints>
- Overmind.Mcp references NO other project. No new packages. No comments; English-only.
- Known flakes: port race (rerun once), CLR 0x80131506 (build-server shutdown).
</constraints>

<acceptance-criteria>
- [ ] tools/list shows 18 tools incl. overmind_notify + overmind_replies (contract test green).
- [ ] notify posts kind/text/targets; optional params omitted from the payload when absent.
- [ ] projectId resolution: explicit guid → used; env var → used; neither → onboard-cwd path (stubbed in tests).
- [ ] dotnet test green.
</acceptance-criteria>

<refs>
- Spec: feature 020 — tool decisions. Plan: tasks section.
- Pattern refs: OvermindPlanningTools.cs:37-49, OvermindOnboardingTools.cs:12,29-35 (env/cwd resolution), Program.cs:63-78, HandshakeContractTests.cs:40-56, McpSessionTestBase.cs:39-99.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Execution started — dotnet-coder subagent (routes from 01 in tree; review of 01 in parallel).
- 2026-09-20 — Coder done: OvermindNotificationTools (overmind_notify + overmind_replies), resolution chain explicit → OVERMIND_PROJECT_ID → onboard {cwd} with a clear call-overmind_onboard error on failure; optional fields omitted from payloads; client-side kind validation (sibling pattern); Program.cs DI + WithTools; HandshakeContractTests 16 → 18 (renamed Eighteen); RegisterNotificationRoutes stubs + Canned fixtures; 10 contract tests incl. omission, env-branch, onboard-branch, failure path. Suite 846 → 856. To review.
- 2026-09-20 — Review PASS (audits/review-020-02.md): 0 blockers, 5 INFO (unpinned onboard-200-broken-body branch; 503-on-onboard maps to a semantic-search error without an onboard hint — loopback exotic; cwd branch inherits 013 auto-create — sanctioned; env-parse duplication ×2 ok, extract on the third; replies always pins projectId per spec). Resolution chain fully pinned incl. resolved-id-in-payload; omission without null serialization (tighter than the sibling class); includeRead omitted → server default TRUE (lossless, mirroring 020/01); kind vocab byte-exact; descriptions carry the WHEN (013 lesson). Done.
</log>
