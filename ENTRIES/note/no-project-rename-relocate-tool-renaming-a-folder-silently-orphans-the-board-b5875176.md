---
id: b5875176-9c9e-4e07-bd06-2858f17320a2
type: note
title: No project rename/relocate tool — renaming a folder silently orphans the board
tags: capability-gap, migration, onboarding, project-rename
createdAt: 2026-09-30T15:49:40Z
updatedAt: 2026-09-30T16:29:47Z
---

2026-09-30, Qwen Code (dotNetCrew main agent), сессия в проекте `WoWMemeGenerator` (`7352b1ea-dd6c-44e4-93c4-496bb1164a19`).

**Чего нет:** ни одного инструмента, который меняет `name` или `diskPath` уже зарегистрированного проекта. Ростер overmind даёт `list_projects` (read), `overmind_onboard` (resolve-or-create из cwd), CRUD над entries и planning-артефактами — и всё. `delete_entry` явно ограничен notes/decisions/gates/guards. Project rename/relocate отсутствует и в описаниях, и в схемах.

**Что это причиняет:** `overmind_onboard` резолвит проект по рабочему каталогу и **создаёт проект, если не нашёл**. Значит обычный для этого пользователя шаг — переименовать папку продукта — не переименовывает проект, а **осиротили борд**: на следующем онборде поднимается пустой проект с новым id, а старый борд со всеми фичами остаётся висеть на мёртвом пути.

Второй, более тихий урон: **entries миграции не переживают** — notes/decisions/gates/guards живут только в БД (материализуются только `BACKLOG/*/*.md`), их можно только перепечатать `read_entry` → `write_entry` вручную, теряя createdAt и историю.

## EXECUTED today — the workaround was carried out, here is what actually happened

Renamed `/mnt/…/AI-Tools/WoWMemeGenerator` → `…/Chadline` and rebuilt the board in the same session. Verified outcomes, not theory:

- **`overmind_onboard` followed the moved directory** (MCP config carries only `{command, trust}` — no cwd), and created `Chadline` = `2559862f-14cf-42c0-878d-035402f88c84` with `targetDir` on the new path. No phantom folder was recreated on disk.
- **`planning_import_feature` is a solid migration vehicle:** 4 calls restored 8 artifacts + 6 tasks, statuses verbatim (`inProgress` stayed `inProgress`), priorities verbatim (p1 stayed p1), and **feature `depends_on` and task `blockedBy` edges all re-linked from `restored from imported file` notes** — I expected to re-add three edges by hand and did not need to. `materialize is a no-op` in the response is a good board-vs-disk equality signal.
- **Entries had to be re-created by hand** (2 × `write_entry`), and lost their original `createdAt` — new ids, same text; fidelity check available via `contentLength` (4909 → 4909).
- `planning_set_current` schema is **undocumented in the tool description**: it rejects `featureSlug` (the param every sibling planning tool uses) and requires `slug` + `isCurrent`. Two failed calls to discover that.

**The part Overmind cannot warn about, and the real cost:** the *agent runtime* does not follow the move. After `mv`, every shell and file tool call failed — first `chdir(2) failed: No such file or directory` (the default cwd is still the deleted path), then `Directory '…' is not within any of the registered workspace directories` even for the new folder and for `/tmp`. MCP kept working, so a session can finish a board migration and then be unable to verify the filesystem it just migrated. **Post-move disk verification is impossible without restarting the CLI inside the new folder.**

**Чего хочется:** либо `update_project` с полями name/diskPath (плюс каскад в materialization targetDir), либо документированный маршрут миграции вместе с предупреждением в `overmind_onboard`, что «creating it if needed» — это тихое создание дубликата борда при переезде папки, и что runtime cwd после `mv` придётся перезапустить руками. И зафиксировать в описании `planning_set_current` настоящие имена параметров: `slug`, `isCurrent`.