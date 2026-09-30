---
feature: 090
task: 04
slug: field-nits-batch
status: inprogress
review: light
created: 2026-09-29
depends_on: 03
brief: Closeout: live MCP dogfood of the alias brief-patch on the isolated fresh build (scratch copy of the prod board)
---

# Task 4 — closeout: live dogfood + architect light + result

<goal>
Prove the batch end-to-end on the live system, run the structural review, and write the result artifact.
</goal>

<scope>
- Live dogfood through the real MCP surface on this very feature: brief-patch one of this feature's tasks (version must not move), planning_read_feature(090, includeArtifacts=false), alias read 090/T1.
- Full dotnet test run as the final gate.
- Architect light review → audits/architect-review-2026-09-29-090.md; fix blockers if any (nits → result deviations or the microbatch queue note).
- Result artifact draft with verification evidence.
</scope>

<anti-scope>
- New scope items (they go to the microbatch note, not this feature).
- Committing (user asks explicitly, per crew rules).
</anti-scope>

<constraints>
- Dogfood must exercise the REAL MCP tools, not the HTTP endpoints directly — that is the point of R4.
</constraints>

<acceptance-criteria>
- [ ] Live dogfood transcript recorded: brief-patch (no version move), includeArtifacts=false read, alias read 090/T1 — all through MCP.
- [ ] Full dotnet test suite green (count recorded).
- [ ] Architect light review written to audits/architect-review-2026-09-29-090.md, blockers fixed or waived with rationale.
- [ ] Result artifact drafted with evidence and deviations.
</acceptance-criteria>

<refs>
- Spec 62aac67e (all); plan b78738e8 (Task 4).
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-29 [agent] — task cut from validated plan.
- 2026-09-30 [agent] — [agent] taken into work; closeout run by the orchestrator (live dogfood via real MCP + full tests + architect light sub-agent + result).
</log>