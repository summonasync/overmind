---
feature: 093
task: 02
slug: approval-deep-link-toast
status: done
review: light
created: 2026-09-28
brief: MCP: url passthrough pin + quote-the-url instructions in notify description / ServerInstructions / SessionBootstrapPrompt
---

# 093/02 — MCP: proxy the url, teach the agent to quote it

<goal>
overmind_notify returns the host-stamped url and the agent-facing text tells the agent to quote it in chat when asking for an approval or reporting a question.
</goal>

<context>
Spec 3748e264 (validated), plan 98cb3e9a — task 2 of 3, parallel with 01 (disjoint files). Current state: src/Overmind.Mcp/OvermindNotificationTools.cs Notify() returns Guarded(() => _api.PostAsync(...)) — likely a raw JsonElement passthrough, in which case url arrives for free and this task is instructions + contract pins. Confirm and pin it either way. Session bootstrap lives in Program.cs ServerInstructions (~line 99) and SessionBootstrapPrompt.
</context>

<scope>
- src/Overmind.Mcp/OvermindNotificationTools.cs — Notify: verify/ensure the host response (including url) reaches the tool result unmodified (passthrough expected; if the method projects fields, add url to the projection). Tool description gains: the response may carry url — quote it in chat when asking for an approval or reporting a question so the human can click through.
- src/Overmind.Mcp/Program.cs — ServerInstructions: one added line pointing at quoting notify urls for approvals/questions.
- SessionBootstrapPrompt — same line, matching its existing phrasing style.
- tests/Overmind.Mcp.Tests — stub route for create returns a body with url → tool response pins the url passthrough; description pin updated (tool names count unchanged — no new tools); bootstrap/instruction text pinned to contain the quoting line.
- Verify: dotnet build + dotnet test (Overmind.Mcp.Tests green).
</scope>

<anti-scope>
- Host (task 01), web (task 03), overmind_replies, any new tool.
</anti-scope>

<constraints>
- Adapter stays a thin tolerant proxy — no origin/url construction in the adapter. Contract additive-only. No comments. English-only.
</constraints>

<acceptance-criteria>
- [x] Tool response passes url through unmodified (contract test pins it).
- [x] Notify description + ServerInstructions + SessionBootstrapPrompt instruct quoting the url for approvals/questions.
- [x] Tool count unchanged; handshake contract test still green.
- [x] dotnet build + dotnet test green.
- [x] Work logged via planning_append_log.
</acceptance-criteria>

<refs>
- Spec 3748e264 (validated); plan 98cb3e9a.
- src/Overmind.Mcp/OvermindNotificationTools.cs, src/Overmind.Mcp/Program.cs, SessionBootstrapPrompt.
- Task 01 defines the url field's exact host shape.
</refs>

<obstacles>
- The url passthrough pin's canned body contains `&` in the query string; it only survives JSON serialization verbatim because Program.cs already swaps the SDK's HTML-safe encoder for UnsafeRelaxedJsonEscaping at startup (same mechanism the Cyrillic quickstart pins rely on). If that encoder patch ever degrades to escaped output, the pin (and Cyrillic pins) will fail — signal, not noise.
- url field shape in Canned.NotificationWithUrlJson mirrors the spec's <browser-origin>/dashboard?project=…&feature=…&tab=…; task 01 owns the exact host stamp — if its final shape differs in field placement the passthrough pin still holds (verbatim proxy), only the canned literal would need a touch-up.
</obstacles>

<log>
- 2026-09-28 [agent] — task sliced from validated plan; parallel with 01.
- 2026-09-28 [agent] — coder picked up task 02
- 2026-09-28 [agent] — coder: Notify confirmed pure passthrough (SendAsync returns document.RootElement.Clone() — zero projection), so url arrives for free and Notify's code path was left untouched. Added: quoting instruction to overmind_notify description (OvermindNotificationTools.cs), one ServerInstructions line (Program.cs), NotificationUrlRouting const composed into both bootstrap branches (SessionBootstrapPrompt.cs). Tests: Notify_HostResponseUrl_ReachesTheToolResultUnmodified pins verbatim url passthrough via stub route returning Canned.NotificationWithUrlJson; description/instructions/bootstrap quoting line pinned (canonical fragment 'quote the url in chat when asking for an approval'). Tool count untouched — ToolsList_ExposesExactlyTheThirtyFourTools green. Evidence: dotnet build 0 warnings/0 errors; dotnet test tests/Overmind.Mcp.Tests 388/388 passed; targeted run of the 5 new/updated pins green.
- 2026-09-28 [agent] — coder done, ready for review: Notify verified as pure passthrough (no code change needed in the method), url passthrough + all three quoting instructions pinned by tests; build 0 errors, Overmind.Mcp.Tests 388/388 green; all 5 ACs ticked.
- 2026-09-28 [agent] — [orchestrator] light review PASS (audits/review-093-02.md): 2 NIT (description ellipsis wording; Canned url literal duplication) — в микробатч, не блокеры; reviewer перепроверил build 0/0 + 388/388; AC 5/5 подтверждены
- 2026-09-28 [agent] — review PASS (audits/review-093-02.md), AC 5/5, build 0/0 + Mcp.Tests 388/388 reviewer-verified; 2 NIT ушли в микробатч (#128, #129)
</log>