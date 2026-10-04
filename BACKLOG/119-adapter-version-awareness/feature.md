---
feature: 119
slug: adapter-version-awareness
status: inProgress
priority: p1
version: 1
created: 2026-10-04
---

# Feature 119 — Adapter version awareness: onboard reports stale adapter + tool drift

<goal>
Agents running stale MCP adapters silently break against the current host: new tools are absent from their toolset (planning_claim_feature, planning_delete_artifact), while removed tools linger and fail with confusing 405s (planning_set_current → Method Not Allowed). The field agent cannot tell "tool misuse" from "stale adapter" — and neither can the orchestrator.
</goal>

<context>
Field incident 2026-10-04: an orchestrator session on a pre-099 adapter tried to claim a feature, found no claim tool in its toolset, and hammered planning_set_feature_priority five times (harmless but noisy) before diagnosing the real cause. User verdict: «Заводи фичей». The host knows what the adapter's toolset should be (ListTools round-trip or session handshake); the onboard/agentIdentity payload could carry adapter version + drift list («tool X is missing from your adapter — restart the session; tool Y was removed — stop calling it»).
</context>

<log>
- 2026-10-04 [auto] — artifact created → inProgress
</log>
