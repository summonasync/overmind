---
feature: 124
slug: custom-scrollbars
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Spec 124 — Custom scrollbars app-wide: styled thin scrollbar instead of the stock default

<goal>
Replace the stock browser scrollbars with one thin, brand-consistent scrollbar across the whole Overmind web SPA: transparent track, muted thumb (design-call #221 token) with a brighter hover state — on the page scroll and every inner scroll container. Pure CSS in the global Tailwind 4 layer, zero JS, zero runtime cost.
</goal>

<scope>
All scrollable surfaces of the web app (single global style, no per-component overrides):

- Page-level document scroll — every page (planning, attention/notification center, issues, entries, settings, search, projects, kits).
- Inner `overflow-y-auto` containers: app sidebar nav and its project subtree (`max-h-[50vh]`), right info panel, modal overlay and xl modal body, markdown preview panes in entry/task/artifact/feature editors, feature workspace log (`max-h-[55vh]`), task editor dependency list (`max-h-44`).
- Horizontal scrollables: mobile nav, markdown code blocks.
- Any future scroll container — the style is global and unqualified, so new containers inherit it with no per-component work.

Single source of truth: `web/src/app/globals.css` `@layer base`. No scrollbar CSS anywhere else.
</scope>

<anti-scope>
- No JS scroll-jacking, custom scroll physics, smooth-scroll libraries.
- No per-project or per-feature scrollbar theming — one brand look for the app.
- No touch/overlay scrollbar re-styling on mobile (the OS owns those).
- No `scrollbar-gutter` changes (adding `scrollbar-gutter: stable` alters page layout — out).
- No `.ts`/`.tsx` component changes; no theme-token additions or rewrites; no light/dark theme system work.
</anti-scope>

<constraints>
- CSS-only, additive edit inside the existing `@layer base` block of `web/src/app/globals.css`. The file is string-pinned by `web/tests/entry-tombstone.test.mts` (asserts `--color-type-tombstone:` via `includes`), so existing content must not be removed, moved or reformatted.
- Webkit: unqualified `::-webkit-scrollbar` / `-track` / `-thumb` / `-thumb:hover` / `-corner` pseudo-element rules (pseudo-element styles do not inherit; unqualified selectors cover every box that scrolls, both axes).
- Firefox: `scrollbar-width: thin` and `scrollbar-color` on `:root` — both properties inherit to all descendants.
- Colors reference `@theme` tokens only (`var(--color-*)`), zero hardcoded hex in the new rules. Thumb base: `--color-muted` (#8b8b96, design call #221). Hover: brighten via `color-mix(in oklab, ...)` with `--color-foreground`, matching the existing `::selection` precedent in the same file.
- Webkit thumb width/height: 8px on both axes; Firefox uses the `thin` keyword. Track: transparent.
- Tailwind 4, static-export SPA — no new dependencies, no client components, no config changes.
</constraints>

<acceptance-criteria>
- [ ] Every scrollable surface (page scroll plus all containers listed in scope) renders the styled thin scrollbar; no stock default scrollbar remains anywhere in the app — verified in a Chromium browser and Firefox.
- [ ] Track transparent; thumb uses `var(--color-muted)`; hover state visibly brighter via `color-mix` on theme tokens; no hardcoded hex in the new rules.
- [ ] Firefox coverage: `scrollbar-width: thin` + `scrollbar-color` on `:root`; an engine without webkit pseudo support still shows styled colors via `scrollbar-color`; an engine with neither degrades to stock honestly (no broken visuals).
- [ ] No layout shift: reserved-gutter model unchanged (no `scrollbar-gutter` added); webkit scrollbar 8px both axes; content does not jump beyond the stock appear/disappear behavior.
- [ ] Zero `.ts`/`.tsx` changes — diff touches `web/src/app/globals.css` (plus the new guard test file if the plan adds one).
- [ ] Thumb contrast vs `--color-background` (#0a0a0f) and `--color-surface` (#14141c) is at least 3:1 (WCAG 1.4.11 non-text); muted #8b8b96 measures ≈5.9:1 and ≈5.4:1 respectively — accepted by token math.
- [ ] `npm run lint && npx tsc --noEmit`, `npm test`, `npm run build` all green in `web/`.
</acceptance-criteria>

<refs>
- Feature body 124-custom-scrollbars (what/why).
- Design call #221 — muted #8b8b96 landed as `--color-muted` in `web/src/app/globals.css`.
- `web/src/app/globals.css` — the only styling file to edit; `@theme` tokens and the `::selection` `color-mix` precedent.
- Pin test `web/tests/entry-tombstone.test.mts` — reads `globals.css` as a string and asserts `--color-type-tombstone:`; additive edits only.
- Scroll-container inventory from the code sweep: `web/src/widgets/app-shell/ui/app-sidebar.tsx` (lines 98, 133), `web/src/widgets/app-shell/ui/info-panel.tsx` (30), `web/src/widgets/app-shell/ui/mobile-nav.tsx` (35), `web/src/shared/ui/modal.tsx` (43, 64), `web/src/shared/lib/markdown.tsx` (157), `web/src/features/planning/components/feature-workspace.tsx` (519), `web/src/features/planning/components/task-editor.tsx` (85, 102), `web/src/features/entries/components/entry-editor.tsx` (89), `web/src/features/entries/components/entry-read-modal.tsx` (55), `web/src/features/planning/components/artifact-editor.tsx` (84), `web/src/features/planning/components/feature-form.tsx` (109).
</refs>

<obstacles>
- The feature body requires "thumb visible on both light and dark themes", but the app ships dark-only (no light theme exists in `globals.css`). Groomed interpretation: token-driven colors keep the thumb readable across the current dark surface system and any future theme swap. Flagged for the human at validation.
- The orchestrator brief mentioned "/70 line-pins in globals.css" — they are not in the file. Actual pins: (a) test assertions reading `globals.css` as a string (`entry-tombstone.test.mts`), (b) `/70` opacity class strings pinned in `.tsx` source tests (e.g. `badge.tsx` muted tone). Captured as the additive-only constraint; `badge.tsx` must not be touched.
- `NotificationCenter` has no scroll container of its own (inline list on the attention page; popover inside `approval-bubble`) — it is covered automatically by the global rules.
</obstacles>

<log>
- 2026-10-06 [agent] — spec drafted from the feature body plus a code sweep of `web/src` (overflow/scroll inventory, pin-test check, token/contrast math).
- 2026-10-06 [agent] — validated with human proof: notification 1a285e5d-d1da-4b55-a303-936e82e596c6 replied 2026-10-06 13:05:10 UTC
</log>