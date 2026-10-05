---
feature: 051
task: 02
slug: entries-materialization
status: done
review: light
created: 2026-10-04
depends_on: 01
brief: Clobber-guard over ENTRIES/ + report/status lines + tests
---

# Task 02 — Clobber-guard + observability

<goal>
033 semantics extend to ENTRIES/: hand edits refuse, force warns, failures surface.
</goal>

<scope>
<scope>
- Clobber-guard over ENTRIES/ tracked files (hand-edited → refusal; force → per-file warnings) — pins on the real code paths.
- Frontmatter sanitization (review-051-01 W1): title/tags must not carry \n/\r into the file — validator-level or EntryDocument sanitize, pinned.
- Archive-strategy entry files pinned (N1); rename-in-place pinned (N2 — mechanism identical to the pinned tombstone pair).
- Skipped/refused lines ride the existing materialize report/status surfaces (074).
- Tests: guard refusal, force pass with warnings, report lines, sanitization, archive, rename.
</scope>
</scope>

<acceptance-criteria>
- [x] Guard refusal + force warnings pinned.
- [x] Report/status lines for ENTRIES/ present — pinned.
</acceptance-criteria>

<refs>
- Spec a87863c1; plan 46b7e368 (task 2); 033 guard; 074 reporting.
</refs>

<log>
- 2026-10-05 [agent] — [agent] Taken by orchestrator (task 01 done). Scope expanded with review-051-01 carry-overs: W1 frontmatter sanitization + N1 archive pin + N2 rename pin join the guard/report work. Dispatching dotnet-coder (sonnet).
- 2026-10-05 [agent] — Coding round complete (dotnet-coder, sonnet; gates re-verified by orchestrator — build 0w, Infra 650 (+6), Api 633 (+2), Mcp 459, format clean; checkpoint 890ef54): guard 033 over ENTRIES/ pinned on real paths (hand-edit refusal + force per-file warnings + foreign-file adopt), skipped/refused lines ride 074 surfaces, W1 sanitized at PLAN level (EntryDocument flattens newlines — projection whole from any stored data, validators refuse comma tag names at the established 400 path instead of Tag.Create which would 500), archive + rename-in-place pinned. Most guard pins green-on-arrival — task 01's structural bet confirmed. Declared: legacy comma-tag would render glued (cosmetic, one-off data fix if ever wanted); BACKLOG.md in refuse runs lands in skipped (not refused) to keep refused purely ENTRIES. Dispatching light review.
- 2026-10-05 [agent] — [agent] Light review PASS-WITH-NITS (audits/review-051-02.md; gates re-run: build 0w, Infra 650, Api 633→634, Mcp 459, format clean). W1 fixed in-tree (05f6cc2): null tag element hit the ForEach Must predicate (FV cascade-continue) → NRE 500; comma predicate now null-guards, pin Tags [null] → 400 added. Coder's seams upheld by reviewer: plan-level sanitization is STRONGER than claimed (KitImporter writes entries directly with kit titles — EntryDocument heals from any door), tag door claim verified (TagsApi is color-only), BACKLOG.md→skipped classification honest (only when disk==row==plan hashes). N1 (ownership-row pin) → microbatch; N2 (colon/quotes in title — no ENTRIES parser exists, reverse import would need quoting) → task 03 docs. Both AC ticked. Closing task.
</log>
