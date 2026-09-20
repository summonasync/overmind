---
feature: 033
slug: materializer-clobber-guard
status: inProgress
version: 1
created: 2026-09-20
---

# Feature 033 — Materializer clobber guard: не затирать молча локальные правки в managed-файлах

<goal>
Материализатор больше не затирает молча: перед перезаписью tracked-файла с расходящимся контентом (локальные правки, которых нет на борде) — предупреждение со списком путей, либо отказ без force (зеркало существующего GuardOverwrites для untracked). DetectDriftAsync уже умеет видеть расхождение — SyncAsync должен его консультировать до записи.
</goal>

<context>
Догфуд-находка Mindlash-крю (workflow #8, 2026-09-19): саб-агенты без MCP дописывали в <obstacles> материализованных тасков прямо на диске; следующий materialize (force:false) регенерил файл из тела борды — правки исчезали без предупреждения. Крю живёт с воркараундом «оркестратор транскрибирует всё в planning_append_log немедленно». Тот же класс риска: любые ручные правки в managed-дереве.
</context>

<scope-sketch>
- PlanningMaterializer.SyncAsync: перед записью tracked-файла сравнить хэш контента на диске с хэшем строки в PlanningMaterializedFile; расхождение → warn «local edits to <path> were overwritten» (v1) или refuse-unless-force (решить на спеке; warn — мягче для UX, refuse — честнее для данных).
- Список затёртых путей — в отчёт материализации (он виден в CLI и ответе API).
- Тесты: правка на диске → materialize → предупреждение/отказ по решению спеки.
</scope-sketch>

<open-questions>
- warn vs refuse-by-default (юзер решит на спеке).
- Автопредложение «transcribe to board» в тексте предупреждения.
</open-questions>

<status>
Draft. Источник: note «materializer silently clobbers hand-edits» (удаляется после нарезки — знание перенесено).
</status>