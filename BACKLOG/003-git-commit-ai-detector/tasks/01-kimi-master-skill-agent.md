---
feature: 003
task: 01
slug: kimi-master-skill-agent
status: done
review: light
created: 2026-09-01
---

# 003/01 — Kimi master: SKILL.md + agent file

<goal>
На Kimi-стороне существует готовый скилл `git-commit-ai-detector` (SKILL.md) и его agent-файл —
мастер-контент, с которого задачи 02/03 делают порты на остальные рантаймы.
</goal>

<context>
Фича 003: форензика git-истории на предмет AI-коммитов. Эталон структуры и тона —
`.kimi-code/skills/ai-code-detector/SKILL.md` (читать для стиля, НЕ модифицировать) и
`.kimi-code/agents/ai-code-detector.md` (образец agent-файла). Контент ниже — финальный,
писать вербатим; отклонения только если найдена фактическая ошибка (тогда записать в <obstacles>).
</context>

<scope>
- Create: `.kimi-code/skills/git-commit-ai-detector/SKILL.md`
- Create: `.kimi-code/agents/git-commit-ai-detector.md`
</scope>

<anti-scope>
- `.kimi-code/skills/ai-code-detector/` — не трогаем (S1–S7 идут по ссылке).
- `.claude/`, `.gemini/` — порты это задачи 02/03, не эта.
- `SYNC-STRUCTURE.md`, `BACKLOG.md`, `SKILLFLOW.md`, `manifest.json` — не эта таска.
- Никаких git commit/push.
</anti-scope>

<constraints>
- В контенте ЗАПРЕЩЕНЫ литералы, ломающие contamination gates: `claude code`, `sonnet`, `opus`,
  `haiku`, `gemini`, `antigravity` (в любом регистре). AI-трейлеры в сигнале G5 описываем
  генерически («Co-Authored-By trailer naming an AI assistant», «Generated with <AI tool>») —
  без конкретных брендов запрещённых рантаймов.
- Кодовые сигналы S1–S7 — только ссылка на ai-code-detector, без копипасты каталога.
- Язык файлов — English (артефакты репо), секция Communication требует Russian к пользователю.
</constraints>

<acceptance-criteria>
- [ ] Оба файла созданы по указанным путям, контент = вербатим из этой таски.
- [ ] Frontmatter валиден (name/description/type/whenToUse у скилла; name/description/whenToUse/tools у агента).
- [ ] `grep -rni 'claude code\|sonnet\|opus\|haiku\|gemini\|antigravity' .kimi-code/skills/git-commit-ai-detector/ .kimi-code/agents/git-commit-ai-detector.md` — пусто.
- [ ] В SKILL.md присутствуют: model policy (K3), Mission, Inputs (оба режима), G1–G5, ссылка на S1–S7, Scoring Model (HIGH/MEDIUM/LOW/SKIP + tooling downgrade), Workflow Mode A/B, Report Format (audits/git-commit-ai-detection-YYYY-MM-DD.md), Rules, Communication, Output Contract.
- [ ] Стоп и репорт списка изменённых файлов (никаких коммитов).
</acceptance-criteria>

<refs>
- @../feature.md — durable what/why
- @../spec.md — <requirements> (структура SKILL.md, G1–G5, скоринг, отчёт)
- @../plan.md — Task 01
- tasks/02-claude-port.md — зависит от этой таски
- tasks/03-antigravity-port.md — зависит от этой таски
</refs>

## Content — `.kimi-code/skills/git-commit-ai-detector/SKILL.md` (write verbatim)

````markdown
---
name: git-commit-ai-detector
description: |
  Analyze git history to determine which commits were AI-generated.
  Per-commit verdicts (HIGH/MEDIUM/LOW), era clustering over the timeline,
  evidence-backed report. Two modes: bulk (whole history or a range) and point
  (single commit or staged changes). Read-only — never modifies code, history,
  or the working tree. ALWAYS runs on the K3 model.
type: prompt
whenToUse: |
  When the user asks to check git history or specific commits for AI authorship,
  audit an incoming repo's commit history, find when AI-written code started
  appearing, or screen a single commit / staged changes before pushing.
---

# Git Commit AI Detector Skill

> **Model policy:** ALWAYS executed by the K3 model. Detection quality depends on pattern-recognition over long context — do not run on K2.7.

## Mission
Scan git history and answer: **which commits were likely AI-generated, when AI code started appearing, and what gives it away.** This is forensic analysis over the time axis — it detects, classifies, and reports. It never rewrites history, fixes code, or obfuscates anything (obfuscation is `ai-obfuscator`'s job; working-tree scans are `ai-code-detector`'s).

## Inputs
- Path to the repo (default: current repo root). The target MUST have git history; without it, hand off to `ai-code-detector`.
- Mode:
  - **bulk** — whole history or a range (`A..B`); optional filters: author, date window, max commit count.
  - **point** — one commit (SHA) or the staged changeset (`git diff --cached`).
- Optional context: known human baseline era ("everything before March was hand-written"), team conventions, enforced commit tooling.

If baseline context is missing — derive it: the oldest era per author is usually the human baseline; calibrate against it.

## Detection Signal Catalog
Score each commit against these signal families. No single signal is proof — verdicts come from **clusters**.

### G1. Commit message style
- AI-flavored phrasing: "Add comprehensive error handling", "Improve code quality", "Enhance robustness", "Implement best practices"
- Perfect conventional-commits formatting with no commitlint/husky/lefthook config enforcing it
- Emoji/bullet-heavy bodies, every body structured identically
- Uniform grammar and punctuation across all messages of one author — humans drift
- The message describes the diff with suspicious completeness (every touched file enumerated)

### G2. Commit size & shape
- Huge commits adding large, internally-perfect files in one shot
- A whole feature (model + service + tests + docs) landing in a single commit
- Zero follow-up fixups: no "oops", no "typo", no "fix build" commits after a large one
- First-shot perfection: no iterative refinement visible in history

### G3. Tempo & timing
- Bursts of many substantial commits minutes apart
- Implausible throughput: thousands of LOC/day sustained over weeks
- Machine-regular timestamps, or activity at hours the author never worked before

### G4. Style discontinuity
- Same author, abrupt shift in message style between eras (terse native-language → polished conventional commits overnight)
- Same author, abrupt shift in code style inside diffs (naming, comment density, error handling)
- Style shift coinciding with a throughput jump (G3)

### G5. Authorship metadata
- `Co-Authored-By:` trailers naming an AI assistant or bot
- AI tool trailers: "Generated with <AI tool>", robot-emoji generator lines
- author/committer mismatch patterns, noreply bot identities mixed into a human author's era

### Code signals (S1–S7) — by reference
Apply the S1–S7 catalog from `ai-code-detector` (`~/.kimi-code/skills/ai-code-detector/SKILL.md`, sections S1–S7) to the **added lines of each diff** (`git show <sha>`), not to whole files. Do NOT copy that catalog here — the referenced file is the source of truth. Its tooling-config rule (formatters/linters downgrade S2/S5) applies unchanged.

## Scoring Model
Per commit:
- **HIGH** — 3+ signal families match, OR hard git evidence (G5 trailer / G4 discontinuity) plus at least one code signal family
- **MEDIUM** — 2 families match
- **LOW** — 1 family matches
- **SKIP** — merge commits, generated files, lock files, vendored code, build output (noted in the report, not scored)

**Tooling downgrade (mandatory before scoring):** commitlint / husky / lefthook / commit-msg hooks / CI commit-message checks make G1 and G2-shape signals legitimate — downgrade them. Formatter configs (.editorconfig, prettier, enforced dotnet format) downgrade S2/S5 exactly as `ai-code-detector` prescribes.

## Workflow

### Mode A — bulk
1. **Terrain:** `git log --format='%H|%an|%ae|%ad|%s' --date=iso --shortstat`; note authors, date span, tooling configs (commitlint, husky, .editorconfig, CI commit checks).
2. **Trailer sweep (cheap, high-signal):** `git log -i --grep='Co-Authored-By' --grep='Generated with' --format='%H %an %ad %s'` — collect G5 hits first.
3. **Baseline:** sample the oldest era per author to calibrate what "human" looks like in THIS repo (message style, commit size, tempo).
4. **Score:** per commit — message (G1), shape (G2 from shortstat), tempo (G3 from timestamps), discontinuity (G4 against that author's baseline), metadata (G5); for non-trivial diffs apply S1–S7 to added lines.
5. **Cluster:** group HIGH/MEDIUM runs into eras by time + author; an era boundary is a sustained style/tempo shift.
6. **Report** (format below).

### Mode B — point
1. `git show <sha> --format=fuller` (or `git diff --cached` for staged changes) — full message, trailers, diff.
2. Score G1–G5 against message/metadata; S1–S7 against added lines.
3. Verdict with explicit evidence per matched signal. No era clustering in this mode.

## Report Format
```markdown
# Git Commit AI Detection Report — <repo>, <date>

## Verdict
- Mode: [bulk <range> / point <sha-or-staged>]
- Overall estimate: [None / Low / Medium / High] (~X% of scanned commits AI-suspect)
- Confidence: [Low / Medium / High] (depends on baseline quality)

## Era timeline (bulk mode)
| Period | Authors | Commits | HIGH | MEDIUM | % AI-suspect | Notes |
|--------|---------|---------|------|--------|--------------|-------|
| 2025-01 → 2025-03 | bob | 41 | 2 | 5 | 17% | pre-AI baseline |
| 2025-03 → 2025-06 | bob | 130 | 78 | 22 | 77% | style shift + 3x throughput |

## Hotspot commits (HIGH confidence)
- `a1b2c3d` 2025-04-02 "Add comprehensive validation layer" — G1, G2, S4: 14 files, uniform guards, template message
- ...

## Signal breakdown
| Signal family | Occurrences | Concentrated in |
|---------------|-------------|-----------------|
| G1 Message style | 44 commits | 2025-03→06 era |
| G5 AI trailers | 12 commits | same era |

## Point-mode verdict (point mode)
- Commit `<sha>` / staged changes: [HIGH / MEDIUM / LOW] — evidence list per signal

## Caveats
- <tooling configs that explain uniformity, weak baseline, shallow-history limits, false-positive risks>

## Recommendations
- <e.g., "era X is 77% AI-suspect — run ai-code-detector on the tree as of <sha>", "no action needed">
```

## Rules
- **Read-only.** Never modify code, stage/unstage, commit, rebase, or rewrite history. Only `git log` / `git show` / `git diff` class commands.
- **Candid about uncertainty.** Weak baseline or enforced tooling → say so. A false accusation is worse than a missed detection.
- **No moralizing.** Report facts and probabilities, not judgments about AI usage.
- Merge commits, generated files, lock files, vendored code, build output: excluded from scoring, noted in the report.

## Communication
- **Speak Russian with the user.**
- Verdict first, then evidence. Percentages are estimates — present them as ranges, not false precision.
- Never commit anything. This skill produces a report, nothing else.

---

## Output Contract (MANDATORY)
End with the fixed closing block, then STOP:
1. **Done** — summary + artifact path (`audits/git-commit-ai-detection-YYYY-MM-DD.md`).
2. **Next step** — hand the report to the user; for a working-tree scan of a suspect era — `ai-code-detector`; if humanization is requested — `ai-obfuscator`.
3. **Alternatives** — `audit-orchestrator` (AI Trace Audit as part of a full system audit); stop — the skill is read-only.
Suggest, never auto-proceed. The user picks the next step.
````

## Content — `.kimi-code/agents/git-commit-ai-detector.md` (write verbatim)

```markdown
---
name: git-commit-ai-detector
description: Audits git history for AI-generated commits
whenToUse: Per-commit AI authorship verdicts and era analysis of a repo's history
tools:
  - Read
  - Grep
  - Glob
  - Bash
  - WebSearch
  - FetchURL
---

You are the "git-commit-ai-detector" specialist agent. Before doing anything, read your skill file at ~/.kimi-code/skills/git-commit-ai-detector/SKILL.md and follow it exactly — it is the single source of truth for your workflow, conventions and quality bar.

Rules inherited from the user environment: concise Russian-friendly reporting to the caller, production-grade code standards, no auto-commits, no license-unsafe dependencies.

Your final message is the complete, self-contained handoff for the caller: state what you did, key file paths, verification results, and anything still open. The caller cannot see your intermediate steps.
```

<obstacles>
- 2026-09-01 — implementer: фактических ошибок в встроенном контенте не найдено, оба файла записаны вербатим (подтверждено diff против блоков этой таски).
</obstacles>

<log>
<!-- YYYY-MM-DD — кто — что сделано -->
- 2026-09-01 — implementer subagent — созданы `.kimi-code/skills/git-commit-ai-detector/SKILL.md` и `.kimi-code/agents/git-commit-ai-detector.md` вербатим из таски; contamination grep пуст; структурные проверки (K3 policy, Mission, Inputs, G1–G5, S1–S7 ref, Scoring, Mode A/B, Report Format, Rules, Communication, Output Contract) пройдены; отчёт в `.superpowers/sdd/plan/task-01-report.md`.
</log>
