---
feature: 119
slug: adapter-version-awareness
status: validated
created: 2026-10-05
validated: 2026-10-06
---

# Result 119 — Adapter version awareness: onboard reports stale adapter + tool drift

## What shipped

A stale adapter can no longer fail silently:

- **Adapter side:** every host call carries `X-Overmind-Adapter-Version` (assembly informational version) stamped unconditionally in `AdapterSessionHandler.SendAsync` — the same single pipeline point as `X-Overmind-Session` (099 pattern). The version lives in `AdapterVersion.Current` (single source; Program.cs ServerInfo now uses it too — local reflection duplicate removed).
- **Host side:** the onboard response carries an adapter block — `adapterVersion` (request header, null when absent/blank), `hostVersion`, and a warning line: mismatch → «tools may be missing from your toolset; restart your MCP session»; missing/blank header → «adapter predates version reporting; restart recommended»; match → no warning. Phrases pinned from constants through both doors.
- **LLM-visible loop:** the `overmind_onboard` tool renders the adapter line whenever the warning is non-empty (healthy onboard adds zero content; an old host without the block renders nothing — covered by a dedicated test). JSON payload alone is invisible to the agent; the render is what makes «finds out IMMEDIATELY» real.
- **Version semantics:** SDK-default `InformationalVersion = 1.0.0+<git commit>` (verified on the built binary) — rebuilds of one tree compare byte-equal, a stale adapter binary diverges by commit hash → warning. Exactly the 2026-10-04 incident class.
- **Quickstart:** one line teaches the restart guidance to text-reading agents (review nit, closed in-tree).

## Review rounds

- Light review PASS-WITH-NITS 0 blockers (audits/review-119-01.md): single stamp point verified against the client-factory pipeline, scope-plus render accepted (early-exits keep healthy onboards clean), version-comparison safety checked on binaries, test race (static AdapterSession.Id) closed via a shared xUnit collection, all contract pins intact.
- Coder-caught flake fixed in-round: the new test class shared the static session id with SessionLifecycleTests → both sequenced in `AdapterSessionState`.

## Gates (re-run by orchestrator)

build 0w/0e, Mcp 494 (+8), Api 661 (+3, incl. quickstart line re-run), format green on all touched projects.

## Post-release click-check

1. Fresh adapter vs fresh host (one pack): onboard response carries `adapterVersion == hostVersion`, no warning, tool renders nothing extra.
2. Old adapter (previous pack) vs fresh host: onboard warns «adapter predates version reporting…» (old adapters send no header) — and a NEW adapter against an old host warns via the rendered line in the tool output.
3. Simulated mismatch (hand-crafted header): warning names the toolset risk and the restart action.
4. A regular feature mutation still carries both `X-Overmind-Session` and the version header (single stamp point, no regression to claims).

<log>
- 2026-10-06 [agent] — closeout: review PASS-WITH-NITS 0 blockers (audits/review-119-01.md), quickstart nit closed in-tree (Api 661 re-green), result drafted. Feature goes to review; done-flip waits on the human tap.
</log>