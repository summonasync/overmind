---
feature: 048
slug: mcp-json-escaping-slimming
status: inProgress
priority: p2
version: 1
created: 2026-09-22
---

# Feature 048 — MCP JSON escaping slimming: юникод-эскейпинг бэктиков и угловых скобок раздувает payload на ~25–30%

<goal>
Сериализация MCP-ответов эскейпит бэктики и `<`/`>` в юникод-последовательности — шесть символов там, где в исходнике один. Таск-контракты плотно набиты и тем и другим (XML-секции и code spans), на полевом корпусе это ~25–30% раздувания payload при нулевой информационной выгоде. Прямое продолжение линии «худых ответов», начатой 037.
</goal>

<requirements>
- MCP-слой отдаёт JSON без unicode-escaping этих символов (EnsureAscii=false или кастомный encoder).
- Web API не трогаем: браузеру escaping невидим, раздувание критично только в LLM-контексте.
- Регресс-тест: payload репрезентативного task-контракта не содержит `\u00XX`-последовательностей для этих символов.
</requirements>

<non-goals>
- Изменение самих данных/тел — только транспортная сериализация.
- HTTP-эндпоинты web UI.
</non-goals>

<refs>
- FEATURELIST/08-migration-field-report.md — главный документ: «six characters where the source had one».
- README FEATURELIST п.14.
- 037-mcp-echo-slimming — сосед по той же цели: 037 срезал состав ответа, 048 — его кодировку.
</refs>

<log>
- 2026-09-23 [auto] — artifact created → inProgress
</log>
