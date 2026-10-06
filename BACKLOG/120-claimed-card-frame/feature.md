---
feature: 120
slug: claimed-card-frame
status: review
priority: p0
version: 1
created: 2026-10-04
---

# Feature 120 — Board card: claim frame (pin-frame successor)

<goal>
Claimed features read at a glance on the board card, per the user's design (2026-10-04):
1. **Claim line** where the status column starts, to its LEFT — the slot the brief line currently occupies («что мы делаем», slated to move to the slug row): renders `claimed by {Alias} from {Vendor}` when the feature is claimed.
2. **Border around the card** for the claimed feature (successor of the old pinned-frame affordance).
</goal>

<context>
User messages 2026-10-04: «раньше у нас была рамка вокруг pinned — я хочу такую же вокруг claimed», then the layout decision: claim text «там где статус, слева от статуса (там сейчас у нас строка бриф — что мы делая, которая уедет в слаг) — вот там будем писать claimed by {Alias} from {Vendor}» + «и бордер вокруг фичи». 099/04 shipped the claim chip and liveness; this feature is the card-level presentation.
</context>

<log>
- 2026-10-04 [agent] — User design decision in-session: claim line (claimed by {Alias} from {Vendor}) left of the status column in the brief slot, plus card border. Brief row's move to the slug row noted as context (aligns with 117).
- 2026-10-06 [agent] — Van by claude claimed the feature
- 2026-10-06 [auto] — artifact created → ready
- 2026-10-06 [auto] — task 01 taken → inProgress
- 2026-10-06 [auto] — all tasks done + spec + plan validated → review
</log>
