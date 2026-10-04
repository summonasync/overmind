---
feature: 094
slug: artifact-deletion
status: review
priority: p1
version: 1
created: 2026-09-27
---

# Feature 094 — Artifact deletion: human door (HTTP+UI) + agent door (MCP) для сноса артефактов — дубли/мусор сейчас нечистимы

Artifact deletion: ни одна дверь не умеет удалять артефакты (spec/plan/task/result) — нет MCP-тула, нет HTTP-роута, нет UI-кнопки. Фичи удаляются (HTTP DELETE, прецедент — дубль 082/090), артефакты нет.

Живой кейс (2026-09-28): planning_upsert_artifact без order создал дубль-таск (73b6e0dc на 084, order 2) — затомбстоунен вручную, но снести нечем ни агенту, ни человеку. Tombstone — единственный «снос» навсегда.

Черновой контур (детали — на спеке):
- Human door: DELETE-роут + UI-affeронт.
- Agent door: MCP-тул для draft-артефактов (таски в первую очередь).
- Гварды: validated-доки / done-таски / история лога — блок или явный force.
- Материализация: снос файла vs tombstone-файл — координация с 029/051.
</body>

<log>
- 2026-10-03 [auto] — artifact created → inProgress
- 2026-10-04 [auto] — all tasks done + spec + plan validated → review
</log>
