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
