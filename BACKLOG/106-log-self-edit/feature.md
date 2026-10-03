---
feature: 106
slug: log-self-edit
status: draft
priority: p2
version: 1
created: 2026-10-03
---

# Feature 106 — Log self-edit: caller may correct their own newest log line (version-traced) — Qwen friction #6

<goal>
Log hygiene compromise (Qwen friction log, item 6): append-only `<log>` is the right doctrine, but model-echo clutter (stray CJK tokens in Cyrillic prose — four dated CORRECTION lines on one day) is permanent noise. Allow the caller to edit THEIR OWN most-recent log line — same actor marker, same session, nothing references it yet — with a version trace.
</goal>

<requirements>
1. Edit window: the newest log line only, same source marker (agent/ui), same session where applicable, and no artifact version/state change since the line was written.
2. The edit leaves a version trace (original preserved in artifact versions — 039 versioning covers it).
3. Rejects cleanly when any precondition fails (not newest / different actor / state moved on).
</requirements>

<non-goals>
- Editing historical lines; deleting lines; cross-actor edits.
</non-goals>

<refs>
- Friction log entry 500f8197 (item 6).
- 039-artifact-patch-write (append/patch/versioning contract).
- 078-upsert-log-guard (append-only doctrine — this feature must not weaken it).
</refs>