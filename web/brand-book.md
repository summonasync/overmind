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
| `--muted` | `#71717a` | Secondary text, timestamps, captions |
| `--primary` | `#a855f7` | Primary actions, active nav, links |
| `--primary-hover` | `#9333ea` | Primary hover/pressed |
| `--primary-foreground` | `#ffffff` | Text on primary |
| `--success` | `#22c55e` | Active status, drift "in sync" |
| `--danger` | `#ef4444` | Delete, destructive actions, drift "drifted" |
| `--warning` | `#f59e0b` | Degraded banner, attention |
| `--info` | `#60a5fa` | Notes, neutral highlights |

Entry type semantics:

| Type | Color |
|---|---|
| Note | `#60a5fa` (info blue) |
| Decision | `#f59e0b` (amber) |
| Gate | `#22c55e` (emerald) |
| Guard | `#ef4444` (red) |
| Skill | `#a855f7` (primary violet) |

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
- Text on `--background`/`--surface` must keep ≥ 4.5:1 contrast (`--foreground` = 15.9:1, `--muted` = 4.8:1 on `#14141c` — do not go dimmer than `--muted` for text).

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

- WCAG 2.1 AA minimum for text contrast (see §2 floor: nothing dimmer than `--muted`).
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
