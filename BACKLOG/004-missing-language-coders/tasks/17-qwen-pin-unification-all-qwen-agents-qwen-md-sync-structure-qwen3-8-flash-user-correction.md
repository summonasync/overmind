---
feature: 004
task: 17
slug: missing-language-coders
status: done
review: light
created: 2026-09-28
brief: Qwen pin unification: all .qwen agents + QWEN.md + SYNC-STRUCTURE → qwen3.8-flash (user correction)
---

<goal>Unify ALL qwen model pins to `model: qwen3.8-flash` — user correction 2026-09-28: the ModelStudio lineup has no 3.7-plus/3.8-max differentiation, one uniform pin everywhere.</goal>
<scope>Grep-driven sweep, .qwen side + root docs:
- ALL files under .qwen/agents/*.md: any `model: qwen3.7-plus` or `model: qwen3.8-max` → `model: qwen3.8-flash` (includes older crew agents: feature-orchestrator, sprint-planner, overmind-ceremony, web trio, dotnet/maui/audit/etc.).
- .qwen/QWEN.md «Sub-Agent Rules» section: model names → qwen3.8-flash, drop the two-tier wording (execution vs strong reasoning pin split).
- .qwen/settings-snippet.jsonc: modelProviders / model references if they name 3.7-plus/3.8-max → qwen3.8-flash.
- SYNC-STRUCTURE.md (root): «Known intentional differences» Qwen column (pin row), Kimi→Qwen translation table rows 3–4 (kimi-for-coding* → qwen3.8-flash; kimi-k3/k3-256k/k3 → qwen3.8-flash), pre-commit checklist #6 text (uniform pin).
- Verification: grep -rn "qwen3.7-plus\|qwen3.8-max" .qwen/ SYNC-STRUCTURE.md → 0 hits; every .qwen/agents/*.md carries exactly one `model: qwen3.8-flash`; YAML grep #7 empty; counts unchanged (pin text only).</scope>
<anti-scope>No non-qwen sides (claude/kimi/gemini pins untouched); no skill body content changes; no BACKLOG//audits/.</anti-scope>
<constraints>English-only; NO git commit.</constraints>
<acceptance-criteria>
- [x] grep -rn "qwen3.7-plus\|qwen3.8-max" across .qwen/ and SYNC-STRUCTURE.md → 0 hits
- [x] Every .qwen/agents/*.md carries exactly model: qwen3.8-flash
- [x] QWEN.md and SYNC-STRUCTURE.md (translation rows + checklist #6) state the uniform pin
- [x] YAML grep #7 empty; counts unchanged
</acceptance-criteria>
<refs>Spec/plan log notes 2026-09-28 (user correction); SYNC-STRUCTURE checklist #6 lineage; blocked by T4 (concurrent .qwen/agents writes), runs before T14.</refs>
<obstacles></obstacles>
<log></log>

<log>
- 2026-09-28 [agent] — взята в работу исполнителем
- 2026-09-28 [agent] — Executor: qwen pin unification complete. 51 old-pin hits swept to the single qwen3.8-flash pin. Changed: 23 .qwen/agents/*.md (13× qwen3.7-plus, 10× qwen3.8-max → qwen3.8-flash; node trio skipped, already correct — 26/26 agents now uniform); .qwen/QWEN.md Sub-Agent Rules (single-pin Allowed model, Default-choice two-tier bullet dropped, Enforcement + Overmind §5 rewritten; "allowed pair" wording removed); .qwen/SKILLFLOW.md (4 lines); 6 skill files name-only swaps (.qwen/skills/ai-code-detector, coding-challenge-orchestrator, audit-orchestrator, pipeline-doctor, feature-orchestrator, ai-obfuscator — plus dangling "do not run on qwen3.7-plus" clause removed); SYNC-STRUCTURE.md (line 9 divergences bullet, Model-pool row, Kimi→Qwen table rows 3–6 + 13, pre-commit checklist #6 rewritten to "model rows (3–6, 13)"). .qwen/settings-snippet.jsonc contains no model references — nothing to change. All 4 ACs verified: old-pin grep = 0 hits across .qwen/ + SYNC-STRUCTURE.md; 26/26 agent pins = qwen3.8-flash exactly one each; checklist #7 YAML grep empty; counts unchanged (26 agents / 29 skills on all four sides). Quirks beyond the contract's enumerated scope, forced by AC #1 (0 hits): Kimi→Qwen table rows 5–6 and 13 also carried old pins and were updated; skill-body model-name substitutions were in-scope as pin changes (no skill logic touched); stale counts "23 agent files + 26 skill dirs" in checklist #4 text are wrong on ALL four sides equally (actual 26/29) — four-side doc fix, out of this task's file scope, left as-is. No git commit/push.
- 2026-09-28 [agent] — исполнение завершено, все 4 AC верифицированы и отмечены — передано на review
- 2026-09-28 [agent] — Orchestrator review passed: old pins 0 hits (was 51 across 28 files), 26/26 agent files carry exactly one model: qwen3.8-flash, QWEN.md two-tier wording dropped, SYNC-STRUCTURE translation rows + checklist #6 updated. Rows 5-6/13 beyond the «rows 3-4» letter = justified deviation to satisfy AC#1, logged. Leftover SYNC-STRUCTURE #4 stale count text (23/26) is naturally covered by T7's parity rewrite — noted for T7 dispatch.
</log>
