---
feature: 081
slug: task-dependencies
status: review
priority: p1
version: 1
created: 2026-09-26
---

# Feature 081 — Task-level dependencies: blockedBy on task artifacts, honored by current_task (030-семантика на уровень тасок)

<goal>
Task sequencing today lives only in prose (`<refs>` text like "57 must not start before the end-to-end pass"), which the machine cannot gate on — the exact class of constraint that gets violated by an agent holding a `todo` task with no stop signal (MindlashStudio report 2026-09-26; same pain as BobHouse B7, which produced feature-level 030).
</goal>

<requirements>
- `blockedBy` on task artifacts with the same semantics features already have (030): a blocked task is skipped by `planning_current_task` in favour of the next actionable one; if every remaining task is blocked, the stop/empty reason names the blocker chain.
- Express and read `020/T3`-style references; no tooling ambiguity with feature-level edges (same feature implied when slug is omitted).
- Minimal viable surface: even just `blockedBy` surfaced in listings (080 roster) so "which of these 7 todo tasks is unblocked right now?" is answerable without full bodies.
</requirements>

<non-goals>
- Cross-feature task edges (feature-level 030 already covers that layer).
- Automatic unblocking logic beyond the existing feature-dependency close semantics.
</non-goals>

<refs>
- 030-feature-dependencies (done) — the semantic template to extend downward.
- Source: MindlashStudio report 2026-09-26 gap 3 + BobHouse 020 B7.
</refs>

<log>
- 2026-09-27 [auto] — artifact created → inProgress
- 2026-09-27 [auto] — all tasks done + spec + plan validated → review
</log>
