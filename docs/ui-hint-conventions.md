# UI hint conventions

Rules for every piece of user-facing hint copy in `web/src`. Each rule traces to a measured
outcome of feature 257: a 253-surface inventory (`audits/ui-hints-257-inventory-2026-11-21.md`,
harness `audits/tools-257-hint-inventory.py`, tree at `aee7d413`) and its adjudication
(`audits/ui-hints-257-t2-verdicts-2026-11-21.md` plus the T1–T3 task logs). Pass totals: T2 scope
215 rows — 205 truthful untouched, 10 disclosure fixes, 0 stale, 0 lying; T1 9 kits rows — 8
truthful, 1 stale; 16 settings fields gained help. The 8 grimoire rows re-adjudicated by T3's
follow-up (7 truthful, 1 lying) sit inside T2's 215, so the pass holds one lie, not two — and the
re-adjudication is the lesson in the wording section below.

## When a plain `title=` is fine

Keep the native attribute, change nothing, when the claim is not a claim:

1. **It repeats the element's own visible text.** A truncated name or path, a reworded count.
   `pages/planning-page/ui/feature-overview-panel.tsx:50` puts `title={feature.title}` on the
   paragraph that prints `{feature.title}` — the tooltip only recovers what CSS clipped.
   Same at `features/projects/components/folder-picker-modal.tsx:165` (root path also printed as
   the muted sub-line at 171) and `features/issues/components/issue-list-row.tsx:33` (visible
   "{n} appends" at 35, title rewords it as "follow-ups"). 25 of T2's 35 mouse-only rows stayed
   plain title on exactly this ground; the per-row list is in the T2 verdict artifact.
2. **It adds detail to a value assistive tech already announces** — exact calendar date under a
   relative label (`feature-board.tsx:412`), `aria-valuenow` on a progressbar
   (`features/agent-chat/components/agent-budget-meter.tsx:58`), `aria-pressed` beside the filter
   button title (`widgets/app-shell/ui/projects-rail.tsx:250-252`).
3. **The host renders the text visibly anyway.** `title` on `<Modal>`, `<PageHeader>`,
   `<EmptyState>`, `<WikiSection>`, `<ChoiceButton>` is a heading prop, not a hover claim —
   that class verdict covers most of the 205 untouched rows.

Exception worth naming: `title={maybeUndefined}` on a state branch ships no tooltip at all —
React omits an undefined `title` attribute, so the affordance is an absent bubble, not an empty
one. `feature-claim-chip.tsx:57` did that for a stale chip with no `claimedAt` (pre-fix); branch the
text or drop the attribute.

CSS truncation alone never makes a plain title a disclosure gap.
`features/grimoire/components/grimoire-status-tile.tsx:110-111` puts `title={status.binaryPath}` on
a `<dd>` whose visible text node is that same string — truncate clips pixels, the text node still
reaches assistive tech, so the tooltip only recovers the tail for mouse users.

## When conversion is required

Wire the claim when **both** halves hold: the text is not in the visible DOM, and it carries
state or discloses an outcome (who holds a claim, why something is blocked, what a routing
decision was, whether a folder is readable). T2 converted 10 rows on this test, T1 2 badge rows.

Converting keeps the visible `title=` for mouse users — the fix is an extra AT path, not a swap
(`feature-claim-chip.tsx:57` still carries the title after `5a7f81ca`).

`aria-label` on the same tag clears the harness mouse-only flag but does not clear the visual
gap: the native bubble still only renders on hover, so a keyboard-only sighted user sees nothing.
15 rows sit in that "announced, not visible" class; treat the flag as a floor, not a verdict
(T0 appendix, "Mouse-only, and what the flag does not say").

## Which mechanism

- **Short claim, host takes focus** → `sr-only` description node + `aria-describedby` on the
  host, anchor id scoped per instance.
  `features/planning/components/feature-claim-chip.tsx:26-45` (human arm, `claim-chip-human-${slug}-desc`)
  after `5efb1da7` fixed the static-id shape;
  `features/entries/components/entry-state-chip.tsx:19-23` at `d68bbbe0`. No new chrome, no
  layout change.
- **Host never takes focus** (disabled buttons are not focusable, so `aria-describedby` on them
  announces nothing) → put the disclosure in the control's accessible name.
  `features/projects/components/folder-picker-modal.tsx:243` at `8e826d8f` adds an `sr-only`
  span inside the button declared disabled at line 232.
- **Claim extends text the element already announces** → append it as `sr-only` text.
  `features/lanes/components/lane-feature-move.tsx:71` at `69a0e436` adds "— band {priority}" to
  the slot chip's announced text.
- **Rich multi-line content** (popup with rows, links, status) → the shared `Hint` primitive,
  `web/src/shared/ui/hint.tsx`, hover **and** focus reachable via `group-focus-within`.
  `widgets/app-shell/ui/status-chip.tsx:18-34` is the standing example; the kits badge conversion
  `features/kits/components/kit-class-badge.tsx:11-18` at `cb84edb9` copied that shape. Short
  claims do not get a bubble — T2 rule 4 reserves this shape for content that needs it.

Traps, all hit in this pass:

- `Hint` renders a real `<button>` (`hint.tsx:42`), so it cannot nest inside a button or link.
  The kits row badges became siblings of the row toggle — button at
  `features/kits/components/kit-list.tsx:39`, `<KitClassBadge>` / `<KitOriginBadge>` after it at
  54-55 (`cb84edb9`) — and the row's click target shrank to chevron/name/version/entries as a
  deliberate consequence.
- The bubble is `position: absolute` inside the wrapper (`hint.tsx:59`), so any `overflow-hidden`
  ancestor clips it. The kits list dropped `overflow-hidden` from the `<ul>` and moved
  first/last rounding onto the `<li>` (`kit-list.tsx:26,33`) — otherwise the last row's bubble is
  cut while the keyboard user still gets `aria-describedby`: a half-fix that looks done.
- Anchor ids must be per instance. One shared id across list rows collides, so the kits bubbles
  use `hint-kit-<kitId>-class` / `-source` (`kit-class-badge.tsx:14`). The claim chip shipped
  exactly this defect with static `claim-chip-*-desc` ids (`5a7f81ca`), caught by the wave B
  review — it now scopes all three arms by slug (`claim-chip-alive-<slug>-desc`, `5efb1da7`).

## Wording

- One sentence when a sentence does it; behavior first, not marketing.
  `features/settings/components/settings-form.tsx:172` at `e3d65654` — "Picks the embedder that
  turns entries into vectors — switching it stales the index, so reindex after saving."
- A claim must name what it was measured against. The stale kits row said a candidate is "a
  top-level folder containing SKILL.md folders or gate/guard files" and omitted two
  `KitScanner` arms (prose at the candidate's own root; the scanned folder itself) — fixed in
  `b13b2db8`. Enumerate the arms or say "or", never imply a shorter rule than the code.
- Do not state what a UI path does not do unless every arm holds. `grimoire-wizard.tsx:90`
  promised "Nothing runs" on the manual-install choice while
  `GrimoireIntegrationService.cs:69` takes no intent parameter (the wizard posts the same assisted
  mutation for both choices, `features/grimoire/api.ts:22`) — rewritten at `34534a2a` to state
  what holds in either arm state. The sibling claim "Runs Grimoire's own install channel when one
  is configured" (line 95) stays truthful because the conditional holds vacuously when the channel
  is empty. When copy papers over a behaviour worth changing, file the behaviour as its own
  feature (this became 267) instead of letting the hint carry the lie. Note how this row was
  missed: an earlier verdict cleared it by class — "the host renders hint text visibly" — which
  proves delivery, never truth. Visible is not the same as correct.
- A group note and its field lines must not duplicate. Three settings group notes were rewritten
  so the per-field line carries the behavior and the paragraph stops restating it (T3 log);
  `settings-form.tsx:262` is the field-level shape.
- Copy about a feature that is not on `main` must match `main`, not the branch. T1 checked
  251 with `git merge-base --is-ancestor feat/251-global-materialization origin/main` (not an
  ancestor) and kept the dark wording "Not deployable from this screen yet" instead of flipping
  it live on the strength of an in-progress board status.

Not owed a help line: action buttons whose label names the act (Test connection, Save settings,
Reindex all entries) and status displays (`GrimoireStatusTile`) — the T3 gap list excluded them
from its 25-field denominator, and the count arithmetic (9 helped + 16 added = 25) only closes
with them excluded.

## Error copy

Split by the user's action, never one string covering two mutations. `kitActivateErrorCopy` and
its `kitDeactivateErrorCopy` twin (`web/src/features/kits/lib/kit-activation-error.ts`, `4f496fa0`)
are chosen by which mutation failed (`widgets/kit-workspace/ui/kit-activation-section.tsx:35-36`):
a 404 on activate means the kit or project row is gone, a 404 on deactivate means no link exists —
and the client cannot tell them apart from the response, because every not-found arrives with the
default title "Not Found". Each arm builds its own sentence, the host's GUID-pair detail is never
forwarded to the screen, and where the host does put a code in the title (`MandatoryGlobal`, 409)
the copy keeps that claim verbatim.

## Where copy outside `.tsx` belongs

The harness walks `*.tsx` and reads `shared/lib/page-hints.ts` by name, nothing else. Copy in
other `.ts` modules is outside the 253 denominator — which is where this pass's only lying
surface outside the grimoire wizard lived (`kit-activation-error.ts`). Rule: user-facing strings
in `features/*/lib/**`, `widgets/*/lib/**` and `shared/lib/**` (message builders, query options,
`*-rows.ts`, `*-state.ts`) are hint surfaces too and get the same verdict; they live in the
owning feature's `lib/` module so one file names all arms (`kit-activation-error.ts` holds both
activation arms, exported through `features/kits/index.ts`), not inline in the component. When
re-running an audit, extend the scan to `.ts` before trusting any total.

## Re-running the inventory

```bash
python3 audits/tools-257-hint-inventory.py            # prints table + denominators
IN=audits/ui-hints-257-inventory-2026-11-21.md
diff <(awk '/APPENDIX: hand-written prose/{print; exit} {print}' "$IN") \
     <(python3 audits/tools-257-hint-inventory.py)     # empty = no drift since T0
```

Expect the denominators to move: 253 surfaces / 229 `.tsx` files / 38 mouse-only rows at
`aee7d413`, reconciling as 12 wired (10 T2 + 2 T1) + 25 justified as plain title + 1 out-of-lane
journal row. A new `title=` row appearing on an interactive host is the signal to re-open this
doc's conversion test, not to reach for `Hint`.
