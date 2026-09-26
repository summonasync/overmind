---
feature: 088
slug: bell-approval-quick-actions
status: done
priority: p1
version: 1
created: 2026-09-26
---

# Feature 088 — Колокольчик: кнопки ✓/✗ на approval-нотификациях (аппрув/отказ одним кликом = 077-proof)

<goal>
Replying to approval notifications by typing is friction. The bell gets two quick-action buttons on approval-kind items — green check (approve) / red cross (reject) — sending a canonical reply text with one click. The click IS the 077 validation proof (server-side proof search reads the reply; no backend change).

<requirements>
- On approval-kind notifications in the bell: two buttons — approve (green check) / reject (red cross). Click → POST reply with canonical text "approved" / "rejected" (English, per project rules).
- One-reply semantics respected: after either click the buttons disable and the chosen state shows (existing reply display already handles "shows after set").
- Question-kind notifications keep the free-text reply input (they ask, not request approval).
- Known limitation (consistent with the resolved W-2): the server does not distinguish "rejected" from any other reply in the proof search — rejecting does NOT revoke proof capability; the agent must read the text. Backend enforcement stays out (user decision 2026-09-26: semantics as-is until the internal copilot).
- Board/feature deep links unchanged; a11y labels on both buttons.
</requirements>

<non-goals>
- Backend proof changes (077 stays as-is); the 087 attention page (its inline buttons reuse this component/pattern); OS notifications (084).
</non-goals>

<refs>
- User request 2026-09-26: «зелёная галочка/красный крестик — аппрув, нет».
- 020 (bell, reply path), 077 (reply = proof), 087 (attention page will reuse the buttons).
</refs>

<log>
- 2026-09-26 [auto] — artifact created → inProgress
- 2026-09-26 [agent] — by user request in-session (batch flip): 1/1 таск done, review PASS-WITH-NITS 0 блокеров/ворнингов, S-1 title-хинт применён оркестратором, result 3aeb609b, известное ограничение reject-семантики задокументировано (W-2 юзер-решение от 077)
</log>
