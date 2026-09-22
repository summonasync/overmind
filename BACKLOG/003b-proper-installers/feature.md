---
feature: 003b
slug: proper-installers
status: ready
priority: p2
version: 1
created: 2026-09-21
---

# Feature 003b — Proper OS installers

<goal>
Overmind installs on Windows the way a desktop app is expected to: one setup executable, an entry in Installed Apps, Start Menu shortcut, autostart, and an uninstaller that can also remove user data on explicit consent. 003a made the app runnable from a folder; 003b makes it installable — and, as a side effect of finally having a Windows machine, proves the win-x64 build actually runs instead of merely compiling.
</goal>

<requirements>
- A single `overmind-<version>-setup.exe` installs the Host, the MCP adapter and the web export in one pass, with a SHA-256 sidecar.
- Install location is the user's choice: per-user without elevation by default, per-machine with elevation as an option.
- The installer registers login autostart, and offers to put the MCP adapter on PATH so agents can spawn `overmind-mcp` without an absolute path.
- Upgrade in place: installing a newer version over an older one keeps user data and requires no manual uninstall.
- Uninstall removes everything the installer wrote, and removes `~/.overmind` only on explicit user consent (closes the 003a tombstone).
- A running Host is shut down gracefully before its files are replaced or removed — no process kill on the primary path.
- The installer is verified by an automated smoke run on a real Windows machine, not by build success alone.
</requirements>

<non-goals>
- macOS `.pkg` — still blocked, no macOS build host exists (003b is Windows-only; see spec D8).
- Code signing and store distribution — inherited non-goal from 003a, unsigned setup triggers SmartScreen by design.
- App auto-update — inherited non-goal from 003a: a new version is a new setup run.
- Linux packages (deb/rpm/flatpak) — inherited non-goal.
- Replacing the portable archive: 003a stays the primary channel, the installer is a second one.
</non-goals>

<refs>
- Rides `BACKLOG/003-bundled-installer/` (003a): archive layout, bundled-kit seeding, `build/pack.sh`.
- Unblocks the `[?]` entry in `BACKLOG.md` — "blocked on a Windows/macOS build host" — for the Windows half only.
- 003a hardening note (`audits/review-003-04.md`, NIT): `install.ps1` shortcut lacks WindowStyle, console flash at login — the installer path must not inherit that.
</refs>

<!--
LIFECYCLE: создаётся при постановке фичи в бэклог ДО старта работы.
Не замораживается гейтом, но изменения после status: ready проходят через пользователя (bump version).
Молчаливые правки запрещены.
-->
