---
feature: 121
slug: alias-rules-editor
status: review
priority: p0
version: 1
created: 2026-10-04
---

# Feature 121 — Alias rules: settings editor + stable personal alias in ceremony skill

<goal>
Alias rules as an editable project setting: the user writes the naming prompt («как выбирать алиас») in project settings; agents read it in the agentIdentity block and pick a STABLE personal alias per those rules. Vendor = the runtime (claude / qwen / …), alias = the agent's own chosen name — two agents of the same runtime must not look identical on the board.
</goal>

<context>
User intake 2026-10-04: «два claude by anthropic — что странно» + «мы хотели иметь промпт для выбора алиаса агентами в настройках — типа alias rules». Infrastructure exists since 099: Project.AgentNamingRules (nullable) + SetClaimSettings feeds the agentIdentity block of the onboard response (AgentIdentityDefaults.GenericNamingHint is the empty fallback). Missing: (a) the settings UI never exposes the field — the user cannot write their gachi-core rules anywhere; (b) the ceremony skill says «alias+vendor come from the agentIdentity block» but never tells the agent to CHOOSE a personal stable alias and sign with it.
</context>

<log>
- 2026-10-06 [agent] — Van by claude claimed the feature
- 2026-10-06 [auto] — artifact created → ready
- 2026-10-06 [auto] — task 01 taken → inProgress
- 2026-10-06 [auto] — all tasks done + spec + plan validated → review
</log>
