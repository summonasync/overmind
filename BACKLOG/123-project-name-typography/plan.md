---
feature: 123
slug: project-name-typography
status: draft
created: 2026-10-06
---

# Plan 123 — Project name typography: distinct display treatment vs page headings

<goal>
Give the project name a distinct branded display treatment so `ProjectHeader` stops cloning the page h1 — one component change plus one brand-book line.
</goal>

<approach>
Single-component change. `ProjectHeader` (`web/src/features/projects/components/project-header.tsx`) renders today:

```tsx
<h1 className="text-2xl font-semibold text-foreground">{name}</h1>
```

Target shape — non-heading display element, mono stack, brand purple, compact scale, truncation for long names:

```tsx
<p className="truncate font-mono text-sm font-semibold tracking-wide text-primary">{name}</p>
```

Exact classes may be tuned by the coder within spec constraints: must resolve color from `text-primary`, use an existing font stack with character (`font-mono` preferred — it joins the feature-number identifier language), sit at or below `text-base`, and keep the wrapping `<header className="mb-6">` structure intact. The `secondary` line stays `text-sm text-muted`. No other component gains this treatment.
</approach>

<files>
1. `web/src/features/projects/components/project-header.tsx` — the treatment; the only production file that must change.
2. `web/brand-book.md` — §5 Typography: one line documenting the project-identifier pattern (mono, `--color-primary`, subordinate to the page title, one h1 per view).

No page files change: `planning-page.tsx`, `entries-page.tsx`, `page-header.tsx`, `feature-workspace.tsx` keep their composition as-is.
</files>

<css-approach>
Tailwind utility classes directly on the element in the component. Tokens come from `@theme` in `web/src/app/globals.css` (`--color-primary`, `--font-mono`) — no `@layer base` addition, no new component, no CSS file edits. If a second consumer of the pattern appears later, layering can be revisited then.
</css-approach>

<verification>
Gates, in order, from `web/`:
1. `npm run lint && npx tsc --noEmit`
2. `npm run test` (node --test over `tests/**/*.test.mts`; `project-display.test.mts` — name-resolution logic — untouched and must stay green)
3. `npm run build` (static export must succeed)

Manual pass over the three affected routes with a project selected:
- `/dashboard?project=…` — project name reads as branded identifier, "Dashboard" title dominates.
- `/entries?project=…` — same above "Entries".
- `/dashboard?project=…&feature=…` — same above the feature title; exactly one h1 in the document.
- DevTools: no new font network requests; no hydration mismatch warnings; `ProjectNameBadge` chips unchanged.
</verification>

<pin-strategy>
No string-pinned tests exist for headers (verified: no test references `ProjectHeader`, `text-2xl` or the header markup) and none are added — the change is pure presentation with zero logic; `node --test` has no DOM harness in this repo. The existing `project-display.test.mts` pins name resolution, not presentation, and is out of the diff.
</pin-strategy>

<risks>
- Demoting h1 → p changes the accessibility outline; desired outcome (one h1 per view, `<header>` landmark retained), but reviewers should confirm no CSS elsewhere targets `ProjectHeader h1`.
- The 2xl → text-sm height reduction shifts content upward on the three routes — static restyle, identical before/after hydration, so no CLS; visual QA covers it.
- If the coder reaches for a new font package or font file — STOP: that is anti-scope; the display-font question is a human flag recorded in the spec obstacles.
</risks>

<log>
- 2026-10-06 [agent] — plan drafted alongside spec; decomposition into tasks deferred until after the consolidated human validation sitting.
</log>
