---
feature: 083
slug: feature-body-update
status: draft
created: 2026-09-27
---

# Result 083 — planning_update_feature: agent-door правка title/body/priority с logNote + log-гвард фича-боди (глотает microbatch #74)

## Outcome
Feature bodies are now correctable by agents: `planning_update_feature(projectId, slug, title?, body?, priority?, logNote)` — partial edit with mandatory logNote, `[agent]` provenance line, 070-family compact echo. The feature `<log>` is append-only on EVERY door (human PUT, agent POST, import) via the 078 guard ported into `PlanningFeature.Update` — microbatch #74 absorbed at the entity seam, no door-local copies.

Shipped in four tasks:
- **01 — Core guard** (PlanningFeature): `RequireLogHistoryAppendable` byte-port called before any mutation (`body ?? Body` covers title-only), optional sourced logNote append; Version semantics pinned as found (bump only outside Draft). Review **PASS-WITH-NITS** (audits/review-083-01.md). Field find: the guard exposed that the IMPORT door was silently dropping board log lines on stale disk bodies — same #74 seam, now loudly rejected; characterization fixture fixed test-only.
- **02 — Service + agent door**: `UpdateAsync` overload (logNote mandatory via `FeatureLogNoteRequired`, no status parameter, priority → `SetPriority` + queue-tail per 072, transaction + materializer sync), `POST /api/agent/.../features/{slug}/update`, FluentValidation (at-least-one editable field, priority whitelist), echo `{slug,status,version,updatedAt,bodyChars}`. Review **PASS-WITH-NITS** (audits/review-083-02.md): obstacle 400-vs-409 for `LogHistoryNotAppendable` accepted — spec wording contradicted the shipped 078 mapping (InvalidInput → 400); test pins the fact.
- **03 — MCP tool**: `planning_update_feature` (mutation flags, verbatim description pinned byte-for-byte task↔source↔test, logNote schema-required, blank optionals omitted, passthrough echo), pins 24 planning / 34 total tools. Review **PASS-WITH-NITS** (audits/review-083-03.md); both quirks (C# required-after-optional param order; SDK 2.2.0 emits no wire annotations) verified and accepted.
- **04 — Docs anchors**: CONTEXT «twenty-four tools» + update_feature rung; STRUCTURE feature-log append-only clause in the service-seam bullet (orchestrator-reviewed, docs-only, 2 lines).

## Gates
- `dotnet build Overmind.sln` — 0W/0E, verified independently in all three reviews.
- `dotnet test Overmind.sln` — **1561/1561 green** (1533 → +10 Core/01 → +11 Api/02 → +7 Mcp/03; Core 225 / Infra 496 / Api 462 / Mcp 378).
- Diff confinement: Core + Core tests (01), Infrastructure/Host + Api tests (02), Mcp + Mcp tests (03), CONTEXT/STRUCTURE (04) + BacklogImporterTests characterization (declared, test-only).
- Live MCP probe deferred to post-deploy (house pattern: tool reaches agents on adapter restart). Post-deploy smoke: fix a typo in a fresh feature body with a logNote → `[agent]` line lands; hand-rewritten history → `LogHistoryNotAppendable`; `planning_set_feature_status` still the only status path.

## Deviations
- AC5 of task 01 (diff confined to Core) honestly unticked: the red suite forced a one-file Infrastructure TEST fix (importer fixture mirrors the board's `[auto]` log lines). Reviewer verified the root cause as the #74 seam on the import door and accepted.
- Spec said «409-family» for log-history rewrites; shipped 078 mapping is InvalidInput → 400 with the typed title. Kept, pinned, documented as a cross-door follow-up if product wants 409.

## Follow-ups
- Microbatch: numeric priority strings ("0"–"3") pass as P0–P3 cross-door (tighten to p-prefix everywhere or accept); `Enum.Parse`→`TryParse` in handler; whitespace-logNote test pins (MCP + API); PlanningFeatureService split watch (553 lines / 7 public methods); MCP param-description wire pins; mutation echo-shape unification (set_feature_status MutationEcho vs update_feature passthrough); guard-helper extraction IF a third `RequireLogHistoryAppendable` copy ever appears.
- User gates: result validation, feature done, commit wave (080+083 release pair), bundle deploy.

<log>
- 2026-09-27 [agent] — result drafted after 4/4 tasks done (reviews: PASS-WITH-NITS ×3 + orchestrator docs check); 1561/1561 green; live probe deferred to post-deploy.
</log>