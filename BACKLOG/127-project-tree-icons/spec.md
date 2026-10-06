---
feature: 127
slug: project-tree-icons
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Spec 127 — Project tree: purple project icons distinct from folder nodes

<goal>
Make PROJECT nodes in the project tree read as projects at a glance: a purple mind-metaphor glyph instead of the folder icon they render today, with an inactive/active color split driven by theme tokens. Directory nodes keep the plain folder look untouched. Visual layer only — no tree behavior, no restructuring.
</goal>

<scope>
- **Glyph swap on the project node.** In `web/src/features/projects/components/project-tree.tsx` the project branch (the `entry.projectId !== null` render, ~line 290) replaces `<FolderOpen size={14} aria-hidden className="shrink-0" />` with lucide's `Brain` at the same size: `<Brain size={14} aria-hidden … />`. `FolderOpen` today renders ONLY on project nodes, so the swap fully separates projects from folders; the now-unused `FolderOpen` import is removed.
- **Inactive project node colors.** Icon: `text-primary-hover` (token `--color-primary-hover: #9333ea` — the darker purple of the palette). Label: unchanged — keeps today's `text-muted` from the parent link. Only the icon gets an explicit color class; the label classes on the link are not edited.
- **Icon color is hover-stable.** Today the whole inactive link brightens to `foreground` on hover, icon included. After the swap the icon keeps its purple on hover (explicit class on the icon wins over the parent's `hover:text-foreground`); the label keeps today's hover brightening. Deliberate: purple marks *project-ness*, not state (065 convention — identifier, not state), so the icon does not participate in hover recolor.
- **Active project node: today's treatment, unchanged.** `border-primary bg-primary/10 text-primary` and `aria-current="page"` stay exactly as they are; the `Brain` glyph inherits `text-primary` there. No label or icon edits in the active branch.
- **Optional active brightness bump — human-gated.** The user is open to a brighter active state («можно даже поярче»). NOT in this change by default; if the human approves at result validation it lands as a tiny follow-up tweak, not a spec edit.
</scope>

<anti-scope>
- No structural extract or refactor of `project-tree.tsx` — audit flag F5 stays a separate follow-up; this feature edits the node render in place.
- No tree behavior changes: expand/collapse, selection, `saveLastActiveProjectId`, session-state hooks, data hooks (`use-project-tree`, `use-projects`) untouched.
- No new dependencies — the glyph comes from the already-installed `lucide-react` (^1.42.0).
- Directory nodes untouched: `Folder` icon, `opacity-70`, chevrons, register (`Plus`) button, all folder-row classes stay byte-identical.
- Other icon surfaces untouched: `FolderPlus` header buttons, `RefreshCw`, `X`, `Plus`, `ChevronDown`/`ChevronRight`.
- No label color changes in either state — the body prescribes label treatment only for "as today".
- No brand-book updates — 14px tree glyph, not a documented brand pattern.
</anti-scope>

<constraints>
- Colors resolve from `@theme` tokens in `web/src/app/globals.css` only: `text-primary-hover` (#9333ea) for the inactive icon, inherited `text-primary` (#a855f7) in the active state. No hardcoded hex in the component.
- Icon mechanism stays the existing one: lucide-react imports, `size={14}`, `aria-hidden`, `shrink-0` — same footprint as the icons around it.
- Icon contrast on the dark background (`--color-background: #0a0a0f`): #9333ea clears the 3:1 non-text (WCAG 1.4.11) bar for a graphical object.
- Conditional classes go through the existing `cn` helper (already imported in the file).
</constraints>

<acceptance-criteria>
- [ ] Project rows and folder rows are distinguishable by icon alone at a glance: project = `Brain`, folder = `Folder`.
- [ ] Inactive project node: icon renders in the dark purple of `--color-primary-hover` and stays purple on hover; label renders `text-muted` and brightens on hover exactly as today.
- [ ] Active project node: renders exactly as today (`border-primary bg-primary/10 text-primary`, `aria-current="page"`) with the `Brain` glyph inheriting `text-primary`.
- [ ] Directory nodes render byte-identical to today (same icon, classes, chevron, register button).
- [ ] No unused imports remain after the swap; `npm run lint`, `npx tsc --noEmit`, `npm run test` and `npm run build` pass in `web/`.
- [ ] Any active-state brightness bump exists only if the human approved it at result validation.
</acceptance-criteria>

<refs>
- `web/src/features/projects/components/project-tree.tsx` — project node render ~line 290 (current `FolderOpen`), active/inactive link classes ~lines 285–287, directory node ~line 317, lucide import block ~lines 4–13.
- `web/src/app/globals.css` `@theme` — `--color-primary: #a855f7`, `--color-primary-hover: #9333ea`, `--color-muted: #8b8b96`, `--color-background: #0a0a0f`.
- `web/src/shared/ui/button.tsx` — existing consumer of the `primary-hover` token (utility mechanism proven).
- 065 convention via feature 123 constraints: brand purple is identifier, not state.
- Audit flag F5 (project-tree extract) — explicitly out of scope, follow-up elsewhere.
</refs>

<obstacles>
- **Glyph choice (human flag).** `Brain` exists in the installed `lucide-react` (`brain.mjs`), so no new dependency and no hand-rolled SVG is needed. `BrainCircuit` and `BrainCog` are also in the package; `Brain` is prescribed as the cleanest read at 14px (circuit/cog details muddy at that size). Human confirms at validation; swapping to a sibling glyph is a one-word change.
- **Inactive shade token (human flag).** `--color-primary-hover` is semantically a hover shade but is the only darker-purple token in `@theme`; it is used here as the "dark/muted purple" identifier color. Alternative if the human prefers a derived tone: `text-primary/70`. Either is token-driven; pick at validation.
</obstacles>

<log>
- 2026-10-06 [agent] — spec draft (fast lane: pre-approved body, mechanism and tokens verified in the tree file before drafting)
- 2026-10-06 [agent] — validated with human proof: notification c0de16d0-33a7-48ad-aa03-0c02d3767f26 replied 2026-10-06 18:53:16 UTC
</log>