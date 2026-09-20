---
feature: 016
slug: backlog-import-hardening
status: done
priority: p2
version: 1
created: 2026-09-19
---

# Feature 016 — Backlog import pipeline hardening (Mindlash + Squeezr dogfood findings)

<goal>
Харденинг backlog-import pipeline по двум независимым догфуд-репортам 2026-09-19: (A) note «Dogfood: MindlashStudio backlog import findings» (entry 982d4d8a, проект Overmind) — первая полная миграция внешнего проекта через --import-backlog → materialize (3 фичи / 37 тасок, repro /mnt/Ubuntu-Dev/Projects/AI-Tools/MindlashStudio, коммиты 8501a30..392b339); (B) устный репорт агента из Squeezr про import-CLI. Всё работает end-to-end, но 9 находок.
</goal>

<requirements>
A. MindlashStudio import findings (6):
- A1. Silent frontmatter loss: importer мапит только status/review/version/created; depends_on, phase, decision, created_during дропаются БЕЗ skip-записи в отчёте (rule-9 structured deps теряются, данные только в git history)
- A2. Title double-prefix on round-trip: ResolveTitle стрипит только «Feature NN — » (em-dash); заголовок с двоеточием («Feature 001: Mindlash Studio») выживает → материализованный H1 = «# Feature 001 - Feature 001: Mindlash Studio»
- A3. Task filenames not round-trip-stable: NN-short-slug.md регенерятся из заголовка таски → первый materialize ДУБЛИРУЕТ весь набор тасок вместо replace-in-place (связано с известной ниткой из 006/10)
- A4. Duplicate NN- prefixes: silent last-wins (01-report.md + 01-wrapper-spike.md) — побеждает алфавитно-поздний, второй исчезает без skip-записи (оба были реальным контентом)
- A5. created дефолтит в день импорта, если в источнике нет created-токена (у них был created_during) → 37 тасок с датой 2026-09-19
- A6. Первый materialize требует force:true без наводки из import-отчёта (Overwrite exception на каждый untracked файл); import мог бы пре-сеедить PlanningMaterializedFile rows или отчёт должен говорить «run materialize --force»
- A7. Drift false-missing после смены slug-генерации (user report + live evidence 2026-09-19, проект Overmind): drift показывает 12 «missing» для BACKLOG/008-dogfood-hardening/tasks, но файлы на диске ЕСТЬ — tracked-имена с кириллицей (`01-008-01-import-cli-построчный-skipped-лист.md`) против дисковых транслитерированных (`01-008-01-import-cli-per-line-skipped-list.md`): 008 починил слаг-генератор ПОСЛЕ того, как файлы были затреканы, tracked name ≠ disk name → missing + untracked-пары. Та же семья, что A3 (filename round-trip instability) — нужна миграция/reconcile tracked-имён или детект «тот же файл под новым именем» (по содержимому/hash)
B. Import-CLI findings от агента Squeezr (3):
- B1. Второй инстанс с --import-backlog перезаписывает host.port своим fallback-портом → новые MCP-сессии уходят не туда (host.port = дверь для адаптера, ApiBaseUrl)
- B2. import-CLI после импорта boot'ится как полноценный Host вместо run-and-exit — для скриптов неудобно
- B3. Отчёт импорта хочется дублировать в файл, не только stdout
</requirements>

<non-goals>
- Переделка формата BACKLOG (только импорт/раунд-трип харденинг)
</non-goals>