---
feature: 097
task: 02
slug: clear-conversations
status: done
review: light
created: 2026-09-29
depends_on: 01
brief: Web: Clear conversations button + Show hidden archive toggle + mutation + live e2e + architect light + result draft
---

# 097/02 — Web: Clear conversations button + Show hidden archive + closeout

<goal>
The human door gets the button: "Clear conversations" in the notification center header hides replied rows from the default view; a "Show hidden" toggle reveals the dimmed archive; live e2e proves the whole chain, the result artifact closes the feature.
</goal>

<context>
Spec ded04f10 v2 (validated), plan 7a436aba — task 2 of 2, AFTER task 01 (needs the endpoint + includeHidden contract). Current surfaces: features/notifications slice (api.ts, queries.ts notificationKeys factory, types.ts, lib/use-notification-mutations.ts with the 096 helpers patchList/restoreList/settleList + clearRead precedent), widgets/app-shell/ui/notification-center.tsx (header buttons row: Mark all read / Clear read; kind filter), notification-row.tsx (dim + × with DELETE). Hidden items arrive with HiddenAt on the item type once task 01 lands.
</context>

<scope>
- types.ts + api.ts: HiddenAt on NotificationItem (nullable), hideConversations() → { hidden }, list query gains includeHidden param (query key stays in the notificationKeys family — single invalidation covers both views; decide factory shape per plan risk note, document in obstacles).
- lib/use-notification-mutations.ts: hideConversations mutation — NO optimistic patch (server-authoritative hidden-set), onSuccess invalidate notificationKeys.all (096 settle idiom); ErrorText surface for failures.
- notification-center.tsx: "Clear conversations" button next to "Clear read" — disabled when repliedCount === 0 (derived from items: replyText != null); "Show hidden" toggle switching the list between default and includeHidden views (client state; hidden rows render dimmed; × keeps working from the archive).
- notification-row.tsx: dimmed style when HiddenAt present (archived); existing × path unchanged.
- Web tests: pin what is unit-testable (button disable logic / toggle state shape) following the node:test patterns in web/tests/.
- Verify: cd web && npm run lint && npx tsc --noEmit && npm run build && npm test.
- Live e2e (orchestrator-level guidance; the coder runs it): isolated dev host (scratch HOME, Overmind__BindUrl, kill by exact PID; prod 4761 untouched) — create notification → reply it → POST clear-conversations → { hidden: 1 } → default GET list empty of it → includeHidden=true shows it → agent replies endpoint still returns it → second call { hidden: 0 }.
- architect-review light pass over the full 097 delta (audits/architect-review-2026-09-29-097.md).
- Result artifact (kind=result, draft): outcome, deviations, UI click-check list for the user (button, toggle, dimmed archive, × from archive).
- Closeout: tick ACs via planning_patch_artifact, log via planning_append_log; do NOT validate the result or flip the feature — orchestrator gates those.
</scope>

<anti-scope>
- Backend code (task 01), optimistic patches, unhide action, agent-door surface, 096 buttons semantics (Clear read unchanged).
</anti-scope>

<constraints>
- FSD: features/notifications data logic, widgets/app-shell presentation, barrel imports. React Query v5, TypeScript strict, no comments, English-only, no new packages. Static export rules (plain anchors). 084/088/096/093 invariants untouched.
</constraints>

<acceptance-criteria>
- [x] "Clear conversations" button wired (disabled when no replied rows), hides replied rows from the default view after settle.
- [x] "Show hidden" toggle reveals the dimmed archive; × from the archive hard-deletes (096 semantics).
- [x] lint + tsc + build + web tests green; existing suite untouched-green.
- [x] Live e2e evidence captured (hide → default-excluded → includeHidden visible → replies endpoint returns → idempotent).
- [x] architect light review logged (audits/architect-review-2026-09-29-097.md); findings resolved or carried.
- [x] Result artifact written (draft); ACs ticked; log lines via planning_append_log.
</acceptance-criteria>

<refs>
- Spec ded04f10 (validated, v2); plan 7a436aba; task 01 (endpoint + includeHidden contract).
- 096 mutation helpers + center header (patterns to reuse); 093 title-counter files untouched.
</refs>

<obstacles>
- Query-key shape (plan risk note): notificationKeys.list(includeHidden = false) emits ["notifications", "list", { includeHidden }] — one family, so the 096 settleList invalidation on notificationKeys.all covers both views in a single call. notificationsQueryOptions(includeHidden) mirrors the flag; useNotifications(includeHidden) passes it through. Default-view key gained the { includeHidden: false } segment (internal shape change only; mutation patchList resolves the same default via the factory).
- Header restructure: the old items.length === 0 early return would hide the "Show hidden" toggle exactly in the post-clear state (default view empty, archive non-empty) — the header row now always renders after pending/error, and empty states moved into the list body with view-aware copy ("No notifications" / "Nothing hidden" / "Nothing under this filter").
- placeholderData: keepPreviousData on the list query so toggling views does not flash the full-screen LoadingState (verified against query-core 5.x source: status flips to success with isPlaceholderData while the new key fetches).
- hideConversations mutation is server-authoritative: no optimistic patch, onSuccess settleList; the API fn keeps the task-mandated name hideConversations() (route POST /notifications/clear-conversations) and the hook aliases the request fn as hideConversationsRequest to avoid the name clash.
- repliedCount derives from the active view's items: default view gives the true pre-clear count. In the archive view rows are a MIX of replied and unreplied — includeHidden=true returns all hidden rows, the backend applies no replied filter — but repliedConversationsCount counts by replyText regardless of hiddenAt, so the button stays enabled and a re-click is the idempotent { hidden: 0 }. (Premise corrected at closeout per review-097-02 NIT-2: the original draft claimed every archive row is replied.)
- Unit-test surface: node --test cannot import queries.ts (path aliases), so pins target the pure helpers repliedConversationsCount / isHiddenNotification in lib/conversation-archive.ts (4 new tests); toggle state and key familying are covered by tsc/build plus the orchestrator's live e2e.
</obstacles>

<log>
- 2026-09-29 [agent] — task sliced from validated plan; runs after 01.
- 2026-09-29 [agent] — coder picked up web delta of task 02
- 2026-09-29 [agent] — web delta of task 02 implemented: hiddenAt on NotificationItem, hideConversations() API + includeHidden list param, notificationKeys.list(includeHidden) factory (single family — settleList invalidate of notificationKeys.all covers both views), hideConversations mutation (no optimistic patch, onSuccess settle idiom), Clear conversations button next to Clear read (disabled at repliedCount === 0), Show hidden aria-pressed toggle driving the query param, dimmed archive rows in notification-row, header row now always rendered so the toggle stays reachable when the default view is empty. Evidence: cd web && npm run lint (0 errors; 6 pre-existing warnings in untouched files), npx tsc --noEmit clean, npm test 40/40 pass (36 existing untouched-green + 4 new pins in tests/conversation-archive.test.mts), npm run build static export green. src/** and tests/** untouched; live e2e, architect light and result artifact stay with orchestrator.
- 2026-09-29 [agent] — web delta done, e2e/architect/result stay with orchestrator
- 2026-09-29 [agent] — [orchestrator] live e2e зелёный на изолированном хосте (HOME=/tmp/overmind-097-walk, порт 4896, kill по PID 169744+169748 дочерний, прод 4761 не тронут): (1) question→reply «SQLite» → replied-строка; (2) clear-conversations → {hidden:1}; (3) default list — 0 replied-строк, unread survivor цел (unreadCount=1); (4) includeHidden=true возвращает строку с hiddenAt-штампом; (5) агентский replies endpoint ОТДАЁТ скрытую строку — 077-безопасность живая; (6) идемпотент {hidden:0}. UI-слой (кнопка/тоггл/дим) — юзер клик-чек при следующей выкладке.
- 2026-09-29 [agent] — [orchestrator] architect light APPROVED-WITH-WARNINGS (audits/architect-review-2026-09-29-097.md): WARNING-1 (50-оконный read vs permanent history — задокументирован в result как recorded decision, #140 к 087), WARNING-2 (hide≠validation инварианта непинена на 077-двери — приоритетный микробатч #139), SUGGESTION #141/#142; + result artifact fdacb923 написан (draft).
- 2026-09-29 [agent] — [orchestrator] result artifact fdacb923 (draft, 4.4k симв): outcome по двум таскам, верификация 1649/1649 + веб 40/40 + live e2e 7 пунктов, recorded decision по 50-окну, follow-ups #139-142.
- 2026-09-29 [agent] — web-дельта review PASS-WITH-NITS, live e2e зелёный (077-safe проверен живьём), architect APPROVED-WITH-WARNINGS (2 WARNING → микробатч #139 приоритет + #140 recorded decision), result fdacb923 написан — AC 6/6, таск done
- 2026-09-29 [agent] — obstacles premise corrected per review-097-02 NIT-2: includeHidden=true returns ALL hidden rows (no replied filter server-side), not only replied — conclusion unchanged, code untouched
</log>