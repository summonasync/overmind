---
feature: 036
task: 04
slug: agent-workflow-onboarding
status: todo
review: light
created: 2026-10-06
depends_on: 01, 02, 03
brief: Full gate: dotnet build Overmind.sln + full dotnet test Overmind.sln green (cold-CLR flake: build-server shutdown, retry). Post-deploy verification — real /api/onboard payload eyeball — only after the next adapter deploy, NOT runnable now; contract tests carry the gate until then.
---

# Task 036/04 — full gate + (post-deploy) live walk

<goal>
The whole suite is green with the new teaching text in place, and the live onboard payload is eyeballed once the adapter ships it — closing the feature's done bar honestly.
</goal>

<scope>
- dotnet build Overmind.sln, then full dotnet test Overmind.sln. Known cold-CLR flake per AGENTS.md: dotnet build-server shutdown, then retry the same command.
- Live-payload check (POST-DEPLOY ONLY, not runnable in this task's environment now): after the next adapter deploy/restart, run the host and eyeball the real /api/onboard output — ceremony block present, 032 blocks intact, no truncation. Until then the contract tests (036/01–03) carry the gate; record the deploy-time observation in this task's <log> when it happens.
</scope>

<anti-scope>
- No code changes in this task; a red suite routes back to the owning slice task (036/01–03), not fixed here.
</anti-scope>

<constraints>
- Full serial suite — no test filtering for the gate; targeted filters were the per-slice gates.
- If the post-deploy walk is impossible at close time, the task closes with the suite gate + an explicit <log> note that the live walk is pending deploy — the limitation goes to the result artifact, never silently dropped.
</constraints>

<acceptance-criteria>
- [ ] dotnet build Overmind.sln green.
- [ ] Full dotnet test Overmind.sln green (flake protocol followed if hit).
- [ ] Post-deploy live walk done and logged, OR an explicit pending-deploy note in <log> carried into the result artifact.
</acceptance-criteria>

<refs>
- Spec: 036/S (a8d38f0d-37c9-4513-948e-30a227c1a566, validated). Plan: 036/P (66a76b70-ca36-46a8-ada4-6527042ad4ab, validated) — slice 4.
- Blocked by tasks 1–3: the full gate runs on their combined result.
</refs>

<obstacles>
- Live walk requires the next adapter deploy (spec obstacle note, 032 precedent) — pending by design, see constraints.
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan 66a76b70 (slice 4).
</log>