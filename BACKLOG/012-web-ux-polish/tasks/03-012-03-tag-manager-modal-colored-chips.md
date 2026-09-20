---
feature: 012
task: 03
slug: web-ux-polish
status: done
review: light
created: 2026-09-20
---

# 012/03 — Tag manager modal + colored chips

<goal>
The tag editing UI: a Tags button in the Entries toolbar opens a modal listing all tags, each with a color dot and a 12-color preset palette plus clear; picking a color PUTs it and the entries list chips recolor live. Chips render tinted when a color exists, neutral otherwise.
</goal>

<context>
Feature 012, web half #1. Depends on task 01 (TagsApi). Single render site for chips confirmed (entry-list.tsx:43–48 via shared Badge).
</context>

<scope>
- web/src/features/entries — types + api: TagResponse {id, name, color}; updateTagColor(id, color|null) hitting PUT /api/tags/{id}; tags query key added to the entries domain factory (queries.ts pattern).
- tag-manager component (features/entries/components/): toolbar button "Tags" (place next to the existing filters/actions in the entries header) opening the shared Modal ("md"); list tags ordered by name (GET /api/tags via useQuery); each row: name, current color dot, a row of 12 preset swatches + a clear (×/none) swatch; clicking a swatch PUTs immediately (useMutation) with optimistic dot update + error toast/rollback per existing patterns; on success invalidate the tags key AND the entries list key (chips recolor).
- Preset palette: 12 colors as a local constant (derive from sensible accessible hues; document the hexes in the file once). Clear sends "" (backend clear semantic); "no change" is simply not clicking.
- entry-list.tsx: tag chips gain optional tinted style when the tag has a color — style={{ backgroundColor: hex + "1A", color: hex, borderColor: hex + "40" }} (or the Badge tone-equivalent), neutral Badge unchanged otherwise. The chips' color lookup comes from the tags query cached data (useQuery in the list, or pass down from the page — follow the slice's existing data flow; avoid N+1 fetches).
- Empty tag list → friendly empty state in the modal.
- Verify: npm run lint && npx tsc --noEmit && npm run build.
</scope>

<anti-scope>
- No tag rename/delete UI, no per-entry color editing, no changes to the entry editor's comma input.
- No search-page/filter/info-panel changes.
- No backend changes (fix-loops via status flow if reviews find gaps).
</anti-scope>

<constraints>
- React Query v5 conventions; no useEffect data fetching; mutations invalidate via queryClient; RSC/client rules per slice (these are client components already).
- TypeScript strict, no any; English-only UI strings; a11y: swatch buttons need aria-labels (color name/hex).
</constraints>

<acceptance-criteria>
- [ ] Tags button opens the manager; all tags listed with current colors.
- [ ] Picking a swatch PUTs and the dot updates; entries chips recolor without reload; clear returns neutral.
- [ ] PUT failure → visible error, dot rolls back.
- [ ] Tags without color render exactly as before (neutral Badge).
- [ ] lint + tsc + build green.
</acceptance-criteria>

<refs>
- Spec: feature 012, spec artifact — tag manager decisions.
- Pattern refs: entry-list.tsx:43–48, entry-filters.tsx (toolbar placement), shared/ui/modal.tsx, features/entries queries factory, existing useMutation+toast patterns in the slice.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Execution started — next-js-coder subagent (backend API from 01 in tree; planning/feature-board.tsx being edited by the 022 coder, disjoint).
- 2026-09-20 — Review PASS (audits/review-012-03.md): 0 blockers, 3 no-action observations (theoretical optimistic interleave self-healing via refetch; tags fetch on page mount is load-bearing for the shared cache; as-Error casts pattern-consistent). Shared single tags query verified between manager and list; hex+alpha safety proven against the \z validator. Done.
</log>
