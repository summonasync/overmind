---
feature: 045
slug: artifact-version-diff
status: draft
priority: p2
version: 1
created: 2026-09-22
---

# Feature 045 — Artifact version diff: что именно изменилось между версиями — лог говорит почему, не показывает что

<goal>
Версии артефакта инкрементируются, лог фичи записывает что и почему поменялось — но не показывает, что именно изменилось в теле. Для проекта, стоящего на правиле «tombstone rather than delete», история — субстанция, не метаданные: `git log -p` на спеке отвечает на «почему так решили», у артефакта эквивалента нет. Агент, поднимающий фичу позже, не видит историю правок так, как её показывает `git diff`.
</goal>

<requirements>
- Инструмент или параметр чтения: diff двух версий артефакта, минимум «последняя vs N-1».
- Формат секционный или unified-подобный — на усмотрение спеки, но компактный: diff по определению меньше сравниваемых тел.
- Доступ через MCP-адаптер (агент — основной потребитель).
</requirements>

<non-goals>
- Web UI для diff — вторым шагом по полевому спросу.
- Git-интеграция/экспорт истории в репозиторий.
</non-goals>

<refs>
- FEATURELIST/03-tool-description-gaps.md, FEATURELIST/08-migration-field-report.md — оба независимо назвали.
- FEATURELIST/02-overmind-vs-files.md — «git log -p is "why was this decided"» — то, чего не хватает артефактам.
- README FEATURELIST п.6.
</refs>