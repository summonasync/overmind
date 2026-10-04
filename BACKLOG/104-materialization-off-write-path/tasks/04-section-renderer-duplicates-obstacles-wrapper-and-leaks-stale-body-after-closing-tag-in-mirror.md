---
feature: 104
task: 04
slug: materialization-off-write-path
status: done
review: light
created: 2026-10-04
brief: Section renderer duplicates obstacles wrapper and leaks stale body after closing tag in mirror
---

<goal>
Section renderer детерминированно портит материализованный task-файл, когда секция `<obstacles>` несколько раз заменялась через `planning_patch_artifact` в течение жизни артефакта, и внутри текста есть inline XML-подобные теги. Board-side `planning_read_artifact(sections:"obstacles")` отдаёт чистый одиночный wrapper — зеркало на диске рендерит ДВА открывающих `<obstacles>` (строки 53-54) и один закрывающий (:80), при этом буллет, удалённый с борды более поздним патчем ("Addendum before review…"), живёт в файле ПОСЛЕ `</obstacles>` (:98) вне какой-либо секции. Данные на борде чистые, рендер — нет; lastRun атомарный, ошибок нет (Squeezr project 31f33e30, task 3b1ab0c2, mirror file BACKLOG/055-watch-embeddings-cpu/tasks/02-….md, наблюдение 2026-10-04 19:42-19:45Z).
</goal>
<scope>
Рендер секций planning-артефакта в managed task-файлы: (1) не добавлять обёртку, если хранимое тело уже содержит тег секции (или наоборот — нормализовать при записи, чтобы обёртка была единственной); (2) объяснить/устранить утечку вытесненного буллета после закрывающего тега — похоже, файл не перезаписывается полностью, а склеивается из свежего тела секции и остатков предыдущего прогона; (3) тест: артефакт с ≥2 последовательными patch-заменами obstacles, текст содержит inline `<…>` теги — зеркало после каждого прогона валидно (один wrapper, никакого хвоста после закрытия).
</scope>
<anti-scope>
Не трогать clobber-guard (033) и нейминг (076) — это про тело файла, не про путь записи/имена. Board-side read и patch API работают корректно — их не чинить.
</anti-scope>
<constraints>
Воспроизводимо прямо сейчас на Squeezr-проекте; не чинить вслепую — сначала тест-репро из скоупа (пункт 3).
</constraints>
<acceptance-criteria>
- [x] Репро-тест краснеет до фикса: два патча obstacles с inline-тегами → зеркало содержит двойной открывающий тег.
- [x] После фикса зеркало валидно после каждого patch-прогона (ровно один wrapper, все буллеты внутри, вытесненные не переживают перезапись).
- [x] Существующий битый файл на Squeezr-дереве перегенерируется корректно при следующем материализационном прогоне.
</acceptance-criteria>
<refs>
104 (материализация в write-path — этот же механизм); фикс: 509a7ca (PlanningSections.Normalize + Replace sanitize + PlanningLog inline wrapper); review 104/04 PASS-WITH-NITS (2W/4N в микробатч-леджере #197-201).
</refs>
<obstacles>
Origin: догфуд Squeezr, заметка ff6a291f была заведена как entry-note до того, как договорились «баги — тасками»; note удалена после переноса сюда (filed 2026-10-04 by controller per crew rule «bug reports = task artifacts»; repro captured while releasing Squeezr 1.0.8). Вопрос-в-вопрос из исходной находки: почему body-фрагмент, удалённый section-patch'ем, вообще пережил последующие перезаписи файла — признак append-склейки вместо полной перезаписи. РАССЛЕДОВАНО (кодер 104/04): склейки не было — агенты эхотят wrapper-теги из зеркала обратно в patch-контент, Replace вклеивал дословно; board-read (last-anchored-pair) мусор не видел.
</obstacles>
<log>
- 2026-10-04 [agent] — [agent] Claimed by Van (claude), taken by orchestrator — pulled ahead of task 01 (independent bugfix, user's live repro on Squeezr). Dispatching dotnet-coder (sonnet) with the repro-first constraint.
- 2026-10-05 [orchestrator] — heal per review 104/04 solution (б): inline obstacles/log wrappers expanded to block form, controller filing note merged into obstacles; log carries the stored line verbatim (doubled [agent] marker is history, kept) + the heal line; Normalize verified no-op on the clean body; AC confirmed by fix review (509a7ca).
- 2026-10-04 [agent] — Heal per review 104/04 solution (б): inline wrappers expanded to block form, controller filing note merged into obstacles; stored log line carried verbatim + heal line. AC confirmed by fix review (509a7ca).
- 2026-10-05 [orchestrator] — heal-пасс v3: v2 heal-тело закрыло obstacles чужим тегом (</anti-scope>) — тот же класс ошибки, что чинили; закрытие исправлено на </obstacles>. No irony left behind.
- 2026-10-04 [agent] — Heal v3: fixed the v2 closing-tag typo (obstacles was closed with a foreign tag — the very class of bug under repair); stored 3 log lines carried verbatim + the honest correction line.
- 2026-10-04 [agent] — [agent] Light review passed (PASS-WITH-NITS — W1/W2 + N1-N3 to microbatch #197-201; live scratch-console probes by reviewer), fix in 509a7ca, body healed per review solution (б) — inline wrappers expanded, controller note in obstacles, log prefix verbatim. All 3 AC ticked (repro-first honored). Closing task; the corrupted mirror file self-heals on the next materialization pass (AC-3, pinned by test).
</log>