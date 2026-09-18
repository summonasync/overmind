---
feature: 009
slug: mcp-registration
status: done
version: 2
created: 2026-09-16
---

# Feature 009 — MCP registration command for existing agents

<goal>
CLI-команда (кандидат: overmind-mcp install), которая регистрирует MCP-адаптер Overmind в конфиги существующих агентов (kimi-code, claude, codex, gemini, antigravity, qwen) — вместо ручного редактирования mcp.json/settings, как мы делали при догфуде 0.1.0. Из deferred P2/P3 agent onboarding.
</goal>

<requirements>
- idea stub: обсуждается, спека не начата
- scope project/user, идемпотентность, обновление пути при апгрейде версии
- install-layout: стабильный «current»-линк (~/apps/overmind -> overmind-<ver>-<rid>) как живое место инсталляции — mcp.json/автостарт/регистрации агентов ссылаются на него и переживают апгрейды (принято в догфуде 0.1.1, 2026-09-17)
- мапа конфигов per-агент (JSON vs TOML, разные пути) — вероятно рядом с platforms.json
</requirements>

<non-goals>
- Запуск агентов (agent launcher) — это P1
- Runtime-доставка скиллов через MCP — это P2
</non-goals>