---
feature: 003
slug: git-commit-ai-detector
status: draft        # draft | approved (approved = FROZEN)
version: 1
created: 2026-09-01
---

# Spec 003 — git-commit-ai-detector skill

<decisions>
- Отдельный скилл `git-commit-ai-detector`, НЕ режим внутри ai-code-detector: single-purpose,
  детектор кода не раздувается, его секция S8 не конфликтует. Shared-каталог сигналов (вариант 3)
  отклонён как premature — сигналы шарим ссылкой на файл, а не выносом в _shared/.
- Имя: `git-commit-ai-detector` (выбор пользователя).
- Два режима в одном скилле: bulk (история/диапазон) и point (коммит/staged) — пользователь выбрал «оба».
- Полная глубина: git-метаданные + сообщения + кодовые диффы (не «только git-уровень»).
- Кодовые сигналы S1–S7 НЕ копипастятся — SKILL.md ссылается на .kimi-code/skills/ai-code-detector/SKILL.md
  как на source of truth; применение — только к added-строкам диффа.
- Модель: K3 во всех рантаймах по translation-таблицам SYNC-STRUCTURE.md (Kimi K3 → Claude Opus →
  strongest reasoning model), как у ai-code-detector.
</decisions>

<open-questions>
- Нет. Все вопросы закрыты в brainstorm (режимы, глубина, гранулярность отчёта, подход).
</open-questions>

<requirements>
- Структура SKILL.md зеркалит ai-code-detector: frontmatter (name/description/type/whenToUse),
  model policy, mission, inputs, signal catalog, scoring model, workflow, report format, rules,
  communication, output contract.
- Git-сигналы, семейство G:
  - G1 Commit message style: AI-флейвор формулировки («Add comprehensive…», «Improve code quality»),
    идеальный conventional-commits без commitlint/конфига, emoji/bullet-heavy body,
    шаблонная грамматика сообщений у одного автора.
  - G2 Commit size & shape: огромные коммиты с большими идеальными файлами, целая фича одним коммитом,
    отсутствие fixup/доработок следом.
  - G3 Tempo/timing: пачки коммитов за минуты, неправдоподобный throughput (LOC/день), аномальная
    регулярность таймстампов.
  - G4 Style discontinuity: резкая смена стиля сообщений/кода у одного автора между эпохами.
  - G5 Authorship metadata: `Co-Authored-By` AI-трейлеры, AI-tool трейлеры («Generated with…»),
    author/committer mismatch.
- Скоринг per-commit: HIGH = 3+ семьи сигналов ИЛИ git-доказательство (G5/G4) + кодовые сигналы;
  MEDIUM = 2 семьи; LOW = 1 семья. Перед скорингом — проверка tooling-конфигов (commitlint, husky,
  lefthook, форматтеры, enforced conventional commits): легитимная унификация понижает G1/G2.
- Merge-коммиты, generated-файлы, lock-файлы, build output — исключены из скоринга, но отмечаются в отчёте.
- Workflow bulk: git log → baseline-эпоха (калибровка «human» по старым коммитам) → per-commit скоринг →
  кластеризация по времени/авторам → отчёт. Workflow point: полный разбор G + S одного коммита/staged → вердикт.
- Отчёт: audits/git-commit-ai-detection-YYYY-MM-DD.md; секции: Verdict → Era timeline (таблица:
  период, авторы, % AI-suspect) → Hotspot-коммиты (HIGH, с перечнем сигналов) → Signal breakdown →
  Caveats (false-positive риски, качество baseline) → Recommendations.
- Rules: read-only, без морализаторства, candid про неопределённость, русский в коммуникации,
  никаких коммитов, closing block по output contract (Done / Next step / Alternatives).
- Agent-файл git-commit-ai-detector.md по образцу ai-code-detector.md (frontmatter tools: Read, Grep,
  Glob, Bash, WebSearch, FetchURL; ссылка на SKILL.md как single source of truth).
- Triple-edit deliverables: SKILL.md + agent во все три рантайма; SYNC-STRUCTURE.md — две строки в
  file mapping + обновление count parity (20→21 agents, 22→23 skills); BACKLOG.md — индекс-строка;
  SKILLFLOW.md — исполнитель читает граф и добавляет узел/переходы скилла только если SKILLFLOW регистрирует скиллы явно (иначе — не трогать).
- Cross-runtime contamination gates из SYNC-STRUCTURE.md pre-commit checklist должны быть чистыми.
</requirements>

<!--
LIFECYCLE: после status: approved файл ЗАМОРОЖЕН.
Изменение = ре-валидация с пользователем + bump version. Молчаливые правки запрещены.
Читатели: оркестратор, ревьюер. Исполняющий субагент spec НЕ читает —
его контракт — tasks/NN-foo.md.
-->
