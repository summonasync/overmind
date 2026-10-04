---
feature: 119
slug: adapter-version-awareness
status: draft
created: 2026-10-04
---

# Spec 119 — Adapter version awareness: onboard reports stale adapter + tool drift

<goal>
A field agent on a stale MCP adapter finds out IMMEDIATELY, not after hammering wrong tools: the onboard response carries the adapter version next to the host version, with a loud warning when they diverge — «restart your MCP session».
</goal>

<scope>
- R1. Adapter side: every HTTP call to the host carries an `X-Overmind-Adapter-Version` header (adapter's assembly informational version), stamped in the same place the session header is (CreateHttpClient, 099 pattern — one stamp point).
- R2. Host side: the onboard response gains an adapter block — `adapterVersion` (from the request header), `hostVersion` (host assembly version), and when they differ a warning line: version mismatch → «tools may be missing from your toolset; restart your MCP session». A MISSING header means the adapter predates version reporting → the same restart warning with «adapter predates version reporting» phrasing.
- R3. Version comparison v1: any mismatch warns (deploy cadence is high; semantic component-wise comparison is overthinking). Challengeable: switch to major.minor-only comparison if warnings get noisy on patch releases.
- R4. Tool-drift listing (which exact tools the stale adapter lacks) — NON-GOAL v1: the host cannot see a stale adapter's toolset; the version warning is the honest signal. Revisit if the warning proves insufficient in the field (incident 2026-10-04 was the motivating case).
</scope>

<anti-scope>
- No persistence of adapter versions (no migration — the header is live-process truth; session rows stay as 099 built them).
- No tool-drift enumeration; no forced disconnect of stale adapters (the human restarts sessions).
- No changes to claim/lock semantics.
</anti-scope>

<constraints>
- The header stamps once (same stamp point as X-Overmind-Session), zero per-request allocations beyond the header itself.
- Tests: header presence/format (adapter), onboard payload warning paths (mismatch, missing header, match → no warning) on the host; pins on the warning phrasing (the quickstart/skill texts will reference it).
</constraints>

<acceptance-criteria>
- [ ] Adapter sends X-Overmind-Adapter-Version on every host call.
- [ ] Onboard payload carries adapterVersion + hostVersion; mismatch and missing-header paths warn with restart guidance; match stays silent.
- [ ] Test matrix green; result drafted.
</acceptance-criteria>

<refs>
- Incident 2026-10-04 (pre-099 adapter: no claim tools, dead set_current 405s, five misfires); 099 (CreateHttpClient stamp point, AgentSession); OnboardApi/OnboardContracts (payload); 121 (alias rules — sibling agentIdentity block work).
</refs>

<log>
- 2026-10-04 [agent] — spec drafted from the field incident; comparison and drift-list decisions challengeable/non-goal marked.
</log>