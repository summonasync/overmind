---
feature: 003
task: 05
slug: crew-role-boundaries
status: done
review: light
created: 2026-09-27
brief: Stack drift fix on .kimi-code: Mediator/Wolverine/Shouldly/Mapperly, .NET 10+/C# 14+
---

<goal>Fix the library-stack drift in .kimi-code coder/reviewer/init/best-practices skills.</goal>
<scope>dotnet-coder SKILL.md: CQRS → Mediator (martinothamar), one-handler-per-file stays; Messaging → Wolverine (never raw RabbitMQ/ServiceBus clients; MassTransit ONLY in legacy codebases); Testing → xUnit + Shouldly + NSubstitute; mapping → Mapperly or manual (never AutoMapper); Runtime → .NET 10+ (latest LTS), C# 14+; description line «MassTransit conventions» → modern-messaging wording; architecture-rules lines mentioning MassTransit → Wolverine + legacy clause. dotnet-code-reviewer: checklist flags MediatR / MassTransit / AutoMapper / FluentAssertions as violations in NEW code, allowed in legacy with a note. dotnet-init: same stack pins. maui-coder: testing/assertion lines if present. dotnet-best-practices: BP-IDs pinning the banned libs → align with Mediator/Wolverine/Shouldly/Mapperly. nuget-licensing-guard stays untouched — align wording with it, do not duplicate its tables.</scope>
<anti-scope>No port sides; no other skills; no weakening of quality rules; no BACKLOG/ or audits/ edits.</anti-scope>
<constraints>Legacy clause explicit everywhere the banned stack is mentioned; English-only; NO git commit.</constraints>
<acceptance-criteria>
- [x] grep -n "MediatR\|MassTransit\|FluentAssertions\|AutoMapper" .kimi-code/skills/dotnet-coder/SKILL.md → hits only inside legacy clauses
- [x] dotnet-coder pins .NET 10+ / C# 14+ / Shouldly / Mediator / Wolverine / Mapperly
- [x] dotnet-code-reviewer flags the banned stack in new code
</acceptance-criteria>
<refs>Spec f0fdb479 §3; Plan T5; contract section in AGENTS.md («Mediator over MediatR…») is the source of truth. Port side depends on this task (T6).</refs>
<obstacles></obstacles>
<log></log>

<log>
- 2026-09-27 [agent] — взята в работу исполнителем
- 2026-09-27 [agent] — Stack drift fixed in 4 files: dotnet-coder (desc/whenTo-use wording, Tech Stack: .NET 10+, C# 14+, Mediator (martinothamar), Wolverine + MassTransit legacy clause, new Mapping row with Mapperly/manual + AutoMapper legacy clause, xUnit+Shouldly+NSubstitute, Shouldly assert example, Mediator IMessage/MessageHandler file-split rule, Wolverine handler/config layering, review triggers Mediator/Wolverine); dotnet-code-reviewer (desc + whenToUse + context line reworded, Messaging (Wolverine) section rewritten, new "Third-Party Library Stack" section: MediatR/MassTransit/AutoMapper/FluentAssertions in NEW code = BLOCKER, legacy allowed with explicit note, FluentAssertions=BLOCKER in Testing, sensitive-data line stack-agnostic, C# 14+); dotnet-init (desc, .NET 10+/C# 14+, Wolverine + legacy clause, Shouldly, scan step and AGENTS.md-generation lines legacy-aware); maui-coder (.NET 10+/C# 14+, Shouldly in Tech Stack and Testing Rules, FluentAssertions legacy clause). dotnet-best-practices already aligned (BP-31/60/63/92) — untouched; nuget-licensing-guard untouched. Residual out of scope: tutor-master/SKILL.md:87 still says "modern C# 13" — outside this task's 5-file scope, flag for follow-up. All 3 ACs verified by grep.
- 2026-09-27 [agent] — All 3 ACs verified by grep, ticked; executor work complete — moving to review per task contract
- 2026-09-27 [agent] — Orchestrator review passed: dotnet-coder pins verified (.NET 10+/C# 14+/Mediator martinothamar/Wolverine/Mapperly/Shouldly), banned libs only inside legacy clauses (2 hits, both clause-bearing), reviewer has explicit BLOCKER rules for banned stack in new code (3 rules), dotnet-best-practices already aligned (BP-31/60/63/92), nuget-licensing-guard untouched. Out-of-scope leftover recorded: tutor-master:87 «modern C# 13» — routed to T6 as a one-line drift fix across all four sides (same drift class, deviation logged there).
</log>
