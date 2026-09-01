---
feature: 002
task: 03
slug: finalize-gemini-md
status: done
created: 2026-09-04
---

# 002/03 — Finalize .gemini/GEMINI.md

<goal>
Прототипный `.gemini/GEMINI.md` ревьюится построчно против `.kimi-code/AGENTS.md` и
приводится к финальному виду: ничего не потеряно, ничего Kimi-специфичного не осталось.
</goal>

<context>
GEMINI.md был написан в quick prototype до оформления фичи. Теперь есть утверждённые
feature.md/plan.md — файл сверяется с ними. Это глобальная память Antigravity, аналог
AGENTS.md/CLAUDE.md.
</context>

<scope>
- Diff-проход `.kimi-code/AGENTS.md` vs `.gemini/GEMINI.md`: каждая секция либо перенесена,
  либо осознанно адаптирована/выкинута (список выкинутого — в log с причинами).
- Проверить соответствие translation table из plan.md (формулировки моделей по ролям,
  behavior-правила вместо hooks).
- Убедиться, что секции присутствуют: Communication Style + тон, .NET/Frontend стеки,
  Build commands, Documentation Files table, artifact provenance, skill output contract,
  Delegation Rules, Safety Rules, AI Coding Traces, Development Workflow (10 правил).
- Сверить с `.claude/CLAUDE.md`: уровень адаптации должен быть сопоставим (CLAUDE.md —
  эталон полного порта).
</scope>

<anti-scope>
- Не меняем AGENTS.md / CLAUDE.md.
- Не трогаем скиллы/агентов (tasks 01-02).
</anti-scope>

<constraints>
- Семантика правил не меняется.
</constraints>

<acceptance-criteria>
- [ ] Все обязательные секции на месте
- [ ] `grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm\|config\.toml\|sonnet\|opus\|haiku' .gemini/GEMINI.md` пусто
- [ ] Список выкинутых секций с причинами — в log
- [ ] Изменённые файлы доложены; коммит только после подтверждения
</acceptance-criteria>

<refs>
- @../feature.md
- @../plan.md — translation table
- .kimi-code/AGENTS.md, .claude/CLAUDE.md — источник и эталон
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-04: Полный реврайт прототипа как full port AGENTS.md. Восстановлено из источника: Slang Dictionary + gachi/roast правила, Product & UX placeholder, полный Query Keys пример, AI/ML Context, полная Documentation table (вкл. SOCIAL_DICTIONARY), Rule for agents (TECHNICAL-SOLUTIONS), Coding Challenges, полные формулировки Development Workflow и AI Coding Traces. Адаптировано осознанно: Sub-Agent Rules → Delegation Rules (модели по ролям execution/reasoning, ai-obfuscator never same model); Deterministic Enforcement → Safety Rules (behavior + Antigravity permission settings; «writing to settings.json/hooks» → «IDE/agent config self-modification is human-only»; onboarding mode → strict approval mode); Superpowers Integration → условная («if installed», иначе SKILLFLOW напрямую). Заголовок: «Global Agent Context (Google Antigravity)». Гейт `grep -niE 'k2\.7|\bk3\b|kimi|agentswarm|config\.toml|sonnet|opus|haiku' .gemini/GEMINI.md` — пусто.
</log>
