import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { parseAgentStreamFrame } from "../src/features/agent-chat/lib/stream-frame.ts";

const chipSource = () =>
  readFileSync(
    new URL("../src/features/agent-chat/components/agent-route-trace.tsx", import.meta.url),
    "utf8",
  );

test("route frames parse into a routing trace", () => {
  const frame = parseAgentStreamFrame(
    "route",
    JSON.stringify({ target: "worker", source: "learned" }),
  );
  assert.deepEqual(frame, { kind: "route", target: "worker", source: "learned" });
});

test("route frames keep the target when the source is absent or unknown", () => {
  const unknown = parseAgentStreamFrame(
    "route",
    JSON.stringify({ target: "chat", source: "psychic" }),
  );
  assert.deepEqual(unknown, { kind: "route", target: "chat", source: null });
  const absent = parseAgentStreamFrame("route", JSON.stringify({ target: "chat" }));
  assert.deepEqual(absent, { kind: "route", target: "chat", source: null });
});

test("route frames without a usable target are dropped", () => {
  assert.equal(parseAgentStreamFrame("route", JSON.stringify({ source: "learned" })), null);
  assert.equal(parseAgentStreamFrame("route", "not json"), null);
});

test("chip copy keeps the arrow, the subtle separator and the source hints", () => {
  const source = chipSource();
  assert.ok(source.includes("→ ${trace.target}"), "the chip lost the routing arrow");
  assert.ok(source.includes("· ${visibleSource}"), "the chip lost the routing source separator");
  assert.ok(
    source.includes("learned pattern") && source.includes("classifier"),
    "the chip lost the per-source hints",
  );
});
