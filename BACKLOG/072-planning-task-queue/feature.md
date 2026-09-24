---
feature: 072
slug: planning-task-queue
status: draft
priority: p1
version: 1
created: 2026-09-24
---

# Feature 072 — Planning task queue: MCP-тул «что брать следующим» — проекция борды (приоритет asc, blocked-aware, readiness), юзер рулит приоритетами с борды

<goal>
One MCP tool answering "what should be worked on next": planning_task_queue(projectId, limit?) returns the open features as an ordered queue — priority ascending, blocked-aware (via the 030 resolver when it lands), readiness-flagged (spec/plan/tasks carved?), current pinned on top. The human changes priorities through the existing board controls (038 inline select); the agent re-reads the queue. No new mutation path — the queue is a projection, priorities remain the single source.
</goal>

<requirements>
- MCP tool planning_task_queue (ReadOnly, Idempotent): ordered open features — sort: priority asc, then readiness (more carved = more ready), then oldest first; current feature pinned first with a flag; each entry compact {slug, number, title, priority, status, tasksDone/taskTotal, readiness, blockedBy? (once 030 lands), isCurrent}.
- Blocked features (030 dependencies) stay visible in the queue with their blocker list but sort below takeable ones — visibility, not hiding.
- Compact echo per 037; description states the ordering contract and that the human reorders via the board.
- Spec decides 035-execution-queue's fate: superseded by this projection (recommend closing it as such) or kept for manual reordering.
</requirements>

<non-goals>
- New mutation tools (priorities move through the board/UI only).
- Cross-project queues.
- Automatic assignment/scheduling.
</non-goals>

<refs>
- User request 2026-09-24 ("получить task-queue по mcp, а я изменяю" — agent reads, human reorders via existing controls).
- planning_list_features (the data source); 034 (priority data), 038 (inline priority editing — the human's lever), 030 (blocked-awareness input), 035 (execution queue — fate decided in spec), 052-briefing (start-of-session sibling; queue is mid-session "what next").
</refs>