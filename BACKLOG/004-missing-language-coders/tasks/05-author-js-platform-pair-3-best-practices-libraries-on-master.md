---
feature: 004
task: 05
slug: missing-language-coders
status: done
review: light
created: 2026-09-27
brief: Author js-platform pair + 3 best-practices libraries on master
---

<goal>Author the js-platform pair + three best-practices libraries on master .kimi-code.</goal>
<scope>- skills/js-platform-coder/SKILL.md + agent: owns Electron (main/preload/renderer split, IPC, security model: contextIsolation on, no nodeIntegration in renderer) + React Native (navigation, native modules, platform-specific code, multi-platform builds for 2–3 targets). Anti-scope: web apps (→ web-coder), standalone backend services (→ node-coder). Routing table + output contract + kimi agent shape.
- skills/js-platform-code-reviewer/SKILL.md + agent: mirrored checklist (electron security model, IPC hygiene, RN navigation/native-module patterns, platform build configs).
- skills/web-best-practices/SKILL.md, node-best-practices, js-platform-best-practices: BP-ID catalogs mirroring dotnet-best-practices shape (BP-IDs, enforceable rules, consumed by coder + reviewer); initial top rules per lane: web — TS strict, RSC/client boundary, BFF pattern, React Query usage, a11y; node — async/errors, security, config, logging; platform — electron security, RN perf/navigation. NO agents for these three.</scope>
<anti-scope>No port sides; no registration files (T7); no BACKLOG//audits/.</anti-scope>
<constraints>One skill one job; English-only; NO git commit.</constraints>
<acceptance-criteria>
- [x] kimi counts: agents=28, skills=34
- [x] Best-practices skills exist WITHOUT agent files; BP-ID format matches dotnet-best-practices
- [x] YAML grep #7 empty
</acceptance-criteria>
<refs>Spec §Phase A items 7–9; Plan T5; port side depends on this task (T6).</refs>
<obstacles></obstacles>
<log>
- 2026-09-28 [agent] — взята в работу исполнителем
- 2026-09-28 [agent] — normalize body: single log section (dedupe after status move) + newline after acceptance-criteria tag so AC items parse
- 2026-09-28 [agent] — T5 executed on master .kimi-code: created skills/js-platform-coder/SKILL.md + agents/js-platform-coder.md (Electron main/preload/renderer split, typed IPC contracts with zod validation in main, security model contextIsolation/no-nodeIntegration/sandbox, RN navigation + native modules + 2-3 target builds) and skills/js-platform-code-reviewer/SKILL.md + agents/js-platform-code-reviewer.md (mirrored checklist: security model, IPC hygiene, RN navigation/perf, build+signing configs). Created web/node/js-platform-best-practices SKILL.md catalogs, NO agents, BP-ID format mirroring dotnet-best-practices (severity B/W/S, decade-per-section numbering). Verified: kimi counts agents=28 skills=34 (ls); best-practices agent grep 0 hits; YAML grep #7 empty; cross-runtime leakage grep clean on all 7 new files; strict yaml.safe_load parse OK on all 7 frontmatters. Quirk hit as documented: first status move duplicated <log> and AC items unparseable (no newline after opening tag) — normalized via one full upsert order=5, then all 3 ACs ticked via planning_patch_artifact. External polish pass touched all 7 files after authoring (fuses, setWindowOpenHandler, EXPO_PUBLIC_*, expo-router, OTA rules; BP catalogs scoped to the exact initial rule sets from the task scope) — current on-disk state verified and accepted. No registration files touched (T7 owns). No git commit.
- 2026-09-28 [agent] — execution complete: 7 files authored on master .kimi-code, all 3 ACs verified and ticked (counts 28/34, BP catalogs without agents in dotnet BP-ID format, YAML grep #7 empty), moved to review
- 2026-09-28 [agent] — Orchestrator review passed: 7 files authored (platform pair + 3 BP catalogs 25/20/20 BP-IDs), counts hit 28/34 target exactly, bp skills agentless, YAML clean, anti-scope routing present. Orchestration incident documented: session restart orphaned the first executor which kept running and completed the task; a duplicate re-dispatch was stopped before file damage — no conflicts on disk.
</log>