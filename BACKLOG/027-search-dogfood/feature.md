---
feature: 027
slug: search-dogfood
status: done
version: 1
created: 2026-09-20
---

# Feature 027 — Search dogfood: semantic + fulltext systematic pass

<goal>
Систематический догфуд-проход по поиску Overmind (semantic + fulltext) с закрытием находок: UX-поверхность ошибок, empty states, качество сниппетов/ранжирования, поведение при недоступных ногах (Ollama/Qdrant), round-trip настроек модели. Триггер: первый живой semantic-запрос юзера 2026-09-20 обосрался голым «search failed».
</goal>

<контекст>
2026-09-20: юзер впервые включил semantic mode в вебе → «search failed». Диагностика с live-хоста (HEAD-билд 4d31643, порт 4761): health ok, vectorSearch up, но semantic search отдаёт честный 503 из 025/01 — Embedding provider 'ollama' failed for model 'all-minilm-l6-v2': 404 Not Found (в Ollama юзера модели нет, у него nomic-embed-text/768; настройка модели в UI — это как раз недоделка 025/02). API-слой честный, UX-слой глухой: detail 503 (provider/model/error + подсказка) не доезжает до юзера.
</контекст>

<правила прохода>
- Каждый пункт прохода = либо закрытая находка (таска + фикс + ревью), либо осознанное «ок, так и надо» с записью в log.
- Проход идёт ПОСЛЕ выката 025/02 (настройки модели + Test-кнопка + reindex) — тестить семантику до конфигурируемой модели бессмысленно.
</правила прохода>

<log>
- 2026-09-20 — by user request in-session («поехали закроем 027», 2026-09-20) — first delegated close via the agent door (023/08): task 027/01 done (SearchError honest 503 detail + settings link, review PASS), result artifact validated. Founding incident closed end-to-end; systematic dogfood pass continues opportunistically.
</log>
