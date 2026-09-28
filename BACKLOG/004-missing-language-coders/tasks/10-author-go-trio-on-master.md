---
feature: 004
task: 10
slug: missing-language-coders
status: done
review: light
created: 2026-09-27
brief: Author go trio on master
---

<goal>Author the go trio on master .kimi-code: go-coder + go-code-reviewer (skills + agents) + go-best-practices (skill, no agent).</goal>
<scope>- go-coder SKILL.md + agent: idiomatic Go — stdlib-first, error wrapping (%w), context propagation, table-driven tests, gofmt, go.mod hygiene; no-comments, English-only, decomposition. Anti-scope: other languages; routing table + output contract.
- go-code-reviewer skill + agent: mirrored checklist (error handling, concurrency safety, context usage, security, dependency hygiene).
- go-best-practices skill: BP-ID catalog, initial top rules. NO agent.</scope>
<anti-scope>No port sides; registration sweep folded into T14; no BACKLOG//audits/.</anti-scope>
<constraints>English-only; NO git commit.</constraints>
<acceptance-criteria>
- [x] kimi counts: agents=32, skills=40; YAML grep #7 empty; bp skill agentless
</acceptance-criteria>
<refs>Spec §Phase C items 13–15; Plan T10; port side depends on this task (T11).</refs>
<obstacles></obstacles>
<log>
- 2026-09-28 [agent] — взята в работу исполнителем
- 2026-09-28 [agent] — normalize artifact body: acceptance-criteria tags onto their own lines, collapse duplicated log section — content unchanged
- 2026-09-28 [agent] — AC verified: kimi counts 32 agents / 40 skills (exact), YAML grep #7 empty on agents and skills, go-best-practices has no agent file; leakage greps clean
- 2026-09-28 [agent] — go trio authored on master .kimi-code: skills/go-coder/SKILL.md + agents/go-coder.md (stdlib-first net/http, %w wrapping, ctx propagation, goroutine ownership, table-driven tests, gofmt/go.mod hygiene, routing table + output contract); skills/go-code-reviewer/SKILL.md + agents/go-code-reviewer.md (mirrored checklist: error handling / concurrency / context / security / module hygiene; read-only tools allowlist); skills/go-best-practices/SKILL.md (BP-01..BP-64 catalog, 38 rules, no agent). AC verified: agents=32, skills=40 exact; YAML grep #7 empty; bp agentless. Port side (T11) unblocked; registration remains with T14 sweep.
- 2026-09-28 [agent] — executor finished: go trio authored on .kimi-code (5 new files), AC ticked, moving to review
- 2026-09-28 [agent] — Orchestrator review passed: go trio authored (coder with stdlib-first net/http + slog + goroutine ownership + %w error wrapping; reviewer with independence framing + 6-block checklist; BP catalog 38 rules in decades), counts 32/40 exact, YAML clean, agentless bp confirmed.
</log>