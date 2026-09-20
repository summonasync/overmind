---
feature: 012
slug: web-ux-polish
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Plan 012 — Web UX polish: tag colors + feature editor window

<approach>
Two independent halves. Backend colors first (Tag.Color + TagsApi, additive) — it unlocks the tag manager; in parallel, the Write/Preview toggle finally moves to shared/ui (pure refactor, zero behavior change) because both the feature editor and good hygiene need it. Then the web rides both: the tag manager modal + colored chips (consumes TagsApi), and the feature editor window (consumes the shared toggle + existing xl modal variant). Closeout is light: no disk round-trip, no MCP surface, no migrations beyond one nullable column.
</approach>

<contract>
- Tag.Color: string? (hex #RRGGBB, max 16); EF migration AddTagColor (nullable).
- GET /api/tags → TagResponse[] {id, name, color} — all tags, no paging (small table).
- PUT /api/tags/{id} {color: string?} — absent/null = untouched, "" = clear, #RRGGBB = set; format validation 400; unknown id 404. No rename/delete. MCP untouched.
- Web wire: TagResponse type + updateTagColor api fn; new tags query key in the entries domain factory.
- shared/ui WritePreviewToggle: one component, props {mode, onChange} (or stateless render-prop as the three copies dictate — extractor decides, consumers stay identical); consumed by entry-editor, artifact-editor, task-editor, feature-form.
- Feature editor: both modal call sites (feature-workspace, planning-page) render FeatureForm in Modal xl; textarea rows 20; Write/Preview toggle over the body; submit shape unchanged.
- Tag manager: Entries toolbar button → Modal listing tags; color dot + 12-color preset palette + clear; PUT on pick; invalidates entries list.
- entry-list chips: inline tinted style when color present (hex bg + alpha, hex text), neutral Badge fallback otherwise.
</contract>

<tasks>
- [ ] 01-tag-colors-backend — Tag.Color + migration + TagsApi (GET/PUT) + FluentValidation + Api.Tests → tasks/01-tag-colors-backend.md
- [ ] 02-shared-write-preview-toggle — extract the toggle from the three editors into shared/ui, all three consume it, zero visual change → tasks/02-shared-write-preview-toggle.md
- [ ] 03-tag-manager-colored-chips — tag manager modal (toolbar button, palette, clear), colored chips in entry-list, React Query wiring → tasks/03-tag-manager-colored-chips.md
- [ ] 04-feature-editor-xl-window — FeatureForm in xl modal (both call sites), textarea rows 20, shared toggle, verification + closeout → tasks/04-feature-editor-xl-window.md
</tasks>

<risks>
- The three Write/Preview copies may have drifted subtly (classes, aria) — the extractor must diff them and preserve each editor's exact markup; any intentional divergence gets noted, not silently unified.
- Chips recolor depends on the entries invalidation firing after a tag PUT — the tag manager must invalidate the entries list key, not just its own.
- Tags are global across projects — the manager lists ALL tags; acceptable today (single-user app), noted for a future per-project scope.
- PUT semantics absent/null=untouched mirrors brief — keep the validator and the service guard consistent (format check only when non-null and non-empty).
</risks>

<verification>
- dotnet build + test green with new TagsApi tests (list shape, set/clear/untouched, 400 format, 404).
- web: lint 0 new errors, tsc 0, build green; the three legacy editors render identically (manual DOM sanity via build + code review).
- Live: assign a color in the Tag manager → chips tint in the entries list without reload; clear → neutral again; feature editor opens full-size with working toggle.
</verification>
