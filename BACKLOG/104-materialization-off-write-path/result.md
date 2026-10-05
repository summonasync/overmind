---
feature: 104
slug: materialization-off-write-path
status: validated
created: 2026-10-05
validated: 2026-10-05
---

# Result 104 — Materialization off the write path + honest timeout errors (Qwen friction #3)

Волна закрыта: мутации больше не ждут зеркало (очередь в хосте), отказ пасса не ванишится (074), таймаут мутации превращён адаптером в честный «committed / no trace / unknown». 4/4 таска done с ревью; архитектор light APPROVED-WITH-WARNINGS — все находки закрыты до result'а.

## Что село

- **04 — рендер-баг зеркала** (внеполосно, первым): корень — агенты эхотят wrapper-теги из зеркала в патчи; Replace-sanitize + Normalize-heal (509a7ca), зеркало самолечится. Review PASS-WITH-NITS (отчёт живёт в таск-логе 04 и микробатче #197-201 — отдельного audits/review-104-04.md нет, указатель здесь по N3 архитектора).
- **01 — async-пасс** (fe0119a + фикс 7366ff1): bounded Channel + sequential worker, enqueue-or-inline по `planning.materializationSync` (дефолт off), импорт всегда inline, 21 call-site, мёртвые IPlanningMaterializer-зависимости вычищены. Review PASS-WITH-NITS (W1: queue-full fallback отвязан от request-токена).
- **02 — адаптерные таймауты** (cceb4fe + фикс 5189ae6): MutationGuard на 22 мутационных call-site'ах, verify-проба по logNote — хвост лог-секции (многострочные ноты), section-less редбэк = unknown, никогда не «no trace». 6 проб / 16 честных unknown. Review PASS-WITH-NITS.
- **03 — closeout** (499e363): консистенс-пасс — докстринги/онборд несут eventual-семантику, триггер-текст флаг-осведомлён (W3: при sync=on говорит inline, не врёт про фон); воркер перегоняет shutdown-прерванные пассы с живым токеном (W1: гвард-метод TrySyncMaterializationAsync отличает «отменён» от «отработан», пост-фактум проверка токена убита — буферизованные итемы покрыты дизайном); контракт пробы закреплён на дескрипторе (W2). Архитектор light **APPROVED-WITH-WARNINGS** (audits/architect-104.md): W1/W2/W3 вкатаны, N1 оформлен патчем спеки (глобальный флаг v1, «per project» — было неверно), N2 → микробатч, N3 — этот указатель.

## Гейты финального дерева

dotnet build 0 warnings; последовательно: **Core 316, Infra 634 (+1 worker-пин), Api 624 (+1 onboard-пин), Mcp 454 (+8 timeout-drill пинов за волну)**; format clean по всем тронутым проектам. Web не затронут.

## Post-release click-check

1. Любая мутация с борды → ответ приходит без ожидания зеркала; BACKLOG-файл догоняет за секунды (watch: `git status` в проекте-потребителе).
2. `planning_materialize` → синхронный отчёт, ждёт завершения (это его контракт).
3. PUT settings `planningMaterializationSync: "on"` → та же мутация ждёт пасс inline; онборд-текст при этом говорит «runs the mirror pass inline».
4. `planning_set_status` с logNote под обрубленной сессией хоста → при восстановлении адаптер отвечает одним из трёх честных исходов; «no trace» невозможен, когда проба не видит лог.
5. Отказ пасса (сломать путь зеркала) → lastAttempt/lastError светится в онборде, ответ мутации при этом зелёный.

## Остаточное (осознанное)

- Residual N1 (review-104-02): переиспользование идентичного logNote (шаблонные ноты двух агентов) даёт ложное «committed» — низкий риск при claim-дисциплине; строка в дескрипторы пробных тулов → микробатч #210.
- entries_*/notifications/onboard остаются на raw transport-тексте при недоступности хоста — маркеров для пробы у них нет, унификация не добавляет честности (решение клоуз-аута).
- **Спека демотирована в draft** (честный N1-патч frozen-артефакта) — ревалидация едет одним тапом вместе с этим result'ом.

Коммиты волны: fe0119a, 7366ff1, cceb4fe, 5189ae6, 499e363.

<log>
- 2026-10-05 [agent] — Result drafted: 4/4 tasks done (three task reviews PASS-WITH-NITS with W-fixes in-tree, architect APPROVED-WITH-WARNINGS with W1-W3 fixed in 499e363, N1 spec-patched). Gates: Core 316, Infra 634, Api 624, Mcp 454 sequential, build 0w. Post-release click-check inside; residuals recorded (microbatch #210/#211). Spec deliberately in draft — re-validation rides the return tap with this result.
</log>