---
id: 74eb89d8-b7f6-46da-99c1-a75ddd68b677
type: note
title: planning_validate требует notify-reply proof даже при humanGate=off и чат-апруве
tags: agent-runtime, planning, ux, validation
createdAt: 2026-09-29T16:50:03Z
updatedAt: 2026-09-29T16:50:03Z
---

2026-09-29, Qwen Code runtime (Bob.AI-daily-assistant session): планировочный разрыв между ceremonial-словарём и proof-механикой.

Ситуация: проект humanGate=off (onboard: Validation.humanGate=off), skill overmind-ceremony трактует «humanGate off» как «chat approval → planning_validate». Фактически planning_validate(spec) вернул 409 ValidationProofRequired: «send overmind_notify(kind=approval) and wait for the human's reply in the notification center, or validate via the UI» — даже при явном чат-апруве владельца в той же сессии (poll ask_user_question, «Апрувлю, валидируй»).

Проблема: (1) текст onboard-блока Validation (humanGate=off) не отражает, что proof через notification center всё равно обязателен для spec/plan; (2) агент может бесконечно ретрайвать validate, считая 409 ошибкой UI-гейта; (3) легитимный чат-апрув остаётся непроверяемым доказательством — двойной стандарт vs UI-дверь (ей proof не нужен).

Запрос: либо документировать в payload/описании tools, что при humanGate=off proof-цикл notify→reply обязателен (тогда поправить и skill-текст crew), либо принимать явный флаг «chat-approved» с logNote-провенансом как proof. Обход сейчас: notify kind=approval + poll overmind_replies, затем validate.