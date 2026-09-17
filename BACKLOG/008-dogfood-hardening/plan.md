---
feature: 008
slug: dogfood-hardening
status: validated
created: 2026-09-16
validated: 2026-09-16
---

# Plan 008 — Dogfood hardening: importer gaps + web UX from 0.1.0

<approach>
Три независимых пласта, порядок — от бэкенда к фронту к релизу: (1) importer/CLI-правки в Infrastructure + Host (IMP-1…IMP-5, все в одном bounded context, тесты Infrastructure + Host); (2) web-косметика (UI-3, UI-4 в shared markdown + app-shell); (3) PERF-1 одной строкой в EntriesApi; (4) closeout: docs, репак 0.1.1 с bundle-version 2, живой догфуд upgrade-sync. Каждый пласт — отдельная таска с своим build/test-гейтом, чтобы ревью шли мелко.
</approach>

<tasks>
- [ ] 01-import-cli-skipped-list — IMP-1: BacklogImportCommand печатает report.Skipped построчно; Host-тест на вывод → tasks/01
- [ ] 02-import-frontmatter-and-status-mapping — IMP-2 + IMP-3: срез инлайн-комментариев, approved → validated для spec/plan; тесты парсера и маппинга → tasks/02
- [ ] 03-import-result-md — IMP-4: result.md как артефакт kind=result, round-trip с материализатором; тесты → tasks/03
- [ ] 04-import-letter-suffix-tasks — IMP-5: NN+буква в TryParseTaskOrder, append + loud warning; тесты → tasks/04
- [ ] 05-web-sidebar-and-ceremony-preview — UI-3 + UI-4: отделение сайдбара, цветные церемониальные секции в превью; lint+tsc → tasks/05
- [ ] 06-entries-split-query — PERF-1: AsSplitQuery в EntriesApi.ListByProject; ворнинг исчезает из лога → tasks/06
- [ ] 07-closeout-docs-release-011 — CONTEXT.md + ADR-поправки, pack 0.1.1 --bundle-version 2, живой догфуд upgrade-sync → tasks/07
</tasks>

<risks>
- IMP-4: result-артефакты раньше не импортировались — у существующих БД (наша боевая) result.md отсутствует на диске; повторный импорт после фикса не должен плодить дубли (идемпотентность по Kind/Order). Mitigation: тест на повторный импорт.
- IMP-5: сдвиг order при коллизии меняет позицию существующих тасок при ре-импорте — принимаем: re-import идемпотентен по (Kind, Order), новые order назначаются только новым файлам.
- UI-4: превью используется в entries, artifacts, tasks — раскраска секций должна быть в shared markdown-компоненте, чтобы не плодить три копии (см. нитку про Write/Preview toggle из 006/09).
- Репак 0.1.1 перезапишет ~/apps — миграции и upgrade-sync съедят живой профиль; перед репаком снапшот ~/.overmind.
</risks>

<verification>
- dotnet build + dotnet test Overmind.sln зелёные (suite 526+ новых)
- npm run lint && npx tsc --noEmit && npm run build в web/ чистые
- Живой прогон: --import-backlog на этом репо печатает skipped-лист; 01b импортируется с warning; spec/plan с approved → validated; result.md round-trip
- Живой догфуд: upgrade-sync при bundle-version 2 на боевом профиле, ворнинга EF нет в логе хоста
</verification>