---
feature: 001
slug: example-feature
status: draft        # draft | confirmed (confirmed = вход для spec gate)
created: YYYY-MM-DD
sources: []          # чем кормился интейк: тикет, письмо, CSV, дизайн-файлы
---

# Requirements 001 — <Feature title>

<raw-inputs>
- Список исходных документов/сообщений, из которых собраны требования (со ссылками/путями)
</raw-inputs>

<requirements>
- REQ-1: Атомарное требование, outcome-level, одно на строку
- REQ-2: ...
<!-- Дедуплицировано, декомпозировано до actionable items.
     Каждое требование должно быть проверяемым. -->
</requirements>

<contradictions>
- Противоречия между источниками — НЕ решать самостоятельно, выносить пользователю
</contradictions>

<gaps>
- Чего не хватает, чтобы требования были полными (вопросы к пользователю)
</gaps>

<assumptions>
- Допущения, принятые при нормализации — каждое требует подтверждения пользователем
</assumptions>

<!--
LIFECYCLE: status: confirmed — после того как пользователь подтвердил полноту.
Дальше этот файл — вход для Phase 0.1 spec gate (вместе с feature.md). requirements.md НЕ замораживается
намертво, но изменения после confirmed проходят через пользователя.
Связка: REQ-и уточняют <requirements> из feature.md; источник истины по скоупу — feature.md,
requirements.md — интейк-нормализация сырых входов (создаётся только при месиве на входе).
-->
