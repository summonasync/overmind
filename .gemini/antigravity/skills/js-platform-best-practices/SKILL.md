---
name: js-platform-best-practices
description: |
  Codified Electron and React Native engineering rules (BP-IDs) consumed by js-platform-coder
  during implementation and by js-platform-code-reviewer as the review checklist. Single source
  of enforceable code-quality rules for the platform lane: Electron security and IPC hygiene,
  RN performance, RN navigation and platform code.
type: prompt
whenToUse: |
  During any Electron/React Native implementation (coder loads it with the task file)
  and any platform code review (reviewer cites violations by BP-ID).
---

# JS Platform Best Practices — Codified Rules

Each rule has a stable BP-ID. Reviewer cites violations as `BP-NN (severity)`.
Severity: **[B]** BLOCKER, **[W]** WARNING, **[S]** SUGGESTION.
IDs are scoped to this catalog; when citing across lanes, name the catalog first (`node-best-practices BP-10`).

## Electron Security & IPC
- **BP-01 [B]** `contextIsolation: true`, `nodeIntegration: false` on every window; `sandbox: true` unless a documented ADR says otherwise. `webSecurity: false` anywhere = BLOCKER.
- **BP-02 [B]** The renderer holds no Node reach: no `require`, no `process`, no Node globals. The preload bridge exposes named functions — passing `ipcRenderer` through `contextBridge` = BLOCKER.
- **BP-03 [B]** Every `ipcMain.handle` validates its payload with the contract zod schema first. A main handler trusting renderer input = BLOCKER.
- **BP-04 [B]** `setWindowOpenHandler` denies by default; external URLs via `shell.openExternal` against an https-only allowlist. `will-navigate` restricted to app origins; remote content in a privileged window = BLOCKER.
- **BP-05 [W]** Permissions granted per capability via `session.setPermissionRequestHandler` — no blanket allows.
- **BP-06 [W]** Production hardening: devtools gated, CSP without `unsafe-eval`, fuses hardened (`runAsNode` off, inspect flags off). Missing on a release config = WARNING.
- **BP-07 [B]** Secrets stay in the main process; a renderer-held credential without a documented threat model = BLOCKER.

## React Native Performance
- **BP-10 [B]** Long lists virtualized (`FlatList` / `SectionList` / FlashList). `.map` over unbounded data into Views = BLOCKER.
- **BP-11 [W]** Hermes on, new architecture not disabled. Reverting to legacy architecture flags = WARNING (BLOCKER without an ADR).
- **BP-12 [W]** Continuous gestures and scroll-linked animations run on the UI thread (Reanimated worklets / native driver) — JS-thread-driven = WARNING.
- **BP-13 [S]** `memo`/`useMemo`/inline requires only where profiling proves the win — reflexive memoization everywhere is gold-plating, not performance.
- **BP-14 [W]** Images carry explicit dimensions and a cache policy; full-res assets shipped where a thumbnail would do = WARNING.
- **BP-15 [W]** No per-frame data crossing the JS↔native bridge — batch it or make it event-driven.

## React Native Navigation & Platform Code
- **BP-20 [B]** Typed route param lists; navigation calls compile against them. Untyped string routes scattered across screens = WARNING (BLOCKER once params drift).
- **BP-21 [B]** Deep-link params parsed and validated at the entry point. An unvalidated deep link reaching business logic = BLOCKER.
- **BP-22 [B]** Auth gating decided at one navigator-level place — per-screen ad-hoc redirects = WARNING, a missed one = logged-out access.
- **BP-23 [W]** Platform divergence at the edges: `Platform.select` / `.ios.tsx` / `.android.tsx` splits; per-OS magic constants inside screens = WARNING.
- **BP-24 [B]** Native capability behind a typed wrapper (Expo Modules API / TurboModule codegen); raw `NativeModules.X` calls outside a wrapper = BLOCKER.
- **BP-25 [W]** Every OS capability has its full permission story: Info.plist usage description + AndroidManifest entry + runtime request flow. Missing plist string = BLOCKER (guaranteed store rejection).
- **BP-26 [S]** Safe areas and keyboard avoidance handled consistently per screen; `allowFontScaling` respected instead of broken with fixed heights.

## Usage
- **Coder:** load with the task file; rules are constraints, not aspirations.
- **Reviewer:** checklist = this file + task `<acceptance-criteria>` + spec-compliance. Cite as `BP-NN [B/W/S] — file:line — what's wrong — fix diff`.
- New recurring rule discovered during work → propose adding it here (reflect loop), don't patch it silently into one review.

---

## Output Contract (MANDATORY)
End with the fixed closing block, then STOP:
1. **Done** — summary + artifact paths (skill-prefixed per AGENTS.md provenance rule).
2. **Next step** — return control to the calling skill (`js-platform-coder` / `js-platform-code-reviewer`).
3. **Alternatives** — propose a new BP-ID to the user when a recurring rule is discovered; stop — reference skill, never runs standalone.
Suggest, never auto-proceed. The user picks the next step.
