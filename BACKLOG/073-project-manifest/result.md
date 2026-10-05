---
feature: 073
slug: project-manifest
status: draft
created: 2026-10-05
---

# Result 073 — Project manifest: AGENTS.md-аналог внутри Overmind — MCP-чтение для любого агента, UI-редактор на project-config, импорт agents/claude/qwen.md

Волна закрыта: у проекта есть манифест — полный markdown (стек, команды, конвенции), читаемый любым агентом одним MCP-вызовом, редактируемый человеком на конфиге, импортируемый из AGENTS/CLAUDE/QWEN с записью источника. 3/3 таска; архитектор light **APPROVED** (audits/architect-073-063.md, волна A) — единственный W (дубль списка приоритетов в моём scan-хинте) закрыт до result'а.

## Что село

- **01 — бэкенд** (ea08eac + фикс 121ba0d): миграция AddProjectManifest (TEXT nullable, FTS не задет), setManifest-семантика (absent/false сохраняет — легаси-клиенты целы; true+текст пишет; true+null чистит), импорт AGENTS→CLAUDE→QWEN first-found с header-строкой и PathGuard containment, MCP read_project_manifest (projectId | env | onboard cwd-resolve; честный not-set; вербатим-markdown ради токенов). 200K-кап рулен ревью PASS (обе двери + entity backstop). N2 ревью закрыт: oversized-кандидаты падают громко с 400 по имени файла (121ba0d) — и не буферятся, и не молча импортируют файл второго приоритета.
- **02 — web** (62bbcdf + фикс 869aaf5): секция Manifest в detail-модале — textarea через setManifest (пустой драфт честно чистит), Import-кнопка с состояниями found/not-found/limit, честный absent-state; драфт синкается по ЗНАЧЕНИЮ project.manifest (refetch-on-focus не утёрт несохранённое). W1 (key={detail.id}) — однословный харденринг.
- **03 — клоун** (3f3a4b1 + фикс 5bb659f): 071-scan теперь несёт manifestHint (AGENTS→CLAUDE→QWEN first-found у рута; веб-секция рендерит как note) — хвост груминга дотащен. Архитектор: W-A1 — мой хинт дублировал список приоритетов и уже разъезжался семантически (пустой AGENTS.md → хинт врал) — теперь общий ManifestImporter.Candidates + skip пустых как у импортёра.

## Гейты финального дерева

dotnet build 0 warnings; последовательно: **Core 350 (+5), Infra 659 (+9), Api 642 (+7), Mcp 464→486** (063-волна соседняя), format clean; web: tsc 0, **тесты 163**, lint 0e/6w, build 11 роутов.

## Post-release click-check

1. MCP: `read_project_manifest` без манифеста → честное «No manifest is set…»; после импорта — вербатим-markdown с header-строкой источника.
2. UI: detail-модал проекта → Manifest → Save (setManifest=true) → перечитать тулзой → правка видна.
3. Import-кнопка: «Imported manifest from AGENTS.md.» + редактор показывает тело с header'ом; без файлов в руте → not-found ошибка.
4. 071-scan в проекте с AGENTS.md → manifestHint в ответе и note в секции импорта.
5. PUT без setManifest (легаси-payload) → манифест не тронут.

## Остаточное

- #231 (ReadOnly-тулзы создают проект через onboard-resolve) — приподнят архитектором: правильная форма — шов ProjectResolver с create-политикой на вызов; кандидат в мини-фичу при груме.
- Микробатч: N-X1 (handshake-тест «FortyThree» при 44 тулзах — переименовать), N-A2 (поведенческий тест manifestHint на Api-уровне), N-A3 (lowercase-namespace замечание протухло).

Коммиты волны: ea08eac, 121ba0d, 62bbcdf, 869aaf5, 3f3a4b1, 5bb659f.

<log>
- 2026-10-05 [agent] — Result drafted: 3/3 tasks done (01 PASS-WITH-NITS + N2 oversized fail-loud, 02 PASS-WITH-NITS + W1 key hardening, 03 closeout with architect APPROVED + W-A1 shared candidates fixed in 5bb659f). Gates: Core 350, Infra 659, Api 642, Mcp 486, web 163, build 0w. Post-release click-check inside. Waiting for the human tap.
</log>