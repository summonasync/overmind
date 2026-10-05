---
feature: 029
slug: entry-tombstones
status: validated
created: 2026-10-05
validated: 2026-10-05
---

# Result 029 — Entry tombstones: тип записи + автозахват при удалении + список для агентов перед работой

Волна закрыта: удаление записи хоронит её в надгробии (title+« (deleted)», исходный тип, дата, провенанс двери/актора, опциональный reason, финальный контент в фенс-блоке) — агенты больше не ресерректают мёртвое из стейл-контекста. Tombstone — полноправный пятый гражданин: searchable, listable, writeable, editable; могила могилы не рождает. 3/3 таска; архитектор light APPROVED-WITH-WARNINGS, оба W закрыты до result'а.

## Что село

- **01 — Core+Infra** (ad68f6e + фикс f29bdb7): конверсия атомарна (один SaveChanges), фенс размеряется больше собственных бэктик-ранов контента И reason (ревью-W1), рекурсия отсечена в API-ветке до энтити-гварда, FTS-триггеры+Qdrant подхватывают могилу в той же транзакции, старый вектор гасится по ИСХОДНОМУ id. Контракт delete 204→200 (additive, оба клиента толерантны — верифицировано ревьюером по парсерам).
- **02 — двери** (6480a67 + ee8b539 + фикс d552c2c): MCP — reason в delete_entry (blank → без query-параметра), tombstone в list/write admission, честные описания; Web — могилы в фильтрах/брейкдауне/редакторе, muted-тон+Skull, delete-диалог говорит правку захоронения, reason-инпут для живых записей (спека победила план — рулинг ревьюера, дотянуто фикс-раундом). Два параллельных light-ревью (lanes disjoint), оба PASS-WITH-NITS.
- **03 — closeout** (8445289 + 95ce4c1): quickstart-строка «check tombstones before resurrect» (запинена), update_entry-описание принимает tombstone (N2 ревью), архитектор light **APPROVED-WITH-WARNINGS** (audits/architect-029.md): bury-not-erase держит на всех достижимых путях (греп всех Remove*/MapDelete — обходных нет), W1 — ServerInstructions знали старый набор типов (заклено), N1 — reason-параметр честен на tombstone-целях (заклено).

## Гейты финального дерева

dotnet build 0 warnings; последовательно: **Core 345 (+7), Infra 639, Api 631 (+6), Mcp 459 (+4), format clean**; web: lint 0e/6w pre-existing, tsc 0, **тесты 150 (+12)**, build exit 0 (11 статик маршрутов; в ранних отчётах фигурировало «12/12» — мискаунт, зафиксирован архитектором как N3, маршруты волна не трогала).

## Post-release click-check

1. MCP: `delete_entry` с reason на живой записи → 200-эхо; `list_entries type=tombstone` → могила видна (тайтл «… (deleted)»); `read_entry` — шапка с провенансом, reason, финальный контент в фенсе.
2. Поиск (FTS/семантика) находит могилу по её финальному контенту.
3. Удалить могилу → plain delete, второй могилы нет.
4. Веб: удалить запись с reason в диалоге → строка переворачивается в muted-могилу с «… (deleted)»; могилы в фильтре и брейкдауне.
5. Онборд-квикстарт несёт «check tombstones» строку; server instructions упоминают tombstone.

## Остаточное

- Микробатч: N1 (reason без лимита), N2 (title 510 поверх правила 500), веб-W2 (**контраст --color-muted 3.76:1 против AA — системный токен всего аппа, дизайн-колл юзера** #221), N1-N3 веб (титул модалки, хрупкий пин, сырой провенанс), MCP-N1 (echo мог бы нести tombstoneId).
- Осознанные: KitImporter не конвертирует (lifecycle-sync, kit-owned и так отказ), версии не мигрируют (fenced-тело — память), echo без id могилы (дискавери через type=tombstone запинен в описании).

Коммиты волны: ad68f6e, f29bdb7, 6480a67, ee8b539, d552c2c, 8445289, 95ce4c1.

<log>
- 2026-10-05 [agent] — Result drafted: 3/3 tasks done (01 review PASS-WITH-NITS + W1 fence-over-reason, 02 double-lane reviews PASS-WITH-NITS + W1 both lanes fixed, 03 closeout with architect APPROVED-WITH-WARNINGS — W1/N1 closed in 95ce4c1). Gates: Core 345, Infra 639, Api 631, Mcp 459, web 150, build 0w. Post-release click-check inside. Waiting for the human tap.
</log>