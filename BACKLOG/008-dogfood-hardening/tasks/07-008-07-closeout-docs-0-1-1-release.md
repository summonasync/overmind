---
feature: 008
task: 07
slug: dogfood-hardening
status: done
review: hard
created: 2026-09-16
---

# 008/07 — Closeout: docs + 0.1.1 release

<goal>Релиз 0.1.1 выпущен и задогфужен на боевом профиле: docs синхронизированы, архив собран и установлен, upgrade-sync при bundle-version 2 применился.</goal>

<context>
Фича 008-dogfood-hardening (dogfood 0.1.0 findings). Субагент читает только этот файл. Спека validated: решения IMP-1..IMP-5, UI-3/4, PERF-1 зафиксированы в ней; ревьюер читает @../spec.md.
</context>

<scope>- CONTEXT.md: importer losses (approved-маппинг и result.md убрать из потерь, 01b-поведение описать)
- TECHNICAL-SOLUTIONS.md: ADR-поправки при необходимости
- build/pack.sh --version 0.1.1 --bundle-version 2 --rids linux-x64
- Перед установкой: снапшот ~/.overmind; установка поверх; проверка миграций + upgrade-sync + сида
- Живой прогон всех исправлений (skipped-лист, 01b, result round-trip, ворнинг EF, превью)
- BACKLOG.md Deferred: закрыть/томбстоунить покрытые фичей строки hardening</scope>

<anti-scope>- Релиз win/osx архивов (build-only проверка как в 003a)
- Автокоммиты: коммиты только по явной команде пользователя</anti-scope>

<constraints>
- .NET 10 / C# 14 (таски 01-04, 06), Next.js 16 + Tailwind 4 (таска 05)
- Новые пакеты — только через nuget-licensing-guard / явное одобрение пользователя
</constraints>

<acceptance-criteria>
- [ ] dotnet test Overmind.sln зелёный (526+ новых)
- [ ] Живой догфуд 0.1.1: install, boot, upgrade-sync, все клетки исправлений зелёные
- [ ] CONTEXT.md не содержит устаревших declared losses
- [ ] Ревью hard пройдено
</acceptance-criteria>

<refs>
- @../feature.md — what/why фичи
- @../spec.md — decisions IMP/UI/PERF (ревьюер)
- @../plan.md — подход и риски (ревьюер)
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-17 — 2026-09-17 — orchestrator — input from review 008/04: CONTEXT.md closeout must document the new loud report prefix "reassigned:" (rides the Skipped list; widens the skipped/ignored/unknown-status vocabulary)
- 2026-09-17 — 008/07 started: docs pass delegated, pack + live upgrade dogfood by orchestrator
- 2026-09-17 — 2026-09-17 — hard architect review — PASS-WITH-NITS (audits/architect-review-2026-09-17-008-07.md); WARNING-1 (letter-suffix duplicate cycle on re-import) spawned task 08 in-feature, must land before the live re-import cells of this closeout
- 2026-09-17 — 2026-09-17 — live dogfood 0.1.1 GREEN: checksum, profile snapshot, swap, autostart re-registered; boot over live profile (no migrations needed); upgrade-sync bundle 1->2 (1 updated); health version stamp; MCP adapter 14 tools; re-import cells: per-line skipped list live, 01b imported then folded via matched:, byte-stable no-op on repeat
- 2026-09-17 — 008 closeout complete: docs synced (CONTEXT/ADR/BACKLOG tombstones), 0.1.1 packed+installed, upgrade-sync and all fix cells green live, hard review PASS-WITH-NITS with WARNING-1 fixed via tasks 08/09 and verified
- 2026-09-17 — 2026-09-17 — stability probe: ceremony-triggered sync must be byte-identical outside the touched artifact
- 2026-09-17 — 2026-09-17 — stability confirmed: heal4/heal5 byte-identical, ceremony-sync probe clean (only the touched artifact changed); the heal-window flap was one-time normalization of legacy H1-carrying bodies
</log>