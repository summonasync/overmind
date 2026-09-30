---
feature: 087
slug: attention-page
status: validated
created: 2026-09-29
validated: 2026-09-29
---

# Result 087 — Attention-страница в левом меню: все вопросы и attention агентов в одном месте (не только колокольчик)

## Outcome

3/3 тасок done с ревью; фича живёт в рабочем дереве (uncommitted, ждёт волну):

- **01 Backend (done, review-087-01 PASS-WITH-NITS):** `?awaiting=true` (предикат `ReplyText == null && Kind IN (approval, question) && HiddenAt == null`, EF-translated) + take-параметр 1..500 (дефолт `ListCapacity`=50 — старые вызовы байт-совместимы; #140 закрыт); read-only `GET /api/attention/ceremonies` — один SQL-запрос (done режется в WHERE), причины из существующих полей (spec/plan/result awaiting validation, blocked by N), ноль миграций; url через `NotificationLinks.Dashboard` (реюз 093, не третья копия). Api 501 → 508 (+7 пинов).
- **02 Web (done, review-087-02 PASS-WITH-NITS):** страница `/attention` — AwaitingSection (server ?awaiting + клиентский гард, inline approve/reject/reply через shared NotificationActions, диплинки, «All clear»-empty), CeremoniesSection (группы по проектам, reasons-бейджи, коллапс при пустоте), History = notification-center (чипы/×/read/clear/clear-conversations/show-hidden нетронуты). Bell ужат до unread-only + «Open Attention», действий нет; пункт «Attention» + бейдж в sidebar/mobile-nav; notification-row несёт sr-only «Archived» (закрыт 097-02 SUGGESTION). Web 40 → 48 (+8 пинов).
- **03 Closeout (done):** live e2e на изолированном хосте + architect light + фикс-раунд + result.

## Verification

- dotnet build 0/0; полный сьют **1657/1657** (Core 225, Infra 535, Api 509, Mcp 388) — три независимых прогона (coder, reviewer 01, architect) + финал после фикс-раунда.
- Web: lint 0 errors (6 притащенных warning-ов в нетронутых файлах), tsc clean, **48/48**, static build green с `/attention`.
- **Live e2e** (isolated host: HOME=/tmp/overmind-087-e2e, порт 4897, kill по PID 679135; прод 4761 цел): (1) project+question агент-дверью → url-штамп 093 в ответе; (2) `?awaiting=true&take=200` → вопрос в awaiting; (3) take=0 → 400; (4) POST reply → строка УШЛА из awaiting (084-инвариант серверно); (5) фича 001 + spec draft → ceremonies: `['spec awaiting validation']`, number 001, диплинк `?project=&feature=`; (6) done-фича исключена. UI-слой (bell unread-only, awaiting-инпуты, ceremonies-рендер) — юзер клик-чек при выкладке.

## Recorded decisions

- **Read-but-unanswered остаётся в awaiting** (арбитраж review-087-01): предикат не содержит ReadAt по валидированной спеке — «прочитано ≠ обработано»; иначе read-all из 096 молча дренил бы «Waiting on you». Запинено (`readUnanswered` в ожидаемом массиве пина); escape hatch — одна строка предиката + правка пина.
- **notification-center физически остался в widgets/app-shell** (obstacle кодера): его рендерит бабл 084 (anti-scope), а widgets не может импортить из pages. «Переезд» — на уровне поверхности: bell теряет центр, страница хостит его через баррел `@/widgets/app-shell` (после фикс-раунда архитектора). Двухдомность легитимна (pages→widgets — declared-legal направление).
- **notification-href переехал в features/notifications/lib с однострочным шимом** на старом пути — единственный потребитель шима байт-нетронутый approval-bubble.tsx. Срок жизни шима связан с будущей миграцией баблов (microbatch-заметка).

## Fix round (post-architect, в дереве)

Architect APPROVED-WITH-WARNINGS (audits/architect-review-2026-09-29-087.md): WARNING-1 (первый features→features импорт: notifications→planning через featureNumber) и WARNING-2 (deep import мимо баррела) закрыты фикс-раундом оркестратора по предложенным путям: `feature-number.ts` → `shared/lib/feature-number.ts` (все импорты planning переведены, мёртвый ре-экспорт из баррела убран, FeatureSlugLabel импортит shared), `NotificationCenter` экспортирован из `@/widgets/app-shell`. SUGGESTION-3 закрыт canary-пином `Ceremonies_EveryReturnedRow_CarriesAtLeastOneReason` (инвариант «inclusion ⇒ ≥1 reason» между SQL- и memory-половинами). Гейты после раунда: build 0/0, Api 509/509, web 48×48/lint 0e/tsc/build зелёные.

## Follow-ups (микробатч)

- #146 (NIT-ы 02): BellPreviewRow-split из bell-файла; focus-visible-ring на «Open →»-ссылках awaiting/ceremonies; мёртвый баррел-экспорт listNotifications/params; единый кэп бейджа (99+ vs 50+).
- #147 (SUGGESTION-4, user-gated): reasons — English UI-copy в Infrastructure, wire-identity для web-ключа; триггер миграции на `CeremonyReason` enum в Core — второй потребитель (095 OS-тосты, MCP-дверь) или неанглийский UI.
- #148 (SUGGESTION-5): web-пины wire-шейпа 087 — URL `awaiting`+`take` + fixture-шейп ceremonies-ответа (зеркало одностороннему бэкенд-пину).
- Awaiting-латентность: строка уходит из «Waiting on you» через ~1 RTT (оптимистика патчит list-ключ); кандидат — `setQueriesData` по всей семье (097-02 patchList-прецедент).

<log>
- 2026-09-29 [agent] — result drafted after architect APPROVED-WITH-WARNINGS + fix round + live e2e; feature moves to the user's result-validation gate.
</log>