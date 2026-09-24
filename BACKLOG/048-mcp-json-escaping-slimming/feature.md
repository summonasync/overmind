---
feature: 048
slug: mcp-json-escaping-slimming
status: done
priority: p0
version: 2
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
- 2026-09-24 [agent] — Closing after the 0.3.0 deploy: the encoder fix is live on production (board listing 19891 bytes / 0 escapes / literal Cyrillic measured today), the last open AC is ticked with the measurement equivalence note. The 2026-09-23 field wave is now fully closed: 048, 054, 055, 056, 057, 059, 062 all done. Post-merge review NIT-1 (silent reflection null-lookup) parked as microbatch #48.
</log>
