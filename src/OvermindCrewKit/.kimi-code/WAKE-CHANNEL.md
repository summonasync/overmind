# WAKE-CHANNEL (Drop File Recipe)

> The MCP-free first read of the 190 wake ritual. The drop file is a plain file — it works
> even when the overmind transport is down, which is exactly when `overmind_wake_peek`
> is unreachable.

On every signal that concerns an agent (a human reply on its session's notification, a wave
message mentioning its alias), the Overmind Host rewrites that agent's wake drop file:

> `<stale 2026-11-17 — the human-reply half of this sentence no longer fires on live data>` A
> reply is stamped read at the moment it is written (the 084 reply⇒read invariant,
> `AgentNotification.Reply()`), and feature 234/T5 dropped read replies from the default wake
> window — the same window this drop file is fed from. So on a current database only
> `wave-mention` signals land here; replies remain reachable through `overmind_replies` and the
> notification center, and per-agent reply delivery becomes feature 244 (delivery cursor). Until
> 244 lands, do not plan a wake ritual around a reply appearing in this file.

```
~/.overmind/wake/<alias>.json        # override the directory with OVERMIND_WAKE_DIR
```

The 190 tick reads it BEFORE `overmind_wake_peek`, then peeks as usual:

1. Read `~/.overmind/wake/<alias>.json`. **Lowercase your alias first** — drop file names
   are always lowercase (`Dan` → `dan.json`), so mixed-case identities resolve to the same
   file from every code path. A missing file means no pending signals (or no live session)
   — fall through to the peek.
2. Compare the file's `cursor` with the last one you consumed. Same cursor → nothing new,
   done. Different → act on the signals newer than what you already saw.
3. Call `overmind_wake_peek` as usual — session-scoped, authoritative, side-effect free;
   it pages past the file's 100-signal cap and stays the source of truth. The file is its
   push projection, not a replacement.

Drop shape (JSON, camelCase):

```json
{
  "alias": "dan",
  "updatedAt": "2026-11-01T10:00:00.0000000Z",
  "cursor": "2026-11-01T10:00:00.0000000Z|0f8fad5b-d9cb-469f-a165-70867728950e",
  "signals": [
    {
      "kind": "notification-reply",
      "refId": "…",
      "roomId": null,
      "from": "human",
      "createdAt": "2026-11-01T09:59:00.0000000Z",
      "excerpt": "approved"
    },
    {
      "kind": "wave-mention",
      "refId": "…",
      "roomId": "all",
      "from": "erin",
      "createdAt": "2026-11-01T10:00:00.0000000Z",
      "excerpt": "ping @Dan from the wave"
    }
  ]
}
```

Rules:

- `cursor` is `<ISO-8601 timestamp>|<signal refId>` — feed it back as `overmind_wake_peek`'s
  `since` to resume exactly where the file ends.
- Reading the file advances nothing: re-reads are free, watermarks untouched. Consuming
  mention traffic still goes through `wave_chat_unread` as usual.
- Writes are atomic (temp file + rename): a reader never sees a torn file.
- Files older than 7 days are swept by the Host; after a restart the file re-appears on the
  next signal — re-peek instead of waiting for it.
