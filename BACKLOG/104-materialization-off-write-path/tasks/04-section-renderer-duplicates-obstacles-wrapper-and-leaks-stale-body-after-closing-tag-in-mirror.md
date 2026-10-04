---
feature: 104
task: 04
slug: materialization-off-write-path
status: todo
review: light
created: 2026-10-04
brief: Section renderer duplicates obstacles wrapper and leaks stale body after closing tag in mirror
---

<goal>Section renderer детерминированно портит материализованный task-файл, когда секция `<obstacles>` несколько раз заменялась через `planning_patch_artifact` в течение жизни артефакта, и внутри текста есть inline XML-подобные теги. Board-side `planning_read_artifact(sections:"obstacles")` отдаёт чистый одиночный wrapper — зеркало на диске рендерит ДВА открывающих `<obstacles>` (строки 53-54) и один закрывающий (:80), при этом буллет, удалённый с борды более поздним патчем ("Addendum before review…"), живёт в файле ПОСЛЕ `</obstacles>` (:98) вне какой-либо секции. Данные на борде чистые, рендер — нет; lastRun атомарный, ошибок нет (Squeezr project 31f33e30, task 3b1ab0c2, mirror file BACKLOG/055-watch-embeddings-cpu/tasks/02-….md, наблюдение 2026-10-04 19:42-19:45Z).</goal>
<scope>Рендер секций planning-артефакта в managed task-файлы: (1) не добавлять обёртку, если хранимое тело уже содержит тег секции (или наоборот — нормализовать при записи, чтобы обёртка была единственной); (2) объяснить/устранить утечку вытесненного буллета после закрывающего тега — похоже, файл не перезаписывается полностью, а склеивается из свежего тела секции и остатков предыдущего прогона; (3) тест: артефакт с ≥2 последовательными patch-заменами obstacles, текст содержит inline `<…>` теги — зеркало после каждого прогона валидно (один wrapper, никакого хвоста после закрытия).</scope>
<anti-scope>Не трогать clobber-guard (033) и нейминг (076) — это про тело файла, не про путь записи/имена. Board-side read и patch API работают корректно — их не чинить.</anti-scope>
<constraints>Воспроизводимо прямо сейчас на Squeezr-проекте; не чинить вслепую — сначала тест-репро из скоупа (пункт 3).</constraints>
<acceptance-criteria>
- [ ] репро-тест краснеет до фикса: два патча obstacles с inline-тегами → зеркало содержит двойной открывающий тег
- [ ] после фикса зеркало валидно после каждого patch-прогона (ровно один wrapper, все буллеты внутри, вытесненные не переживают перезапись)
- [ ] существующий битый файл на Squeezr-дереве перегенерируется корректно при следующем материализационном прогоне
</acceptance-criteria>
<refs>104 (материализация в write-path — этот же механизм)</refs>
<obstacles>Origin: догфуд Squeezr, заметка ff6a291f была заведена как entry-note до того, как договорились «баги — тасками»; note удалена после переноса сюда. Вопрос-в-вопрос из исходной находки: почему body-фрагмент, удалённый section-patch'ем, вообще пережил последующие перезаписи файла — признак append-склейки вместо полной перезаписи.</obstacles>
<log>- 2026-10-04 [controller] filed as a task per crew rule «bug reports = task artifacts» (was Overmind note ff6a291f, deleted); repro captured while releasing Squeezr 1.0.8</log>