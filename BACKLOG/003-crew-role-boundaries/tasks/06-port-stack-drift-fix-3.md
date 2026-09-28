---
feature: 003
task: 06
slug: crew-role-boundaries
status: done
review: light
created: 2026-09-27
brief: Port stack drift fix ×3
---

<goal>Port the stack-drift fix to .claude, .gemini/antigravity, .qwen.</goal>
<scope>Apply the T5 edits to the mapped files on all three port sides (dotnet-coder, dotnet-code-reviewer, dotnet-init, maui-coder, dotnet-best-practices where the drift exists).</scope>
<anti-scope>No .kimi-code edits; nothing else.</anti-scope>
<constraints>Translation tables; identical semantics ×3; NO git commit.</constraints>
<acceptance-criteria>
- [x] Per-side grep (T5 AC #1) shows only legacy-clause hits
- [x] All three sides pin .NET 10+ / C# 14+ / Shouldly / Mediator / Wolverine in dotnet-coder
</acceptance-criteria>
<refs>Spec f0fdb479 §3; Plan T6; blocked by T5 (master output).</refs>
<obstacles></obstacles>
<log></log>

<log>
- 2026-09-27 [agent] — взята в работу исполнителем
- 2026-09-27 [agent] — Port complete on all three sides (.claude, .gemini/antigravity, .qwen). Per side: dotnet-coder, dotnet-code-reviewer, dotnet-init copied from post-T5 master with the runtime path (~/.claude, ~/.gemini/antigravity, ~/.qwen) preserved on the single side-specific line (coder context item 5, reviewer BP-IDs line); residual diff vs master is exactly that one path line per file. maui-coder ported too — initial drift scan (grep -c on '^[-+][^-+]') undercounted bullet lines starting with '-', re-checked with diff -q which exposed real drift; now byte-identical to master (Shouldly + FluentAssertions legacy clause + C# 14 / .NET 10+). dotnet-best-practices: no drift on any side (byte-identical to aligned master), untouched. Verification per side: dotnet-coder banned-lib mentions (MassTransit line 29, AutoMapper line 32) appear ONLY inside legacy clauses; pins confirmed — .NET 10+ (latest LTS) line 25, C# 14+ line 26, Mediator (martinothamar) lines 28/72, Wolverine lines 29/60/62/73/93, Shouldly lines 35/82; reviewer BLOCKER rules present — MassTransit-in-new-code (line 51), MediatR/MassTransit/AutoMapper/FluentAssertions-in-new-code (line 64), FluentAssertions asserts (line 69). SPEC DEVIATION (single line, same drift class, per task instruction): skills/tutor-master/SKILL.md line 87 on ALL FOUR sides (.kimi-code included) changed 'modern C# 13' -> 'modern C# 14'; verified grep 'C# 13' returns 0 hits in all four copies; frozen spec NOT edited — deviation recorded here per instruction. Hard rules respected: no git commit/push, .kimi-code touched only in tutor-master line 87, no contract/BACKLOG/audits files.
- 2026-09-27 [agent] — Execution complete: T5 semantics ported to .claude, .gemini/antigravity, .qwen (coder/reviewer/init/maui-coder; best-practices had no drift); tutor-master C# 13->C# 14 deviation applied on all four sides; both ACs verified and ticked per side. Moving to review.
- 2026-09-27 [agent] — Orchestrator review passed: per-side grep confirms banned libs only in legacy clauses on all four sides (coder lines 29/32 identical), reviewer BLOCKER rules present, tutor-master modern C# 14 on all four sides, best-practices byte-identical (no drift). Executor self-caught a diff-counting quirk (grep -c misses bullet-prefixed diff lines) and re-verified via diff -q — good discipline. Spec deviation (tutor-master line) logged.
</log>
