# Overmind.Host.Kestrel.Tests

Boots the full production `Program` pipeline on a real Kestrel server bound to an ephemeral
loopback port and asserts contracts on the raw HTTP wire: real URL parsing, routing table,
SPA fallback, middleware order. Every test class carries `[Trait("Host", "Kestrel")]`.

## Harness roles

- `Overmind.Api.Tests` — `ApiFactory`, in-memory TestServer: fast handler/service tests.
- `Overmind.Host.Kestrel.Tests` — `KestrelApiFactory`, real Kestrel with real-socket
  `SocketsHttpHandler` clients: production-shape contracts that TestServer cannot decide.

## Factory isolation recipe

- unique temp SQLite database, migrated and then seeded with `telegram.enabled=false` and
  `planning.materializationDefault=off` before boot. Both settings live in the settings
  table (no `IConfiguration` fallback in `AppSettingsService`), so the rows are written
  directly; config keys of that name would be read by nothing;
- own temp `Overmind:PortFilePath`;
- `Overmind:BindUrl` left empty so the app never overrides the factory's ephemeral URL
  (`http://127.0.0.1:0`, actual port read back from `IServerAddressesFeature`);
- temp `wwwroot` with a minimal `index.html` exposed via `OVERMIND_WEB_ROOT`, so the SPA
  fallback pipeline is always in play regardless of the repo's `web/out` state;
- fake embedding model directory (same trick as `ApiFactory`) so semantic search boots offline;
- stub conversation stream source + turn-stream gate for future legs that need to control
  agent-chat stream timing.

## Browser harness (CDP)

Browser-level tests drive real chrome against a real host build of the web app:

- `[BrowserFact]` instead of `[Fact]` for every browser test — the attribute probes for a
  chrome binary and a node/npm toolchain at discovery time and emits `[SKIP]` with a reason
  when either is missing. A browser test placed outside `KestrelBrowserCollection` fails
  loudly on fixture use, so it cannot silently skip or run host-less.
- Discovery overrides for forcing a path: `OVERMIND_TEST_CHROME_BIN` and
  `OVERMIND_TEST_NODE_BIN` point at an explicit binary (set them to a nonexistent path to
  exercise the skip contract locally).
- `BrowserHostFixture` builds the web static export once per run (`npm ci` + `npm run build`
  in the repo's `web/`, generous timeout) and boots a second Kestrel host whose web root is
  that build output. The static gate on `OVERMIND_WEB_ROOT` serializes the env between the
  two hosts in one process — boot the browser fixture's host only through the fixture, and
  only one factory instance may set the env at a time.
- `CdpChatHarness` (Page + Runtime domains only) exposes `SeedState`, `Navigate`,
  `Evaluate`, `SnapshotDom`, `TypeIntoComposer`, `ClickSend`. Typing goes through React
  state (native prototype setter + bubbled input event on the controlled composer), so the
  legs observe what a human would produce.
- Existing legs to copy from: `SendPathOrderingBrowserTests` (stream gate held mid-turn,
  structural DOM pins) and `CompletionWindowBrowserTests` (two-slot response delay:
  history held after its handler, attach held before its handler).

