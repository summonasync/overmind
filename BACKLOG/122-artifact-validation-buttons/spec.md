---
feature: 122
slug: artifact-validation-buttons
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Spec 122 — Approve/Decline buttons on artifact views (human validation door)

<goal>
The user validates artifacts where they READ them: Approve / Decline live on the spec/plan/result view in the workspace, not buried in the edit modal, and not only via the notify→tap round-trip.
</goal>

<scope>
- R1. Discovery (changes the shape): an Approve button ALREADY exists in ArtifactEditor (visible when the artifact is not validated, next to Save; calls the human validate door — no proof needed, the human click IS the proof; works today with humanGate on or off). The user never saw it because the artifact VIEW (workspace tabs) has no actions. This feature MOVES the door to the surface, not invents it.
- R2. View-level actions: on the spec/plan/result view in feature-workspace (draft state, not frozen), an Approve button calls the existing human validate door — same mutation the editor button uses. No proof round-trip, no notify dependency. After a later edit (demote to draft) the button returns — freshness by state, same rule as notify proofs.
- R3. Decline: new action with an optional reason → backend appends a `[ui] declined by human: <reason>` line to the artifact log (new small human-door route; artifact stays/returns to draft — decline never changes status, it records the veto). The claiming agent sees it in the artifact log on the next read. **Challengeable:** v1 keeps decline as a log line only; auto-notifying the claiming agent (overmind_notify to the holder) is a v2 candidate.
- R4. Frozen (validated) artifacts: no Approve (already validated), no Decline (nothing to veto — a change goes through edit → demote → new round, existing flow).
- R5. Result artifacts: Approve fits (result validation closes waves today via notify); Decline on result = same log-line semantics.
</scope>

<anti-scope>
- No change to agent-side proof mechanics (notify→reply→validate stays for agents).
- No auto-notify of agents on decline (v2 candidate); no status machinery changes; no email/push.
- Task artifacts keep their own status flow (decline does not apply).
</anti-scope>

<constraints>
- The human validate call is the existing door — zero new validation semantics on Approve.
- Decline route: human door only, optional reason (trim, max ~500 chars), always appends with [ui] source marker (043 discipline).
- FSD: actions live in features/planning; no cross-feature imports.
</constraints>

<acceptance-criteria>
- [ ] Draft spec/plan/result views show Approve; clicking validates via the human door (no notify round-trip).
- [ ] Decline with optional reason appends a [ui] declined log line; artifact stays draft.
- [ ] Validated artifacts show neither button; post-edit demote brings Approve back.
- [ ] Web gates green; decline route tested; result drafted.
</acceptance-criteria>

<refs>
- User intake 2026-10-04 («нет кнопок Approve/Decline на spec и plan»); artifact-editor.tsx:123-130 (existing editor Approve); feature-workspace.tsx:283-312 (validate mutation wiring); AgentPlanningApi validate door; 043 provenance; 109 (done-gate — sibling proof thinking).
</refs>

<log>
- 2026-10-04 [agent] — spec drafted; discovery: editor Approve already exists, feature is surface-level relocation + new Decline.
- 2026-10-04 [agent] — validated with human proof: notification a7103fb2-9391-401f-822f-58204271149c replied 2026-10-04 19:12:42 UTC
</log>