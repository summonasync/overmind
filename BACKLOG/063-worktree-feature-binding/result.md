---
feature: 063
slug: worktree-feature-binding
status: validated
created: 2026-10-05
validated: 2026-10-05
---

# Result 063 — Worktree/branch ↔ feature binding: agents locate task code and status super fast

Волна закрыта: один MCP-вызов отвечает «где код этой фичи» — discovery adapter-side (хост не видит машину агента), матчится по slug и NNN-токену с границами, stale-worktree репортится, ride-along статуса с хоста в одном ответе. 2/2 таска; архитектор light **APPROVED** (audits/architect-073-063.md, волна B) — W-B1 (workspace-аффинность) закрыт до result'а.

## Что село

- **01 — тулза** (8aa947b + фиксы 4eac860/5bb659f): IGitCli seam (Process, 10s timeout, kill process tree на ЛЮБОМ cancellation-пути — ревью-W1), porcelain/for-each-ref парсеры (сверены с живым git смоуком), матчинг: slug-substring OrdinalIgnoreCase + NNN-токен с границами (`1063-` не матчится — негатив запинен; digit-only slug = substring-only), detached резолвится через валидированный hex + git log -1, bare → null. Worktree и его ветка листаются без дедупа (спека: все совпадения, ничего не угадываем). Ride-along: GET фичи падает независимо → discovery-половина честна (featureError).
- **02 — клоун** (5bb659f): архитектор light **APPROVED**: W-B1 закрыт — тулза отвечает `workspaceMatchesProject` (workspace vs diskPath проекта) вместо молчаливого описания чужого репо; направление зависимостей чистое, шов IGitCli признан правильным домом.

## Гейты финального дерева

dotnet build 0 warnings; последовательно: **Mcp 486 (+22), Api 642, Infra 659, Core 350**, format clean. Инъекций нет: слаг не доходит до git argv — в аргументах только константы и ограниченный 4–64 hex sha.

## Post-release click-check

1. В репо с веткой `feat/063-worktree-feature-binding`: `planning_feature_code("063-worktree-feature-binding")` → matches с hash/датой, feature-половина со статусом.
2. Worktree с удалённым путём → `stale:true`, не выброшен.
3. Слаг без совпадений → honest empty + hint про `feat/NNN-slug`.
4. Запуск из чужого репо с явным projectId → `workspaceMatchesProject:false` — сигнал виден.

## Остаточное

- Микробатч: #231 (три копии ResolveProjectIdAsync + onboard-create ложь — шов ProjectResolver, поднята форма), N1 (single-match git-log failure деградировать в null), N2 (hint несёт чужой пример), N-X1 (handshake «FortyThree» при 44).
- Осознанные: C-quoted экзотические пути → stale=true (v1), worktree+branch без дедупа (спека), реальный git в тестах не шеллится (StubGitCli).

Коммиты волны: 8aa947b, 4eac860, 5bb659f.

<log>
- 2026-10-05 [agent] — Result drafted: 2/2 tasks done (01 PASS-WITH-NITS + W1/W2 security fixes in 4eac860, 02 closeout with architect APPROVED + W-B1 workspace affinity fixed in 5bb659f). Gates: Mcp 486, Api 642, build 0w. Post-release click-check inside. Waiting for the human tap.
</log>