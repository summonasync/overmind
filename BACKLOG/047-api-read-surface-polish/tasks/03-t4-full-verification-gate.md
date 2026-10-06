---
feature: 047
task: 03
slug: api-read-surface-polish
status: todo
review: light
created: 2026-10-06
depends_on: 00, 01, 02
brief: Full gate: dotnet build + serial dotnet test Overmind.sln, web lint/tsc, straggler sweep over taskTotal mirrors; spec AC fully ticked.
---

# T4 — Full verification gate

<goal>
The whole solution proves the feature: build green, full serial test suite green, web checks green, no straggler mirrors missed, every spec acceptance item ticked.
</goal>

<scope>
- dotnet build Overmind.sln, then full serial dotnet test Overmind.sln. Known flake (AGENTS.md): cold-CLR Internal CLR error 0x80131506 → dotnet build-server shutdown, retry once.
- Re-run cd web && npm run lint && npx tsc --noEmit if T3 did not leave it green.
- Straggler sweep: grep -rn "taskTotal" src tests web/src — confirm no mirror record or doc that should also carry the counts was missed (AgentSessionLockApiTests/FeatureDependencyApiTests local mirrors deserialize leniently — extend only if a count assertion is wanted, otherwise leave).
- Tick the remaining spec acceptance-criteria items on the spec artifact.
</scope>

<anti-scope>
- No new code unless a gate failure demands a fix — fixes land in the owning task's file set.
</anti-scope>

<constraints>
- Serial test run (AGENTS.md contract), full solution scope.
- No commits.
</constraints>

<acceptance-criteria>
- [ ] dotnet build Overmind.sln green.
- [ ] Full dotnet test Overmind.sln green (serial).
- [ ] Web lint + tsc green.
- [ ] Straggler sweep done; findings either fixed or explicitly declared out of scope.
- [ ] All spec acceptance-criteria items ticked.
</acceptance-criteria>

<refs>
- Spec 420cfc7d-0f9b-4883-a44d-130a911f8625 (acceptance criteria).
- Plan c505ad37-2f6c-41a6-8f01-20d5ad4ecf96 (T4, verification gates, compat note).
- Waits on Tasks 0, 1, 2 (blockedBy edges).
</refs>

<obstacles>
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan T4; blockedBy T1-T3 per plan strategy.
</log>