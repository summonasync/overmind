---
feature: 084
task: 06
slug: notification-human-actions
status: done
review: light
created: 2026-09-28
brief: Settings form: nudge mode select + nag minutes control
---

# 084/T4 — Web: settings form controls for nudge discipline

<goal>
Expose notificationNudgeMode + notificationNudgeIntervalMinutes on the host settings form, matching the existing form patterns.
</goal>

<scope>
- web/src/features/settings/types.ts + api.ts — extend the settings shape.
- web/src/features/settings/components/settings-form.tsx — select quiet/nag/mini + numeric minutes field (visible/enabled only for nag), immediate PUT, validation errors per the form's existing error surface.
</scope>

<anti-scope>
- Project-level settings; bubble rendering.
</anti-scope>

<acceptance-criteria>
- [x] Mode select + minutes field round-trip through PUT/GET; minutes disabled unless nag.
- [x] Invalid values surface the form's existing error styling.
- [x] npm run lint && npx tsc --noEmit green; npm test green.
</acceptance-criteria>

<refs>
- Plan 084 T4; T1 contract (key names, defaults, validation).
</refs>

<log>
- 2026-09-28 [agent] — [agent] 084/T4 implementation started: types + api + settings-form extended with nudge mode/interval.
- 2026-09-28 [agent] — [agent] Nudge mode select + minutes field wired into SettingsFormValues and the settings PUT body; GET values seed the form state. Minutes field renders only for nag (per scope: visible/enabled only when nag), so it is unreachable otherwise.
- 2026-09-28 [agent] — [agent] Submit blocks with an Error when minutes is not an integer > 0; message renders through the form's existing ErrorText surface (danger banner, role=alert), cleared on input change.
- 2026-09-28 [agent] — [agent] npm run lint: 0 errors, 6 pre-existing warnings in unrelated files; npx tsc --noEmit clean; npm test 16/16 pass.
- 2026-09-28 [agent] — [agent] 084/T4 done. types.ts: NotificationNudgeMode union + nudge keys on SettingsResponse/SettingsFormValues (084/T1 contract verbatim). api.ts: PUT body extended. settings-form.tsx: Notifications section with mode select, minutes input shown only for nag, submit-time validation (integer > 0) through the existing ErrorText surface, state reset follows the syncedSettings pattern. Verified: lint 0 errors (6 pre-existing warnings elsewhere), tsc --noEmit clean, npm test 16/16.
- 2026-09-28 [agent] — [agent] review APPROVED-WITH-NITS; both nits fixed in-tree by orchestrator: (1) honest parse — Number + Number.isSafeInteger instead of parseInt (5e2/3.9 no longer silently truncate); (2) leaving nag with invalid minutes resets the field to the server value and any mode change clears intervalError. lint+tsc+test 16/16 green. Form-growth note accepted — future refactor candidate.
</log>
