---
feature: 004
task: 03
slug: missing-language-coders
status: done
review: light
created: 2026-09-27
brief: Author node trio on master: node-coder/node-code-reviewer/node-architect
---

<goal>Author the node trio on master .kimi-code: node-coder, node-code-reviewer, node-architect (skill + agent each).</goal>
<scope>- skills/node-coder/SKILL.md + agents/node-coder.md (kimi shape, no model pin): owns standalone Node services (Express and derivatives Fastify/Nest/Hono), workers, scripts/CLI/tooling, non-UI npm libraries. Stack baseline: TypeScript strict, ESM, structured logging (no string interpolation in templates), graceful shutdown, env-based config, vitest, no-comments, English-only, mandatory decomposition, AbortSignal in async APIs. Anti-scope: anything inside a Next.js repository (→ web-coder), UI code (→ web-coder), Electron/RN (→ js-platform-coder); routing/handoff table; output contract; Russian-comms rule. Reference sibling shape: skills/feature-orchestrator/SKILL.md structure + dotnet-coder discipline sections.
- node-code-reviewer skill + agent: mirrored checklist (TS strictness, async/error handling, security: injection/secrets/auth, dependency hygiene, API design); reviewer-independence framing.
- node-architect skill + agent: service boundaries, transport choices, deployment shape for Node backends; **Ownership:** consulted BY feature-orchestrator note; never runs planning gates.
Registration on master only as needed (SYNC-STRUCTURE rows land in T7).</scope>
<anti-scope>No port sides; no SKILLFLOW/SYNC-STRUCTURE/contract edits (T7); no BACKLOG//audits/.</anti-scope>
<constraints>One skill one job; exact stack names; English-only; NO git commit.</constraints>
<acceptance-criteria>
- [x] kimi counts: agents=26, skills=29
- [x] All three SKILL.md carry scope, anti-scope with routing, output contract; YAML grep #7 empty
</acceptance-criteria>
<refs>Spec 49cb498f §Phase A items 4–6; Plan T3; port side depends on this task (T4).</refs>
<obstacles></obstacles>
<log>
- 2026-09-27 [agent] — взята в работу исполнителем
- 2026-09-28 [agent] — normalize original T3 task body in place (order=3): AC newline fix, collapsed duplicate log; no semantic changes
- 2026-09-28 [agent] — normalize T3 body: closing acceptance-criteria tag moved to its own line so the board parses the AC items
- 2026-09-28 [agent] — node trio authored on master .kimi-code: skills/node-{coder,code-reviewer,architect}/SKILL.md + agents/node-{coder,code-reviewer,architect}.md. kimi counts 23/26 → 26/29 (AC-1 verified). All three SKILL.md carry Scope + Anti-Scope with routing tables + Output Contract; reviewer carries independence framing, architect carries Ownership-consulted-BY note; agent descriptions quoted where they contain ": " — grep #7 empty (AC-2 verified). Contamination greps empty on new files; all 6 frontmatters parse as YAML. Quirk: task body had unparsable AC section (missing newline after opening tag AND closing tag glued to last item) — fixed by full upsert order=3. Quirk: first normalization upsert ran without order and created a duplicate task (745b3fa6, order 16) — tombstoned + blocked, delete it in the Overmind UI (MCP cannot delete tasks). No git commits, no SKILLFLOW/SYNC-STRUCTURE/contract edits (T7 lands registration).
- 2026-09-28 [agent] — T3 done: node trio (skills + agents) authored on master .kimi-code, both ACs verified (counts 26/29, grep #7 empty, sections present), moved to review. Port side (T4) unblocked.
- 2026-09-28 [agent] — Orchestrator review passed (content review): node-coder SKILL.md is concrete and enforceable — Node 24 LTS baseline, zod boundaries, pino field-logging (no template interpolation), mandatory layering with one-way deps, AbortSignal discipline, fail-fast env config, routing table incl. monorepo rule from the spec. Reviewer carries independence framing + handoff to node-architect; architect carries Ownership note. Counts 26/29, YAML clean, 6 files present. Board duplicate 745b3fa6 (order 16) tombstoned+blocked — UI deletion pending alongside 3159541d.
</log>