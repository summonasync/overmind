---
feature: 070
task: 01
slug: response-polish-round3
status: done
review: light
created: 2026-09-26
brief: Compact echo objects on all mutation endpoints; honest re-tick no-op; feature.UpdatedAt touch
---

# 070/01 — Backend: compact echoes, honest no-op, feature touch

<goal>
Spec R1-host, R2, R4: mutation POSTs stop returning empty 200; re-tick is an honest no-op; a live feature stops looking frozen.
</goal>

<scope>
- Compact echo {id/slug, version, status, lastLogLine} from: artifact /log, /status, /validate endpoints; feature set-current and set-feature-status. warnings? where the door already produces them. The empty-200 era ends (037 obstacle).
- No-op re-tick: already-ticked AC → version NOT bumped, no log line, response surfaces alreadyTicked=true (implementer picks flag vs derived; pins decide).
- Feature.UpdatedAt touch on every artifact mutation (append_log, patch, upsert, validate, set_status) in the same SaveChanges — no extra log line.
- Api/Infra pins: echo key-set per endpoint; no-op version pin; feature-touch pin (updatedAt moves, log unchanged).
</scope>

<anti-scope>
- MCP adapter (task 02), docs (03), delete_entry (04), web UI.
</anti-scope>

<constraints>
- .NET 10 / C# 14; no comments; English-only; no git commands; dotnet build 0 warnings + dotnet test green.
</constraints>

<acceptance-criteria>
- [x] /log, /status, /validate, set-current, set-feature-status return the compact key-set (pins)
- [x] Re-tick: version unchanged + alreadyTicked surfaced (pin)
- [x] Artifact mutation moves feature.UpdatedAt in the same SaveChanges (pin)
- [x] dotnet build 0 warnings + dotnet test green
</acceptance-criteria>

<refs>
- Spec cda4c5c7 (validated) R1-host/R2/R4; plan 13089966; 037 empty-200 obstacle; 068 chain (TickAc early exit).
</refs>

<obstacles />

<log />

<log>
- 2026-09-26 [agent] — 070/01 picked up: compact echoes + honest re-tick + feature touch; starting implementation
- 2026-09-26 [agent] — Coded (dotnet-coder): compact mutation echoes shipped — artifact /log and /validate (both doors) return {id, version, status, lastLogLine} instead of empty 200 (037 obstacle closed at host level); /status returns the same keys plus the existing warnings/featureStatusChanged; feature set-current and set-feature-status return {slug, version, status, lastLogLine} (agent set-current keeps its blocker STOP door). Honest re-tick: TickAc returns bool with the early no-op exit preserved (frozen case included), patch response surfaces alreadyTicked only on tick_ac ops — replace_section key-set pin unchanged; retried tick leaves version, snapshot and feature updatedAt untouched. Feature.UpdatedAt touch rides the mutation's SaveChanges on upsert, upsert_tasks, patch, validate, set_status, append_log — only when something really changed, no extra log line; new PlanningFeature.Touch/LastLogLine + PlanningArtifact.LastLogLine, PlanningArtifactPatchResult carrier. Pins added: echo key-set pins for both doors (append_log, validate, set_status, set-current, set-feature-status), retick pin at API level, feature-touch pins at service and API level, identical-upsert no-touch pin, entity bool pins. Build 0 warnings; tests 1513/1513 green (Core 215 / Infra 496 / Api 447 / Mcp 355); 068 frozen-sweep and 077 proof-line gates re-verified green; Overmind.Mcp untouched — task 02 projects these live responses.
- 2026-09-26 [agent] — 070/01 done by dotnet-coder: compact echoes on /log, /status, /validate, set-current, set-feature-status; honest re-tick with alreadyTicked surfaced; feature.UpdatedAt touch in the same SaveChanges. Evidence: build 0 warnings, tests 1513/1513 (Core 215 / Infra 496 / Api 447 / Mcp 355), 068/077 grep gates green. All 4 AC ticked; ready for light review.
</log>
