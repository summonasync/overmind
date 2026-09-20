---
feature: 012
slug: web-ux-polish
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Spec 012 — Web UX polish: tag colors + feature editor window

<decisions>
- Tag colors live on the Tag entity as a nullable hex string (max 16 chars, format #RRGGBB — same shape as the FrontmatterKey.Color precedent). Tags are global today (no ProjectId on Tag) — the color inherits that scope; per-project colors are out.
- New additive tag surface, colors only — no rename/delete in this feature: GET /api/tags → [{id, name, color}]; PUT /api/tags/{id} with {color} — absent/null = untouched, "" = clear, "#RRGGBB" = set (format-validated). MCP tools are NOT extended (colors are a human/UI concern; write_entry tags stay name-based).
- The "tag editing UI" from the feature body is a Tag manager modal opened from the Entries toolbar: list of all tags, each with a color dot and a preset palette (12 accessible colors derived from existing design tokens) to pick/clear. The backend stores any valid hex; the palette is a client concern.
- The entry editor keeps its comma-separated tags input — no per-editor color UI (single point of color management).
- Chip rendering: the single render site (entry-list Badge) gains an optional tinted style — background = hex + alpha suffix, text = the hex itself; tags without color fall back to the current neutral tone. Search/filters/info panels unchanged.
- Edit-feature window: FeatureForm renders in the existing shared Modal "xl" variant (both call sites: feature-workspace and planning-page); body textarea grows to rows 20; a Write/Preview toggle is added — and because this is the fourth copy, the existing toggle is extracted into shared/ui and consumed by all four (the 006/09 NIT collected; decisions delegated by the user 2026-09-20).
</decisions>

<open-questions>
- none (user delegated autonomous decisions 2026-09-20)
</open-questions>

<requirements>
Backend:
- Tag entity: nullable `Color` (string?); EF migration AddTagColor (nullable, max length 16).
- New static API class TagsApi: GET /api/tags (all tags — id, name, color; small table, no paging) + PUT /api/tags/{id} {color: string?} with #RRGGBB format validation (FluentValidation), absent/null = untouched, "" = clear; unknown id → 404. Additive routes only.
- Response DTOs in Contracts; mapper follows existing conventions; tests in Api.Tests (get/list shape, set/clear/untouched, format validation 400, 404).
Web:
- shared/ui/write-preview-toggle.tsx extracted from the three existing copies (entry-editor, artifact-editor, task-editor); all three consume it — behavior identical, no visual regressions.
- features/entries: tag-manager — toolbar button ("Tags") opening a Modal listing tags (React Query, new tags query key riding the entries domain factory), color dot + preset palette (12 tokens), clear option; PUT on pick; invalidates the entries list (chips recolor).
- entry-list: Badge gains optional color styling (tinted bg + colored text, inline style from hex; neutral fallback).
- planning: FeatureForm (feature-form.tsx) — modal call sites switch from "md" to "xl" (feature-workspace.tsx, planning-page.tsx); textarea rows 20; Write/Preview toggle (shared) over the body; submit shape unchanged ({title, body}).
- types/api: TagResponse + updateTagColor; queries via existing factory patterns.
Tests/verification:
- dotnet build/test green with new Api tests; web lint + tsc + build green.
</requirements>

<acceptance-ideas>
- Assign a color to a tag in the Tag manager → its chips in the Entries list render tinted without a reload; clearing the color returns the neutral chip.
- Tag with no color → chip looks exactly as today (no regression).
- PUT /api/tags/{id} with "blue" or "#GGG" → 400; with "" → color cleared; absent → untouched.
- Edit feature opens full-size (xl): title + slug row, wide textarea (20 rows) with working Write/Preview toggle; the same toggle now lives in shared/ui and the three legacy editors still render identically.
- GET /api/tags returns every tag with its color state.
</acceptance-ideas>
