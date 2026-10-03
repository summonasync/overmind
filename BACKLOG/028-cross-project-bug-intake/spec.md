---
feature: 028
slug: cross-project-bug-intake
status: validated
created: 2026-10-02
validated: 2026-10-02
---

# Spec 028 — Cross-project bug intake: чужой код → чужая борда (bug-сущность + onboarding правило)

## Goal

A protocol for the "foreign project" case: an agent working in project A (Mindlash Studio) that needs a fix in project B (Overmind) does not touch foreign code — it files an issue on B's board, and B's team converts it into work. The onboarding surface explicitly teaches this. Today there is no path: no entity fits (note/decision/gate/guard have no status machine or queue; task artifacts require a feature on the foreign board).

## Decisions (user-approved in chat, 2026-10-03)

- **Dedicated entity + dedicated tab.** New `Issue` entity (protocol word in docs stays "bug"; UI/tools say Issues) with its own lifecycle, and a dedicated **Issues Board** tab in the web UI (a list-with-detail, not a kanban).
- **Open intake with provenance, not permission theater.** The server is stateless — there is no honest way to distinguish "own" vs "foreign" agents, so no such checks are faked. Any agent may create and append; lifecycle moves are protocol-owned by the board's team (human door always full). Every issue and every append carries `authorProjectId` validated against existing projects — provenance, not security.
- **create + append.** Follow-ups to an existing issue from any project land as entries in an append-only **append trail**: each entry carries authorProjectId + text + createdAt, entries are never edited or deleted (the 039 append-log invariant). No duplicate issues for follow-ups.
- **Machine promotion, one call.** `issues_promote(issueId, featureSlug)` creates a task artifact per crew rule 10 ("a bug report is a ready-made execution contract"): goal from title+expected, scope from repro/actual, the append trail moves into `<obstacles>`, `<refs>` points at the issue. The issue becomes `promoted` with a link to the artifact — traceable both ways. Promotion belongs to the board's team.

## Data model

- `Issue`: Id, ProjectId (the board that owns it), Title, Body (markdown; the tool description recommends Repro/Expected/Actual sections — free-form accepted), Status, AuthorProjectId, CreatedAt, ClosedAt (nullable), PromotedToArtifactId (nullable).
- `IssueAppend`: Id, IssueId, AuthorProjectId, Text, CreatedAt. Append-only; listed chronologically in the UI detail.
- Status machine: `open → triaged → promoted → closed`. Forward chain; `closed` is reachable from any non-closed state (not-a-bag close, reason in text); no reopen in v1 (a re-found bug is a new issue); `promoted` only from `open`/`triaged` via issues_promote (it sets status + PromotedToArtifactId atomically). Status moves carry a `source` marker (agent/ui) per the 043 provenance pattern.

## MCP tool surface

- `issues_create(projectId, authorProjectId, title, body)` — compact echo (037 pattern: id, status, title); body-template recommendation lives in the description.
- `issues_list(projectId, status?, authorProjectId?)` — metadata rows (no append texts).
- `issues_get(issueId)` — full card + append trail.
- `issues_append(issueId, authorProjectId, text)` — echo with the new entry.
- `issues_set_status(issueId, status, source, logNote)` — lifecycle door.
- `issues_promote(issueId, featureSlug, source)` — creates the task artifact, sets promoted + link, echo names the artifact.
- `issues_create` on a foreign board posts a notification-center entry on the owner project with a deep-link to the issue (existing notification infra) — the owner team hears about it without polling.

## HTTP + Web (Issues Board tab)

- Both doors (agent + human) over the same service, per repo conventions; status actions in the UI: triage, close, promote (feature picker → promote).
- Left-menu tab **Issues Board** next to Planning: list (title, status chip, author-project, age, append count) with status/author filters; detail view with the append trail and actions. No kanban, no drag.
- Reads are open to any agent/project via issues_list/get by projectId.

## Onboarding protocol

- Quickstart gains the foreign-project protocol: «need foreign code → foreign board: list_projects → issues_create on their project; do not touch their repo; follow-ups = issues_append; the owner team triages/promotes».
- `issues_create` description carries the same protocol (tools are the docs agents actually read).

## Migration

- One EF migration `AddIssues` (Issues + IssueAppends). No data migration (new domain).

## Non-goals

- Reopen, assignees, labels/priorities on issues, kanban board, comments threading (append trail only), cross-project issue search, permission enforcement beyond provenance validation.
- Issue → feature (whole-feature) promotion: issues promote into tasks on an existing feature only.

<log>
- 2026-10-03 [agent] — spec drafted from Socratic gate: dedicated Issue entity + Issues Board tab, open intake with provenance, append-only trail, machine promotion per rule 10, status machine open→triaged→promoted→closed.
- 2026-10-02 [agent] — validated with human proof: notification 8e4a791f-8f6b-4b8f-bb4d-5238a73023ac replied 2026-10-02 21:30:52 UTC
- 2026-10-03 [agent] — 2026-10-03 [agent] — user-approved spec addition (AskUserQuestion, «Лочим», import-precedent): issues_promote is lock-aware — promoting into a feature claimed by a foreign live session is rejected 409 naming the holder (EnsureHolderAsync on the promote path); own-session and claim-free features promote normally. Rationale: promotion writes a task artifact + log + version bump into the feature — a mutation of the holder's contract, same bypass class the user closed for import; lock is session-scoped, so a same-team second agent gets 409 until the claim is theirs. Found by architect-review 2026-10-03 as W1.
</log>