---
feature: 003
task: 11
slug: crew-role-boundaries
status: done
review: light
created: 2026-09-27
brief: dotnet-architect body: MassTransit → Wolverine topology rewrite ×4 (C1 leftover from T8 review)
---

<goal>Rewrite the dotnet-architect SKILL.md BODY from MassTransit-primary to Wolverine-primary (MassTransit legacy-only) on all four sides — closing the C1 drift class found during T8 review.</goal>
<scope>- skills/dotnet-architect/SKILL.md on .kimi-code, .claude, .gemini/antigravity, .qwen:
  - Tech-stack messaging bullet (master line 25): «Use MassTransit as the abstraction over transports» → Wolverine as the messaging abstraction (transports: RabbitMQ, Azure ServiceBus, Kafka); MassTransit allowed only as a legacy note.
  - Section «MassTransit Topology Design» (master line 43) → «Messaging Topology Design (Wolverine)»: rewrite the guidance to Wolverine equivalents (handlers, messaging conventions, saga support where applicable); keep the section structure and any Aspire content intact; banned lib survives only inside an explicit legacy clause.
  - Sagas bullet (master line 48): Wolverine sagas instead of «MassTransit State Machine Sagas», legacy clause for MassTransit sagas.
  - Documentation checklist item (master line 61): «MassTransit topology description» → messaging topology description (Wolverine).
  - Frontmatter is ALREADY fixed (T8 deviation) — do not redo it; verify it still reads Wolverine-primary.
  - Keep each side's existing runtime wording; port copies change by the same semantics only.</scope>
<anti-scope>No other files; no frontmatter rewrites; no changes to reviewers/coders; no BACKLOG/ or audits/.</anti-scope>
<constraints>Do not lose architectural guidance quality — this is a library swap with equivalent depth, not a deletion; legacy clause explicit; English-only; NO git commit.</constraints>
<acceptance-criteria>- [x] grep -n "MassTransit" on dotnet-architect/SKILL.md returns hits ONLY inside explicit legacy clauses, on all four sides
- [x] A Wolverine-primary messaging-topology section exists on all four sides, Aspire content preserved</acceptance-criteria>
<refs>Spec f0fdb479 §3 (C1 drift class); T8 log (finding origin); Plan T10 runs after this task.</refs>
<obstacles></obstacles>
<log>
- 2026-09-27 [agent] — взята в работу исполнителем
- 2026-09-27 [agent] — AC ticks via full upsert: duplicate <log> section from status move broke patch-based AC matching; body normalized, both ACs verified and ticked
- 2026-09-27 [agent] — Done: messaging bullet (line 25) now Wolverine-primary with explicit legacy-only note; «MassTransit Topology Design» renamed to «Messaging Topology Design (Wolverine)» with 5 Wolverine equivalents (conventional routing, Handle()/InvokeAsync RPC, app-level error policies with cooldown, durable inbox/outbox EF Core/Marten, stateful Saga by Id) plus a Legacy (MassTransit) paragraph; checklist item now messaging topology (Wolverine, legacy MassTransit noted). All 4 sides byte-identical (verified by diff before and after; ports spread via cp from master). Aspire section untouched (line 37). grep -n MassTransit: 5 hits/side, all inside legacy clauses (2 frontmatter pre-existing, bullet note, legacy paragraph, checklist note). Frontmatter untouched.
- 2026-09-27 [agent] — executed per artifact: 4 sides rewritten Wolverine-primary, ACs verified by grep+diff on all sides, moved to review
- 2026-09-27 [agent] — Orchestrator review passed: MassTransit hits verified legacy-only on all four sides (case-insensitive check), Wolverine messaging-topology section present ×4, Aspire section untouched, frontmatter preserved. Board bug found by executor and to be reported to the Overmind owner: first planning_set_status move duplicated the <log> section in the task body, breaking AC parsing (patch by acText 400) until a full upsert normalized the body.
</log>