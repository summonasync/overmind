---
feature: 120
slug: claimed-card-frame
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Spec 120 — Board card: claim frame (pin-frame successor)

<goal>
A claimed feature reads at a glance on the planning board card: the claim line `claimed by {Alias} from {Vendor}` renders in the header row immediately left of the status column, and an actively held claim draws the `border-primary` frame around the whole card — the successor of the pre-099 pinned-card frame.
</goal>

<scope>
Board list cards in FeatureBoard (web/src/features/planning/components/feature-board.tsx) only:
- Claim line in the header row (the row where the status column starts: blocked badge / phase label / status select), rendered immediately to the LEFT of that cluster. Text format exact: `claimed by {Alias} from {Vendor}`.
- Claim frame on the card root `li` for actively held claims, token-based: `border-primary` — the exact affordance the old `isCurrent` pin frame used before 099 removed it.
- Pure presentation helpers (`cardClaimLine`, claim-active predicate) in web/src/features/planning/lib/board-card-context.ts, pinned by web/tests/board-card-context.test.mts.
</scope>

<anti-scope>
- No claim/unclaim mutations from web, no new endpoints, no DTO changes — the five claim fields already ship on PlanningFeatureBoardItem (web types and Host contract PlanningContracts.cs) since 099.
- Dashboard overview panel and task-queue surfaces keep their current claim presentation — the body scopes this to the board card only.
- The brief line stays on its own row; its move to the slug row is 117's lane, not this feature. The claim line does not remove or replace the brief line.
- FeatureClaimChip stays as-is — the liveness words («stale», «ended», «human-held») live there; the claim line carries no state word.
- No backend changes: Host / Infrastructure / Core untouched.
</anti-scope>

<constraints>
- Liveness decision (the body says «when the feature is claimed», unconditionally; 099/04 added states later): the claim LINE renders for every claim — any claimState, agent or human — because stale/ended claims still name their holder. The FRAME and the success tone render only for actively held claims: `claimedByHuman === true || claimState === "alive"`. A claim with null liveness counts as not active (conservative).
- Claim line text is invariant: `claimed by {Alias} from {Vendor}` — no state words, no age suffix (age stays on the chip).
- Token-based classes only (border-primary, text-success, text-muted) — no arbitrary colors. Unclaimed cards must be pixel-identical to today: the line renders null, no placeholder element, no layout shift.
- Drag-and-drop affordances stay distinguishable: drop-target keeps `ring-2 ring-primary/40`; the claim frame is a border, not a ring — no collision.
- Client-only change inside the existing "use client" FeatureBoard; no RSC boundary moves, no new dependencies.
</constraints>

<acceptance-criteria>
- [ ] Board card of a feature with an alive agent claim renders the exact line `claimed by {Alias} from {Vendor}` in the header row, immediately left of the status column cluster.
- [ ] That card's root element carries the `border-primary` frame (visually the old pinned-card frame).
- [ ] Stale, ended, and null-liveness claims render the claim line in muted tone with NO frame; the chip keeps carrying the state word.
- [ ] Human-held claims get the frame and success tone; the line format is identical.
- [ ] Unclaimed cards render no claim line and keep today's exact classes — no layout shift.
- [ ] Helpers live in board-card-context.ts; web/tests/board-card-context.test.mts pins the exact format and the active-claim matrix (alive / stale / ended / null / human).
- [ ] `npm run lint && npx tsc --noEmit && npm test && npm run build` all green in web/.
</acceptance-criteria>

<refs>
- Feature body 120-claimed-card-frame — user design decision 2026-10-04 (claim line slot left of status + card border).
- 099-agent-claims (done) — claim fields on all read payloads; chip + liveness semantics this feature presents at card level.
- 117-phase-label-as-tag-chip (draft) — same header row region of feature-board.tsx; no semantic dependency, whichever lands second rebases a one-line region.
- web/src/features/planning/components/feature-board.tsx — card root `li` className (~line 210) and header row (~line 217) as of drafting.
- web/src/features/planning/lib/board-card-context.ts + web/tests/board-card-context.test.mts — existing card-line helpers and the node:test pin style.
- src/Overmind.Host/Api/Contracts/PlanningContracts.cs — PlanningFeatureBoardItem already exposes ClaimedByAlias / ClaimedByVendor / ClaimedAt / ClaimState / ClaimedByHuman (read-only proof that nothing new is needed).
</refs>

<obstacles>
None at draft time. One interpretation note for the human: the user's quote places the claim text «in the brief line's slot», while in code the brief renders on its own row below the chips. This spec resolves it as: header row, immediately left of the status column — the literal «слева от статуса» — with the brief line untouched until 117 moves it. If the human meant «the claim line replaces the brief row while claimed», that is a one-line amendment BEFORE validation, not after.
</obstacles>

<log>
- 2026-10-06 [agent] — Spec drafted from the feature body's 2026-10-04 design decision; liveness handling decided (line = any claim, frame = actively held: alive or human); API verified to need nothing new (fields exist since 099).
- 2026-10-06 [agent] — validated with human proof: notification d4acfe49-3448-4a8f-855e-ecf55ab6b1d9 replied 2026-10-06 13:05:10 UTC
</log>