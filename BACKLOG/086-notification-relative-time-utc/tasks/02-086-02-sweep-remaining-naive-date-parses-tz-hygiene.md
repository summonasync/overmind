---
feature: 086
task: 02
slug: notification-relative-time-utc
status: done
review: light
created: 2026-09-26
brief: Convert 6 remaining naive date sites to the shared helper; TZ hygiene; extra pins
---

# 086/02 — Sweep remaining naive date parses + TZ hygiene

<goal>
Review-086-01 sweep found 6 pre-existing sites on naive parsing (same UTC-as-local root, helper already shipped in 086/01); plus the orphan-test follow-ups (S-1): TZ assignment after import and the review's claim correction.
</goal>

<scope>
- Convert to the shared parseInstant/format helpers: web/src/features/kits/lib/import-labels.ts:48 (formatImportedAt — visible 3h shift), web/src/features/planning/components/feature-workspace.tsx:391 (frozenAt), web/src/features/planning/components/artifact-editor.tsx:76 (frozenAt), web/src/pages/projects-page/ui/projects-info-panel.tsx:42, web/src/features/projects/components/project-detail.tsx:20, web/src/pages/entries-page/ui/entries-info-panel.tsx:63 (calendar dates — off-by-a-day risk at UTC+3). Keep each site's existing display format; only parsing changes.
- tests/relative-time.test.ts: move process.env.TZ assignment before imports (or set via a child bootstrap) so the pin cannot silently rot; add a frozenAt-style pin (Z-less + fractional seconds) and a calendar-date pin (same UTC instant date boundary at UTC+3 renders the correct local date).
- Result-artifact claim correction: record that 5 of 8 pins (not 2) fail under the naive parse (reviewer-verified).
</scope>

<anti-scope>
- Backend Z serialization (microbatch #96); 088 bell buttons (separate task, parallel); 024 bucket rules.
</anti-scope>

<constraints>
- TypeScript strict; English-only; no comments; no new packages; npm run lint && npx tsc --noEmit && npm run build && npm test green.
</constraints>

<acceptance-criteria>
- [x] All 6 sites parse via the shared helper; display formats unchanged
- [x] grep web/src shows no remaining bare new Date(isoString) on API-provided timestamps (list the audited exceptions)
- [x] New pins: frozenAt Z-less + fractional; calendar-date boundary at UTC+3
- [x] TZ assignment precedes module imports (or equivalent robust setup)
- [x] lint + tsc + build + test green (test script now in package.json)
</acceptance-criteria>

<refs>
- audits/review-086-01.md (sweep list + S-1/S-2); 086/01 helper (shared/lib/relative-time.ts).
</refs>

<obstacles />

<log />

<log>
- 2026-09-26 [agent] — Picked up by next-js-coder: converting the 6 review-swept naive date sites to the shared helper, TZ-hygiene bootstrap for the pin suite, plus frozenAt and calendar-boundary pins.
- 2026-09-26 [agent] — Fix landed. 6 naive sites converted to the shared helper, display formats unchanged: formatImportedAt (kits/lib/import-labels.ts — parseInstant + toLocaleString, raw-string fallback preserved), frozenAt badges (feature-workspace.tsx, artifact-editor.tsx), project created dates (projects-info-panel.tsx, project-detail.tsx), entry updated dates (entries-info-panel.tsx). New shared export formatCalendarDate (parseInstant + toLocaleDateString) carries the 5 calendar sites; relative-time.ts now holds the full render vocabulary. AC2 audit: grep src/ for bare new Date(<string>) on API timestamps — zero remaining; audited exceptions: 3 numeric new Date(timestamp) constructions (relative-time.ts x2, import-labels.ts — epoch-ms args, TZ-safe) and ISO-string localeCompare sorts (entries-info-panel.tsx:27, projects-info-panel.tsx:16, board-order.ts:11, feature-dependencies-panel.tsx:70 — lexicographic, no parsing, offset-agnostic). TZ hygiene per S-1: tests/tz.ts bootstrap (process.env.TZ = "Asia/Qatar") imported as the FIRST statement of relative-time.test.ts, so the assignment executes before every other module evaluation — the pin can no longer rot on import order or runner TZ. New pins: parseInstant on Z-less 7-digit-fraction frozenAt shape; formatCalendarDate on the same frozenAt (renders UTC+3-shifted local date, naive parse would render the previous day); Z-less 21:00Z-boundary instant renders the shifted local date. Gates: npm test 11 pass / 0 fail; npm run lint 0 errors (same 6 pre-existing warnings in untouched files); npx tsc --noEmit clean; npm run build green, 9 routes static-prerendered. Changed files: web/src/shared/lib/relative-time.ts, web/src/features/kits/lib/import-labels.ts, web/src/features/planning/components/feature-workspace.tsx, web/src/features/planning/components/artifact-editor.tsx, web/src/pages/projects-page/ui/projects-info-panel.tsx, web/src/features/projects/components/project-detail.tsx, web/src/pages/entries-page/ui/entries-info-panel.tsx, web/tests/tz.ts (new), web/tests/relative-time.test.ts. No git commands run.
- 2026-09-26 [agent] — Done by next-js-coder: 6 naive sites on the shared helper (formatCalendarDate added), TZ bootstrap test precedes all imports, 3 new pins (suite now 11), S-2 correction recorded on 086/01. Gates: npm test 11/11, lint 0 errors, tsc clean, static build green. Evidence in log.
- 2026-09-26 [agent] — [orchestrator] Review corrections applied (audits/review-086-02.md): W-1 newline restored in package.json; S-1 resolved via .mts rename (explicit ESM, glob updated to tests/**/*.test.mts — npm test 16/16; MODULE_TYPELESS remains twice from .ts imports, documented as accepted instead of risky "type": "module" near Next config); S-2 noted: runner total is 16 (11 relative-time + 5 approval-reply from 088), and the audited-exceptions list gains the no-arg new Date().toISOString() at use-notification-mutations.ts:29 (client now, TZ-safe). Reviewer claim verified: all 3 new pins red under naive parse.
</log>
