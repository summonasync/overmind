---
feature: 004
task: 08
slug: missing-language-coders
status: done
review: light
created: 2026-09-27
brief: Author python trio on master
---

<goal>Author the python trio on master .kimi-code: python-coder + python-code-reviewer (skills + agents) + python-best-practices (skill, no agent).</goal>
<scope>- python-coder SKILL.md + agent: senior Python — uv/poetry packaging, ruff, mypy strict, pytest, asyncio; typing discipline; structured logging; no-comments, English-only, decomposition. ML-context note: PyTorch references where relevant (user learns ML/DL); prefer ONNX Runtime when inference lands in .NET. Anti-scope: JS/TS (→ web/node coders), C# (→ dotnet-coder), Go (→ go-coder); routing table + output contract.
- python-code-reviewer skill + agent: mirrored checklist (typing, error handling, security, dependency hygiene, test quality).
- python-best-practices skill: BP-ID catalog, initial top rules. NO agent.</scope>
<anti-scope>No port sides; registration sweep folded into T14; no BACKLOG//audits/.</anti-scope>
<constraints>English-only; NO git commit.</constraints>
<acceptance-criteria>
- [x] kimi counts: agents=30, skills=37; YAML grep #7 empty; bp skill agentless
</acceptance-criteria>
<refs>Spec §Phase B items 10–12; Plan T8; port side depends on this task (T9).</refs>
<obstacles></obstacles>
<log>
- 2026-09-28 [agent] — взята в работу исполнителем
- 2026-09-28 [agent] — AC-секция была одной строкой с тегами (парсер не видел пунктов, patch 400) — нормализована полным upsert, order=8 сохранён
- 2026-09-28 [agent] — Verified: agents=30 skills=37 on .kimi-code; YAML grep #7 rc=1 (empty); no python-best-practices agent; all 5 frontmatters parse valid; leakage greps clean; zero edits to existing files.
- 2026-09-28 [agent] — Python trio authored on .kimi-code master: skills/python-coder (routing + uv/ruff/mypy-strict/pytest/asyncio stack + ML PyTorch/ONNX section + output contract), agents/python-coder, skills/python-code-reviewer (independence framing + 6-block checklist), agents/python-code-reviewer (tools listed), skills/python-best-practices (BP-01..BP-63 catalog, agentless). Verified per plan above; Go routed to go-coder pending Phase C.
- 2026-09-28 [agent] — Работа выполнена: 5 файлов авторены, все проверки зелёные, AC затикана — на ревью.
- 2026-09-28 [agent] — Orchestrator review passed: python trio authored (coder with uv/ruff/mypy/pytest-asyncio stack + ML/DL section: PyTorch discipline + ONNX export for .NET consumption per global AI/ML rules; reviewer with independence framing + 6-block checklist; BP catalog BP-01..63 in decades), counts 30/37 exact, YAML clean, agentless bp confirmed.
</log>