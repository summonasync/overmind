---
name: cargo-licensing-guard
description: |
  Licensing rules and approved alternatives for third-party Rust crates.
  Use before adding, upgrading, or recommending any crates.io dependency —
  especially UI frameworks (Slint, GTK, Qt bindings), anything GPL/AGPL/LGPL/MPL,
  or any commercial/dual-licensed crate.
type: prompt
whenToUse: |
  When the task involves adding a new crate to Cargo.toml, upgrading an existing one,
  choosing between crate alternatives, or reviewing Cargo.toml / Cargo.lock / deny.toml changes.
---

# Cargo Licensing Guard

## Universal Rule
Before adding any third-party crate, verify its license is OSI-approved
(MIT / Apache-2.0 / BSD). Flag dual-licensed, copyleft, or commercial crates explicitly
to the user BEFORE adding — never silently.

Rust ecosystem norm: most crates are dual-licensed `MIT OR Apache-2.0` — that is the safe
default. The guard exists for everything that deviates.

## Known Traps

### Desktop UI frameworks
- **Slint**: GPL-3.0 OR royalty-free/commercial license. Using it in a proprietary,
  non-GPL app = license decision the USER makes, never the agent. Free desktop-only
  royalty-free license exists but has conditions — read them before recommending.
  Alternatives: egui (MIT/Apache-2.0), Iced (MIT), Tauri (MIT/Apache-2.0).
- **Qt bindings** (cxx-qt and similar): GPL/commercial — same STOP-and-ask rule.
- **GTK / gtk4-rs / relm4**: GTK libs are LGPL-2.1+. Dynamic linking of a proprietary
  desktop app is generally OK; static linking or mobile embedding needs review.
  Flag before adoption — Linux-first ergonomics, painful Windows/macOS packaging.

### Copyleft
- **GPL/AGPL crates in a proprietary product = STOP.** Report to the user, wait for a decision.
  AGPL is the hard one — network/copyleft terms make it poison for closed products.
- **MPL-2.0**: file-level copyleft — generally acceptable, but flag when vendoring or
  modifying the crate's source.
- **LGPL**: acceptable for dynamically linked desktop apps; flag static-link/embedding cases.

### Watch the license field, not the fame
- Popular crates occasionally carry unusual terms (CC0 variants, custom notices, Unicode
  license in ICU-derived code). Any license outside MIT/Apache-2.0/BSD → check before adding.
- `cargo deny check licenses` + `deny.toml` is the project-side enforcement gate — every
  project scaffold should carry a `deny.toml` with an explicit allow-list.
- `cargo audit` covers advisories (security), not licensing — run both.

## Procedure
1. On any `Cargo.toml` / `deny.toml` change — check every added/bumped crate against this file.
2. For crates NOT listed here — check the license field on crates.io (or `cargo metadata`) before approving.
3. If a commercial/copyleft license is detected — STOP, report to user with an alternatives table, wait for decision.
4. When a flagged-but-approved crate is adopted — record the decision and ceiling (version,
   conditions) in the project's `deny.toml` or AGENTS.md so future upgrades don't cross the boundary.

---

## Output Contract (MANDATORY)
End with the fixed closing block, then STOP:
1. **Done** — summary + artifact paths (skill-prefixed per AGENTS.md provenance rule).
2. **Next step** — return the verdict to the calling coder skill (approve / pin / replace).
3. **Alternatives** — propose an approved alternative from the rules table; escalate to the user when no compliant option exists.
Suggest, never auto-proceed. The user picks the next step.
