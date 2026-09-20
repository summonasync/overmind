---
feature: 025
slug: vector-search-fix-wizard
status: done
version: 3
created: 2026-09-20
---

# Feature 025 — Vector search: proxy/model fixes + honest diagnostics + setup wizard

<goal>
Векторный поиск: починить и сделать самодиагностируемым (user request 2026-09-20 + live-диагноз сессии).
Живой диагноз 0.1.8 (доказан на машине юзера):
1. БАГ: Qdrant gRPC-клиент уважает системный прокси из env (http_proxy=127.0.0.1:2081) — gRPC на 127.0.0.1:6334 уезжает в прокси и умирает → вечный vectorSearch:"down". MCP-адаптеру тот же фикс уже делали (коммит 7394389, UseProxy=false) — Qdrant/Embedding-клиентам не доложили. Рестарт хоста без прокси-env → instantly "up".
2. БАГ/долг: OllamaEmbeddingProvider хардкодит модель all-minilm-l6-v2 (384 dims) — у юзера nomic-embed-text (768 dims), Ollama отвечает "model not found"; модель ненастраиваема вообще.
3. UX-долг: диагностики нет — health проверяет только Qdrant (эмбеддинг не проверяется вовсе); 503 на semantic врёт ("vector store is down" при ошибке эмбеддинга); Qdrant endpoint отсутствует в Settings UI; причина down логируется на Debug (невидимо); после смены провайдера нет reindex (CONTEXT.md обещает — doc drift).
Решение юзера: «нужен какой-то визард или как-то проработать тему» + «мы с squeezr в одной лодке — должны быть на одной волне» (общий стек Qdrant+Ollama из соседнего компоуза должен подхватываться без плясок).
</goal>

<requirements>
- idea stub: спека по ходу сессии
- Proxy-фикс: loopback-эндпоинты (Qdrant, Ollama, OpenAI-compatible) ходят МИМО прокси; удалённые base URL сохраняют прокси-поддержку (ADR про remote providers)
- Ollama модель: настраиваемая (embedding.ollama.model), dims не хардкодить — авто-детект по факту первого embed (или настройка), честная обработка dimension change
- Диагностика: /api/health расширяется (qdrant: up/endpoint/error + embedding: provider/model/up/error), баннер показывает причину и кнопку к настройкам, в Settings — Qdrant endpoint + модель Ollama + Test-кнопка (ping обоих с понятным результатом)
- Semantic 503 говорит правду: различает qdrant-down vs embedding-failed
- Reindex-подсказка/триггер после смены провайдера/модели/эндпоинта
</requirements>

<non-goals>
- Docker-оркестрация (не мы стартуем контейнеры — ADR 001/05)
- Автодетект чужих compose-стеков (squeezr) — просто корректная работа с уже поднятыми сервисами
</non-goals>