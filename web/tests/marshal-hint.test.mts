import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import {
  MARSHAL_HINT_DISMISSED_KEY,
  dismissMarshalHint,
  marshalHintDismissed,
  subscribeMarshalHint,
} from "../src/widgets/marshal-hint/lib/marshal-hint-prefs.ts";

interface FakeStorage {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
}

const storage = new Map<string, string>();
const listenerBuckets = new Map<string, Array<() => void>>();

const fakeSessionStorage: FakeStorage = {
  getItem: (key) => (storage.has(key) ? (storage.get(key) as string) : null),
  setItem: (key, value) => {
    storage.set(key, value);
  },
};

(globalThis as { window?: unknown }).window = {
  sessionStorage: fakeSessionStorage,
  addEventListener: (type: string, listener: () => void) => {
    const bucket = listenerBuckets.get(type) ?? [];
    bucket.push(listener);
    listenerBuckets.set(type, bucket);
  },
  removeEventListener: (type: string, listener: () => void) => {
    const bucket = listenerBuckets.get(type);
    const at = bucket ? bucket.indexOf(listener) : -1;
    if (bucket && at >= 0) {
      bucket.splice(at, 1);
    }
  },
  dispatchEvent: (event: { type: string }) => {
    for (const listener of [...(listenerBuckets.get(event.type) ?? [])]) {
      listener();
    }
  },
};

const freshPage = () => {
  for (const listener of [...(listenerBuckets.get("overmind:marshal-hint-dismissed") ?? [])]) {
    listener();
  }
};

subscribeMarshalHint(() => undefined);

const source = (path: string) => readFileSync(new URL(path, import.meta.url), "utf8");

test("a fresh session shows the hint", () => {
  storage.clear();
  freshPage();
  assert.equal(marshalHintDismissed(), false);
});

test("dismissing writes the flag under the marshal session key", () => {
  storage.clear();
  freshPage();
  assert.equal(MARSHAL_HINT_DISMISSED_KEY, "overmind.hints.marshalDismissed");
  dismissMarshalHint();
  assert.equal(marshalHintDismissed(), true);
  const payload = JSON.parse(storage.get(MARSHAL_HINT_DISMISSED_KEY) as string);
  assert.equal(payload, true);
});

test("dismissal survives a reload because it is read back from storage", () => {
  storage.clear();
  storage.set(MARSHAL_HINT_DISMISSED_KEY, JSON.stringify(true));
  freshPage();
  assert.equal(marshalHintDismissed(), true);
});

test("a malformed payload falls back to showing the hint", () => {
  storage.clear();
  storage.set(MARSHAL_HINT_DISMISSED_KEY, "not-json");
  freshPage();
  assert.equal(marshalHintDismissed(), false);
});

test("the chip renders above the lane board", () => {
  const page = source("../src/pages/lanes-page/ui/lanes-page.tsx");
  const chip = page.indexOf("<MarshalHint />");
  const board = page.indexOf("<LaneBoard />");
  assert.ok(chip !== -1, "the lanes page mounts the marshal hint");
  assert.ok(board !== -1, "the lanes page keeps its lane board");
  assert.ok(chip < board, "the chip sits above the lane board");
});

test("the chip renders above the wave composer", () => {
  const page = source("../src/pages/wave-chat-page/ui/wave-chat-page.tsx");
  assert.ok(page.includes("MarshalHint"), "the wave chat page mounts the marshal hint");
  const panel = source("../src/features/wave-chat/components/wave-chat-panel.tsx");
  const slot = panel.indexOf("{hint}");
  const composer = panel.indexOf("<WaveComposer");
  assert.ok(slot !== -1, "the panel takes the hint as a slot");
  assert.ok(composer !== -1, "the panel keeps its composer");
  assert.ok(slot < composer, "the hint slot sits above the composer");
});

test("the hint copy pins both command tokens in mono", () => {
  const chip = source("../src/widgets/marshal-hint/ui/marshal-hint.tsx");
  assert.ok(chip.includes("/wave-marshal"), "the marshal command is spelled out");
  assert.ok(chip.includes("/loop 5m /wave-marshal"), "the loop pattern is spelled out");
  assert.ok(chip.includes("font-mono"), "command tokens render in mono");
});

test("the chip is an inline info chip, not a banner", () => {
  const chip = source("../src/widgets/marshal-hint/ui/marshal-hint.tsx");
  assert.ok(chip.includes("rounded-lg"), "the chip carries rounded corners, not full-bleed edges");
  assert.ok(
    chip.includes("border-info/30 bg-info/10 px-3 py-2 text-xs"),
    "the visual contract of the info chip holds",
  );
  assert.ok(!chip.includes("setInterval"), "no auto-dismiss timer");
});

test("the widget exposes the chip through its barrel", () => {
  const barrel = source("../src/widgets/marshal-hint/index.ts");
  assert.equal(barrel.trim(), 'export { MarshalHint } from "./ui/marshal-hint";');
});
