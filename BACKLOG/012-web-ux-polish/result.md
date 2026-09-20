---
feature: 012
slug: web-ux-polish
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Result 012 — Web UX polish: tag colors + feature editor window

<outcome>
012-web-ux-polish shipped, both halves:

Tag colors — Tag.Color (nullable #RRGGBB) + two additive endpoints (GET /api/tags ordered by name; PUT /api/tags/{id} with absent=untouched / ""=clear / set semantics, format-validated, 404) + EF migration AddTagColor. Web: a Tags manager modal from the Entries toolbar (12-color preset palette + clear, optimistic swatch PUTs with rollback, shared single tags query deduping manager and chip list), tinted tag chips in the entries list (hex+alpha bg, hex text, neutral fallback). The regex anchor was hardened in-tree after review ($ matched before a trailing \n — now \z).

Edit-feature window — both FeatureForm call sites render in the shared Modal xl variant; the body textarea grows to 20 rows with the editors' flex-fill pattern; the Write/Preview toggle finally lives in shared/ui (the 006/09 NIT collected) and is consumed by all four editors (entry, artifact, task, feature form) — the three legacy copies were byte-identical and were unified (+6/−77).

Suite 794 → 802 (+8). Reviews: 01 PASS-WITH-NITS (NIT fixed in-tree), 02 PASS, 03 PASS, 04 PASS.
</outcome>

<evidence>
- dotnet build 0w/0e; dotnet test 802/802 (Core 106, Infra 274, Mcp 203, Api 219) — includes TagsApi lifecycle/format/404 tests with non-persistence pinned on every invalid case.
- Regex hardening pinned: "#3B82F6\n" rejected (test added in-tree after review).
- Web: lint 0 errors / tsc 0 / build green across all four tasks (10/10 routes); reviewers re-ran tsc+lint independently.
- Reviews: audits/review-012-01.md (PASS-WITH-NITS), review-012-02.md (PASS, token-level markup equivalence proven), review-012-03.md (PASS, single-query dedup + hex-safety verified), review-012-04.md (PASS, preview-path parity 1:1).
- Live UI visual pass deferred to the user on the next packed release (0.1.8) — same posture as 021.
</evidence>

<deferred>
- Microbatch queue: aria-pressed on the shared toggle buttons (now a one-line fix); tags fetch on page mount is load-bearing (accepted); redundant MaximumLength(16) alongside the regex; uppercase-hex test symmetry; dangling htmlFor in the feature-form preview mode (inert); toast system for web/ as a shared-layer story; shared Select tone variant when a second tone consumer appears.
- Tag rename/delete and per-project tag scoping — deliberately out (colors only, per spec).
</deferred>
