# Brand Book — Overmind

Design system for the Overmind web UI. Source of truth for colors, typography, layout, and component rules. Decisions were made jointly from the mockups in `/design` (Zerg-themed dev tool: strict working instrument with ambient swarm aesthetics and meme zerlings).

## 1. Brand Identity

- **Product**: local context store + control surface for AI coding agents.
- **Tone**: dark, strict, professional tool — with controlled swarm flavor. NOT a game menu.
- **Audience**: the developer running it on their own machine (tray app → browser).
- **Principle**: calm, dense working UI on top of a subtle dark swarm backdrop. Memes are seasoning, not the dish.

## 2. Color System (dark only — no light theme in MVP)

Defined as CSS variables in `src/app/globals.css`, consumed via `@theme` tokens.

| Token | Value | Usage |
|---|---|---|
| `--background` | `#0a0a0f` | App background, under background art |
| `--surface` | `#14141c` | Cards, panels, inputs |
| `--surface-raised` | `#1a1a24` | Hovered rows, dropdowns, modals |
| `--border` | `#262633` | Default borders, dividers |
| `--border-strong` | `#35354a` | Focused/hovered borders |
| `--foreground` | `#e4e4e7` | Primary text |
| `--muted` | `#8b8b96` | Secondary text, timestamps, captions |
| `--primary` | `#a855f7` | Primary actions, active nav, links |
| `--primary-hover` | `#9333ea` | Primary hover/pressed |
| `--primary-foreground` | `#ffffff` | Text on primary |
| `--success` | `#22c55e` | Active status, drift "in sync" |
| `--danger` | `#ef4444` | Delete, destructive actions, drift "drifted" |
| `--warning` | `#f59e0b` | Degraded banner, attention |
| `--info` | `#60a5fa` | Notes, neutral highlights |

Role tokens — *who* is talking, as opposed to the entry-type tokens below (*what* the record is). Both ship in `globals.css`. `--color-agent` and `--color-type-idea` are the same hex, and so are `--color-marshal` and `--color-type-audit` — consistent with the "reuse the hue, don't mint a new one" discipline research-184-01 §8 set for the journal. The rule that keeps the collision readable is about which token name you reference, not the color: role surfaces cite the role token, entry surfaces cite the type token, even when they render identical pixels.

| Token | Value | Usage |
|---|---|---|
| `--color-agent` | `#facc15` | Worker/embedded agent identity: assistant messages (`features/agent-chat/lib/message-role.ts`), wave-chat worker role, `Badge tone="agent"`, ceremony-stepper and feature-board agent dots. 11.96:1 on `--surface` — safe as text at any size in the type scale. |
| `--color-marshal` | `#2dd4bf` | Marshal role only: wave-chat marshal role, `Badge tone="marshal"`, marshal dots. 9.84:1 on `--surface` — same, safe as text. Never reuse for an entry type; the audit type has its own token even though the hex is identical. |

Entry type semantics (tokens `--color-type-*`; Idea/Audit/Tombstone shipped but undocumented until this pass):

| Type | Color | Token | Usage rule |
|---|---|---|---|
| Note | `#60a5fa` (info blue) | `--color-type-note` | Default record tone; also the journal stream and the capture overlay identity (research-184-01 §8: journal rides the note hue, no new color minted in v1). |
| Decision | `#f59e0b` (amber) | `--color-type-decision` | Badges and tree dots. |
| Gate | `#22c55e` (emerald) | `--color-type-gate` | Badges and tree dots. |
| Guard | `#ef4444` (red) | `--color-type-guard` | Badges and tree dots. |
| Skill | `#a855f7` (primary violet) | `--color-type-skill` | Badge tone; shares the primary hue, so a skill chip reads as "action-adjacent" — don't use it for CTAs. |
| Idea | `#facc15` | `--color-type-idea` | `Badge tone="idea"`, stepper/board dots, select tone. Informal-lane cards only; never for agent role (that is `--color-agent`). |
| Audit | `#2dd4bf` | `--color-type-audit` | `Badge tone="audit"`, stepper/board dots. Review and audit entries. |
| Tombstone | `#71717a` | `--color-type-tombstone` | **Reserved, not consumed.** `ENTRY_TYPE_META` renders tombstones through the `muted` tone (`text-muted/70`), and no component references this token yet. 3.79:1 on `--surface` — it does *not* clear the text floor below, so if it ever gets wired up it goes on a chip fill or a glyph, never on body text. Same hex the old `--muted` row claimed. |

Planning ceremony sections (markdown preview) are tinted through semantic aliases of the same token family — opening/closing tag names and section content share the section color:

| Token | Source | Sections |
|---|---|---|
| `--color-ceremony-goal` | `--color-success` | goal, acceptance-criteria, tasks, verification |
| `--color-ceremony-requirements` | `--color-warning` | requirements, obstacles, decisions |
| `--color-ceremony-non-goals` | `--color-danger` | non-goals, anti-scope, risks |
| `--color-ceremony-context` | `--color-info` | context, constraints, refs, open-questions, findings |
| `--color-ceremony-scope` | `--color-primary` | scope, log, approach |

Rules:
- No hardcoded colors in components — always CSS variables / theme tokens.
- Primary is reserved for the main CTA per view and active navigation. Everything else is ghost/secondary.
- Text on `--surface` keeps ≥ 4.5:1: `--foreground` 14.43:1, `--muted` 5.43:1. Nothing dimmer than `--muted` for body text. Two shipped pairs sit under the floor and neither may carry text: `--color-type-tombstone` (3.79:1 — chip fill or glyph only) and `--primary-hover` (3.40:1 — fill/press only).
- `--primary` is the tight one — 4.63:1 clears AA text by 0.13. Violet text on surface never goes below `text-sm`, never dims through an opacity modifier (`text-primary/70`). `--primary-hover` `#9333ea` as a tint today means a 16px icon glyph (`Brain` in `projects-rail.tsx` and `project-tree.tsx`), which the 3:1 non-text floor covers — as text it would not.
- White on `--primary` is **3.96:1** — below the 4.5 text floor. The filled CTA is the one place this system knowingly ships failing label contrast (button text is `text-sm`). Recorded here, not fixed in this pass: darkening `--primary`, switching the label off `#ffffff`, or lifting labels to large-text sizes is a token decision for the owner, not a docs edit.
- Badge chips set `text-{hue}` on a 10% tint of the same hue. The table measures flat pairs against solid panels; tinted chips were not re-measured in a browser.

### 2.1 Measured contrast

Re-derived 2026-11-18 from the hexes in `src/app/globals.css` with the WCAG 2.x relative-luminance formula: sRGB channel `/255`, linearise at the 0.03928 knee (`c/12.92` below it, `((c+0.055)/1.055)^2.4` above), `L = 0.2126R + 0.7152G + 0.0722B`, ratio `(max(L₁,L₂)+0.05)/(min(L₁,L₂)+0.05)`. AA floors: 4.5:1 normal text, 3:1 large text (≥ 24px, or ≥ 18.66px bold) and non-text UI.

| Foreground | Hex | on `--surface` `#14141c` | on `--background` `#0a0a0f` |
|---|---|---|---|
| `--foreground` | `#e4e4e7` | 14.43:1 | 15.57:1 |
| `--muted` | `#8b8b96` | 5.43:1 | 5.86:1 |
| retired `--muted` (now `--color-type-tombstone`) | `#71717a` | **3.79:1** | 4.09:1 |
| `--primary`, `--color-type-skill` | `#a855f7` | 4.63:1 | 4.99:1 |
| `--primary-hover` | `#9333ea` | **3.40:1** | 3.67:1 |
| `--color-agent`, `--color-type-idea` | `#facc15` | 11.96:1 | 12.90:1 |
| `--color-marshal`, `--color-type-audit` | `#2dd4bf` | 9.84:1 | 10.61:1 |
| `--success`, `--color-type-gate` | `#22c55e` | 8.04:1 | 8.67:1 |
| `--warning`, `--color-type-decision` | `#f59e0b` | 8.53:1 | 9.20:1 |
| `--info`, `--color-type-note` | `#60a5fa` | 7.20:1 | 7.77:1 |
| `--danger`, `--color-type-guard` | `#ef4444` | 4.87:1 | 5.25:1 |
| `--primary-foreground` on `--primary` | `#ffffff` / `#a855f7` | **3.96:1** | — |

The `--background` column is reference only — content never sits on bare background (§3: panels stay solid `--surface`). Bold is below the 4.5:1 text floor.

The numbers this section used to carry were never measured: it claimed `--foreground` = 15.9:1 (actual 14.43:1 on surface, 15.57:1 on background) and `--muted` = 4.8:1 for a `#71717a` token. `#71717a` measures 3.79:1 and would have broken the floor the same sentence set; the shipped `#8b8b96` clears it at 5.43:1, so the code was right and the doc was the broken half.

## 3. Background Art & Brand Marks

Assets live in `web/src/shared/assets/brand/` (RGBA with alpha where applicable; `background.png` is opaque).

- `background.png` (dark swarm nebula) is used as the app backdrop at **5–8% opacity**, blended over `--background`. It must never sit under dense text directly — content panels stay on solid `--surface`.
- `logo-mark.png` (Overmind swirl) at the top of the sidebar, ~32px, next to the wordmark. `logo-full.png` (wordmark + swirl + tagline) exists for wider placements.
- Sidebar Kerrigan: a narrow vertical strip along the left edge of the sidebar, masked with a horizontal gradient fading into `--background`. Decorative only, `pointer-events: none`, hidden below `lg` breakpoint. Random variant per page load — pool of 7 (`kerrigan/kerrigan-1..7.webp`, 480w, picked client-side after mount).
- `kerrigan-avatar.png`: round avatar crop for the topbar user chip.
- Tagline **"One context. Many minds."** in muted small caps under the sidebar logo.
- Random Kerrigan/Overmind quote at the bottom of the sidebar, `--muted`, small. Quotes pool: "Knowledge is power. And I intend to take it all." — Kerrigan; "Evolution through understanding." — Overmind; "Same data. Different minds. A higher intelligence." — Overmind.

## 4. Zerlings (agent mascots)

Chibi zerling sprites — already extracted from `design/meme-zerlings-assets.png` into `web/src/shared/assets/zerlings/` (RGBA, transparent background preserved, grid artifacts cleaned): `yelling`, `smug`, `confused`, `sleeping`, `angry`, `love`, `surprised`, `menacing`, `silly`, `celebrating`. No blend-mode tricks needed — sprites carry real alpha.

Usage — mascots appear ONLY in these slots:

| Context | Pose | Size |
|---|---|---|
| Empty states | confused | 96px |
| Loading / busy | running or surprised | 96px |
| Degraded banner (vector search down) | sleeping | 64px |
| Errors / operation failed | yelling | 64px |
| Success toasts / activation done | celebrating | 64px |
| Idle "agents online" status in topbar | smug (next to status text) | 24px (chip) |

Size rule: status displays (empty, loading, error, degraded banner, success feedback) may go big — 64–96px. Chips and small inline confirmations (e.g. "Settings saved" next to a form) stay at 24px.

Rules: never inside tables/data lists, never more than one per viewport region. They are reactions, not decoration.

## 5. Typography

- Sans: system stack (`ui-sans-serif, system-ui, sans-serif`) — zero font payload, native tool feel.
- Mono: `ui-monospace, SFMono-Regular, Menlo, monospace` for disk paths, entry IDs, versions, code snippets.
- Scale: xs 12 (captions, badges, quotes) · sm 14 (body secondary, table cells) · base 16 (body primary) · lg 18 (subheadings) · xl 20 (card titles) · 2xl 24 (page titles).
- Line-height 1.5 body / 1.25 headings; weights 400/500/600/700.
- Page title pattern: 2xl semibold + one-line muted description (as in mockups).
- Project workspace pattern: mono sm semibold project name above the section tabs; each tab opens with a one-line muted description — the tab title is omitted and the name stays the only h1.

## 6. Layout

- **App shell**: fixed left sidebar (~240px, collapsible to icons later) + topbar (~56px) + content area + optional right info panel (~280–320px, collapsible).
- Sidebar is a solid `--surface` panel with a full-strength `--border` right separator and elevation shadow — it must read as a separate surface from the swarm backdrop.
- **Topbar**: global search box (⌘K, FTS across projects/entries/kits), right side — system status ("System Healthy / degraded", zerling smug icon), status dot.
- **Right info panel**: page-specific overview (counts, recent activity, search tips). Collapsible; pages work fully without it.
- Density: data-dense. Tables with `text-sm`, row height ~40px, `surface` rows, `surface-raised` hover, `--border` row separators.
- Spacing: 4px base grid — gap-2 (8) for control clusters, gap-4 (16) card padding, gap-6 (24) section separation, p-6 page padding.
- Content max-width: none (tool UI fills viewport); internal max-w-prose only for markdown preview.

## 7. Components

- Shared primitives live in `src/shared/ui/` (Button, Input, Textarea, Select, Modal, Badge, Field, EmptyState, ErrorText). All take tokens, no hardcoded values.
- Button variants: `primary` (violet fill, white text, hover `--primary-hover`), `secondary` (surface fill, border, foreground text), `ghost` (transparent, muted text, hover surface-raised), `danger` (danger border/text, danger/10 fill on hover).
- Focus: `focus-visible:ring-2` ring in `--primary` at 40% opacity, offset 2. Non-negotiable.
- Badge tones: neutral / success / danger / warning / info + type-colored badges for entry types.
- Modal: surface-raised panel, border-strong, backdrop black/60, max-w-lg, esc + overlay click to close.
- Degraded banner: warning-tinted panel (`warning` border + `warning/10` fill), sleeping zerling 64px, dismissible.

## 8. Icons

- Lucide line icons, 16–20px, stroked `currentColor`, tinted violet (`--primary`) for active/nav states, `--foreground`/`--muted` otherwise.
- Entry type icons: Note=`file-text`, Decision=`git-branch`, Gate=`shield-check`, Guard=`lock`, Skill=`sparkles`, tinted with the type color.

## 9. Accessibility

- WCAG **2.2** AA minimum (was written as 2.1 here until this pass — the crew contract and the journal feature both apply 2.2; the two extra clauses we lean on are named below, the rest of the 2.1→2.2 delta — dragging movements, redundant entry — is not touched by this UI yet).
- **Target Size Minimum (2.5.8, AA)**: every pointer target is at least 24×24 CSS px, transparent padding counted. The journal triage controls and the sidebar nav chips are the tightest cases — judge them by the rendered box, not by the glyph inside it.
- **Focus Not Obscured — Minimum (2.4.11, AA)**: a focused element is never completely hidden by author-created content. Two surfaces can swallow focus here: the sticky topbar (`z-20`, solid backdrop-blurred panel) and the journal capture overlay. Scroll a focused control clear of both before asserting a keyboard path works.
- Visible focus rings everywhere interactive; full keyboard navigation for nav, tables, modals.
- Icons in icon-only buttons require `aria-label`.
- `prefers-reduced-motion`: no background art animation, no transition on the Kerrigan mask.

## 10. Do / Don't

- Do keep content panels solid — art lives behind the shell, not under text.
- Do use exactly one primary action per view.
- Do keep zerlings small and in their slots (§4).
- Don't port the ornate chitinous frames from `design/palletes (1).png` into the app — reference only, game-menu aesthetic, rejected for the tool.
- Don't use the metallic icon set from `design/palletes (3).png` — Lucide + violet tint instead.
- Don't add a light theme in MVP.
- Don't hardcode colors, spacing, or z-indexes per component — tokens only.
