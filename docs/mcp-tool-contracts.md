# MCP Tool Contracts (Tier 2)

Full per-tool contracts for the Overmind MCP adapter. Tool and parameter `[Description]`
attributes carry only selection-critical prose (budget: ≤400 chars per tool description,
≤120 per parameter, ≤800 per tool, ≤24,000 repo-wide, enforced by `DescriptionBudgetLintTests`).
Everything the description dropped — field enumerations, paging recipes, error behavior,
resolution mechanics — lives here. One section per tool; the section heading is exactly the
tool name, and that heading is the anchor (`docs/mcp-tool-contracts.md#<tool-name>`).
Planning ceremony mechanics stay in the `overmind-ceremony` skill; this doc is the wire contract.

## overmind_wake_peek

Wake ritual for the calling session: a side-effect-free read of the two pending-signal arms.
Advances no watermarks, marks nothing read — safe to poll any time.

**Response.** `{signals, cursor}`. Each signal: `{kind, refId, roomId, from, createdAt, excerpt}`.

- `notification-reply` — a human reply to a notification this session sent. `refId` is the
  notification id, `roomId` is null, `from` is `human`, `createdAt` is the notification's
  `RepliedAt`, `excerpt` is the reply text capped at 200 text elements.
- `wave-mention` — a wave chat message mentioning this session's alias. `roomId` is the wave
  room, `from` is the author alias. `refId` is the message id.

**Default windows (omit `since`).**

- Replies: notifications carrying `ReplyText` whose `readAt` is null — a consumed reply leaves
  the default window immediately. The window looks back 7 days; override with
  `OVERMIND_WAKE_REPLY_LOOKBACK_DAYS` on the host (values below 1 clamp to 1). The mention arm
  never reads that variable.
- Mentions: messages created after the session's `LastSeenAt` as it stood before this request.

**Explicit `since` is the replay override.** When `since` is present (a bare timestamp or a
cursor echo), the reply arm also returns consumed (`readAt`) notifications in the window —
that is how replayed or paged traffic looks complete. The mention arm bounds at the same instant.

**Project scoping.** The adapter appends `projectId=<resolved>` to the wake request whenever its
own ProjectResolver yields one (env `OVERMIND_PROJECT_ID`, project pin, or host onboard reply —
resolution failure is swallowed and keeps the poll machine-wide). A session that resolves none
gets the machine-wide window. When scoped, the reply arm filters to notifications of that project;
foreign-project replies stop showing up. The mention arm has no project filter.

**Paging.** ≤100 signals per page, ordered by `createdAt` asc, ties broken by `refId`. The cursor
is the newest `createdAt` seen followed by `'|'` and the newest signal's `refId`, or the request
`since` on an empty page. Feed the cursor back verbatim as `since` and repeat until an under-cap
page arrives; the cursor is inclusive-exact, so no loss or duplicates across a tie group.

**Session identity.** The adapter stamps its registered session automatically; a headerless or
unknown session fails with a named error (`POST /api/agent/sessions` registers).

**Host wire shape.** `GET /api/agent/wake` with optional `since` (ISO 8601, optionally
`|<refId>` — malformed values return 400 naming the expected shape) and optional `projectId`
(adapter-internal; not a tool parameter). The response JSON above is the agent-facing contract
and is unchanged by the lookback, consumption and scoping work; only the row set moved.

<!-- append-regions: the two diet batches fill disjoint zones of this file so parallel lanes
     never collide on the same lines. Insert a tool's section INSIDE its own region, above its
     end marker; never move or rename a marker, never add a heading outside a region.
     region:planning  -> planning_* tools (diet batch A)
     region:others    -> every non-planning, non-wake tool (diet batch B) -->
<!-- region:planning:start -->
<!-- region:planning:end -->
<!-- region:others:start -->
<!-- region:others:end -->
