---
feature: 003
task: 08
slug: crew-role-boundaries
status: done
review: light
created: 2026-09-27
brief: Port role sharpening ×3 + overmind-ceremony model pins (opus / qwen3.8-max)
---

<goal>Port the role sharpening to three sides and close the explicit-model gap.</goal>
<scope>Apply T7 edits on .claude, .gemini/antigravity, .qwen. Plus model pins: .claude/agents/overmind-ceremony.md add model: opus; .qwen/agents/overmind-ceremony.md add model: qwen3.8-max. Kimi: verify config.toml pool covers the ceremony agent — if the convention differs, record it in obstacles instead of forcing a pin. Antigravity: role-based wording by design, no pin — verify description covers the orchestration role.</scope>
<anti-scope>No new pins beyond overmind-ceremony; no .kimi-code edits; no BACKLOG/ or audits/ edits.</anti-scope>
<constraints>Frontmatter YAML rules per runtime; NO git commit.</constraints>
<acceptance-criteria>
- [x] grep -L "^model:" .claude/agents/*.md → empty
- [x] grep -L "^model:" .qwen/agents/*.md → empty
- [x] Coder/architect wording deltas present on all four sides (spot-check)
</acceptance-criteria>
<refs>Spec f0fdb479 §4–6; Plan T8; blocked by T7 (master output).</refs>
<obstacles></obstacles>
<log></log>

<log>
- 2026-09-27 [agent] — взята в работу исполнителем
- 2026-09-27 [agent] — Executor summary. (1) T7 ported to .claude, .qwen, .gemini/antigravity — 5 files per side (dotnet-coder, next-js-coder, maui-coder, dotnet-architect, next-js-architect SKILL.md): coder frontmatter no longer claims "review"; dotnet-coder and maui-coder self-review sections replaced with master's "Review Handoff (NOT self-review)" + handoff-style Output Contract; architect frontmatter plan-features claims stripped; **Ownership:** consulted BY feature-orchestrator line added after H1. Port files are byte-identical to master except the runtime-specific dotnet-best-practices path in dotnet-coder (~/.claude|~/.qwen|~/.gemini/antigravity). (2) Model pins: .claude/agents/overmind-ceremony.md += model: opus; .qwen/agents/overmind-ceremony.md += model: qwen3.8-max. Both grep -L "^model:" checks now return empty. (3) Kimi: convention differs — .kimi-code/config.toml has no per-agent model field; the ceremony/orchestration role is covered by the pool (default_model kimi-code/k3 annotated "audit big tasks, orchestrator tasks, deep planning"). Recorded here instead of forcing a pin, per task scope. (4) Antigravity: overmind-ceremony agent description + whenToUse + declared subagents already cover the orchestration role; no pin by design. (5) Deviation (a) logged and applied: dotnet-architect description/whenToUse on ALL FOUR sides (.kimi-code included) said "MassTransit topology" / "MassTransit/Aspire topology"; replaced with Wolverine-primary wording "Wolverine messaging topology (MassTransit legacy)" / "Wolverine/Aspire messaging topology (MassTransit legacy)" — single-line drift-class fix, frozen spec untouched. Deviation (b): nothing to do (already done in T6). (6) Observation (out of scope, master not touched beyond 5a): master next-js-coder has no "Review Handoff" section and its Output Contract still says "Next step — next-js-code-reviewer (mandatory Phase 3 for Complex tasks)" — T7 on master touched only its frontmatter; ports mirror master byte-for-byte. Recommend a follow-up master-side alignment task if the intent was full parity with dotnet-coder.
- 2026-09-27 [agent] — Implementation complete and verified by executor; handed to review. All 3 ACs ticked (both model-pin greps empty; T7 wording deltas byte-verified on all four sides). Two spec deviations applied and logged (5a on four sides incl. kimi; 5b nothing to do). No commits made.
- 2026-09-27 [agent] — Orchestrator review passed: model pins verified (claude opus, qwen qwen3.8-max; kimi pool convention documented without forced pin; antigravity role wording confirmed), T7 semantics ported byte-identically ×3 sides, deviation (a) MassTransit→Wolverine done in dotnet-architect frontmatter ×4. Executor observation about next-js-coder assessed as non-issue: it never had a self-review section; its Output Contract correctly hands to next-js-code-reviewer. NEW finding routed to task 11: dotnet-architect BODY still teaches MassTransit topology (lines 25/43/48/61) — same C1 drift class, out of my too-narrow deviation scope; fix as a proper task before the final checklist.
</log>
