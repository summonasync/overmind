---
feature: 008
task: 04
slug: dogfood-hardening
status: todo
review: light
created: 2026-09-16
---

# 008/04 — Import: letter-suffixed task files (01b)

<goal>Таски с буквенным суффиксом (01b) импортируются: базовый order=NN, при коллизии — append в конец (max+1) с loud-записью в отчёте. Молчаливый дроп устранён.</goal>

<context>
Фича 008-dogfood-hardening (dogfood 0.1.0 findings). Субагент читает только этот файл. Спека validated: решения IMP-1..IMP-5, UI-3/4, PERF-1 зафиксированы в ней; ревьюер читает @../spec.md.
</context>

<scope>- src/Overmind.Infrastructure/Planning/BacklogNaming.cs — TryParseTaskOrder принимает NN + одну букву
- BacklogImporter.cs — назначение order при коллизии + запись в отчёт
- Тесты: 01b без коллизии, 01b при наличии 01 (append + warning)</scope>

<anti-scope>- Переупорядочивание существующих тасок в БД запрещено
- Формат имён материализатора не меняем</anti-scope>

<constraints>
- .NET 10 / C# 14 (таски 01-04, 06), Next.js 16 + Tailwind 4 (таска 05)
- Новые пакеты — только через nuget-licensing-guard / явное одобрение пользователя
</constraints>

<acceptance-criteria>
- [ ] Тест: 01b при занятом order 1 получает max+1 и loud-запись в отчёте
- [ ] Тест: 01b без коллизии получает order 1
- [ ] dotnet test зелёный
</acceptance-criteria>

<refs>
- @../feature.md — what/why фичи
- @../spec.md — decisions IMP/UI/PERF (ревьюер)
- @../plan.md — подход и риски (ревьюер)
</refs>

<obstacles>
</obstacles>

<log>
</log>