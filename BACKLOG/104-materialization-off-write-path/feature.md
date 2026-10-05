---
feature: 104
slug: materialization-off-write-path
status: inProgress
priority: p1
version: 1
created: 2026-10-03
---

# Feature 104 — Materialization off the write path + honest timeout errors (Qwen friction #3)

<goal>
A write that times out but still commits is the worst failure mode for agent trust (Qwen friction log, item 3, observed 2026-10-03): planning_patch_artifact returned «HttpClient.Timeout of 10 seconds» while the patch HAD committed (read-back showed the new version). An agent re-sending on timeout creates double-version churn; not re-sending risks a lost write.
</goal>

<requirements>
1. Move inline materialization off the request path — it is the documented slow part making 10s timeouts realistic. Async pass (the existing event-driven trigger + a bounded queue), with materialize_status reporting progress (074 infra exists).
2. Timeout/unreachable errors on mutations state explicitly: whether the mutation committed and its version — so the agent's retry logic can be correct («committed at vN — do not resend» vs «not committed — resend safe»).
3. Keep the mirror eventually-consistent guarantee: an async pass failure must surface (lastAttempt/lastError path from 074), not vanish.
</requirements>

<non-goals>
- Dropping the mirror or the event-driven trigger; changing artifact versioning semantics.
</non-goals>

<refs>
- Friction log entry 500f8197 (item 3).
- 074-materialization-run-diagnostics (status/lastAttempt infra).
</refs>

<log>
- 2026-10-04 [auto] — artifact created → inProgress
- 2026-10-04 [agent] — Van by claude claimed the feature
- 2026-10-05 [agent] — Van by claude took over stale claim of Van by claude
</log>
