---
feature: 096
task: 01
slug: notification-cleanup
status: done
review: light
created: 2026-09-28
brief: Backend: DELETE {id} + read-all + clear-read (NotificationsService + NotificationsApi) + тесты
---

# Task 01 — Backend cleanup endpoints

<goal>
Human-door housekeeping API: удалить одну нотификацию, прочитать всё, снести все прочитанные — с честными count-возвратами.
</goal>

<scope>
- src/Overmind.Infrastructure/Notifications/NotificationsService.cs: DeleteAsync(Guid id) (204/404 семантика — bool или исключение по конвенции файла), MarkAllReadAsync() (ReadAt=now где null, возвращает marked count), ClearReadAsync() (hard delete где ReadAt != null, возвращает deleted count). Bulk — EF ExecuteUpdate/ExecuteDelete, без материализации сущностей.
- src/Overmind.Host/Api/NotificationsApi.cs, human-группа: DELETE /api/notifications/{id:guid} → 204, неизвестный id → 404; POST /api/notifications/read-all → { marked }; POST /api/notifications/clear-read → { deleted }. Формы ошибок — как у существующих mark-read/reply.
- tests/Overmind.Api.Tests/NotificationApiTests.cs: delete удаляет строку и второй вызов 404; read-all трогает только ReadAt==null и возвращает точный count; clear-read удаляет только ReadAt!=null; replied-нотификация (ReadAt выставлен reply-путью 084) сносится clear-read, непрочитанный approval переживает read-all и clear-read.
</scope>

<anti-scope>
- MCP agent door, retention/TTL, архив, веб-слой (таска 02).
</anti-scope>

<constraints>
- Инвариант 084 reply⇒read не ломать. Проект резолвится существующей конвенцией human-двери. Код по STRUCTURE.md: static Map<> конвенция, no comments, English-only.
</constraints>

<acceptance-criteria>
- [x] DELETE /api/notifications/{id} удаляет строку; повторный вызов 404.
- [x] read-all возвращает { marked } и обнуляет бейдж только реальных непрочитанных.
- [x] clear-read возвращает { deleted } и удаляет ровно прочитанное множество.
- [x] dotnet build зелёный; dotnet test --filter Notification зелёный с новой покрытием.
</acceptance-criteria>

<refs>
- feature 096 spec (4d1ab184) — decisions section.
- BACKLOG/084-notification-human-actions — reply⇒read инвариант.
- src/Overmind.Host/Api/NotificationsApi.cs:13-20 — текущие двери.
</refs>

<obstacles>
<obstacles>
<obstacles>
- 2026-09-28 [coder] — No real quirks. Convention calls made: DeleteAsync returns bool (true → 204, false → HttpErrors.NotFound 404, same shape as mark-read); count responses carried as ReadAllNotificationsResponse(int Marked) / ClearReadNotificationsResponse(int Deleted) in NotificationContracts.cs; bulk paths are EF ExecuteUpdate/ExecuteDelete (first usage in the codebase, SQLite supports both).
</obstacles>
</obstacles>

<log>
- 2026-09-28 [agent] — task cut from validated plan a41a4350.
- 2026-09-28 [agent] — coder took task 01
- 2026-09-28 [agent] — coder implemented delete/read-all/clear-read: 3 service methods (bulk ExecuteUpdate/ExecuteDelete), 3 human-door endpoints, 2 contract records, 4 new tests — build 0 warnings, Notification filter 23/23, full Api suite 492/492.
- 2026-09-28 [agent] — coder done, build+tests green, AC ticked
- 2026-09-28 [agent] — review PASS-WITH-NITS (audits/review-096-01.md, 0 blockers): NIT-1 test rename fixed in-tree by orchestrator (23/23 green re-verified), NIT-2 resolved as spec log-clarification, NIT-3 radar note on service split. Closing done.
</log>