import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import {
  INFO_PANEL_SECTIONS_KEY,
  sectionCollapsed,
  subscribeSectionPrefs,
  toggleSection,
} from "../src/widgets/app-shell/lib/section-prefs.ts";

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
  for (const listener of [...(listenerBuckets.get("overmind:info-panel-sections") ?? [])]) {
    listener();
  }
};

subscribeSectionPrefs(() => undefined);

const source = (path: string) => readFileSync(new URL(path, import.meta.url), "utf8");

test("sections collapse independently within one page", () => {
  storage.clear();
  freshPage();
  toggleSection("planning", "stats");
  assert.equal(sectionCollapsed("planning", "stats"), true);
  assert.equal(sectionCollapsed("planning", "tips"), false);
});

test("collapse state stays scoped per page", () => {
  storage.clear();
  freshPage();
  toggleSection("planning", "stats");
  toggleSection("entries", "tips");
  assert.equal(sectionCollapsed("entries", "tips"), true);
  assert.equal(sectionCollapsed("planning", "tips"), false);
  assert.equal(sectionCollapsed("projects", "stats"), false);
  assert.equal(sectionCollapsed("projects", "tips"), false);
});

test("toggles pin the collapsed sections into the storage payload", () => {
  storage.clear();
  freshPage();
  toggleSection("planning", "stats");
  toggleSection("entries", "tips");
  const payload = JSON.parse(storage.get(INFO_PANEL_SECTIONS_KEY) as string);
  assert.deepEqual(payload, {
    planning: { stats: true },
    entries: { tips: true },
  });
});

test("state survives a reload because it is read back from storage", () => {
  storage.clear();
  storage.set(INFO_PANEL_SECTIONS_KEY, JSON.stringify({ projects: { stats: true } }));
  freshPage();
  assert.equal(sectionCollapsed("projects", "stats"), true);
  assert.equal(sectionCollapsed("projects", "tips"), false);
});

test("toggling a section back drops it from the payload", () => {
  storage.clear();
  freshPage();
  toggleSection("projects", "stats");
  assert.equal(sectionCollapsed("projects", "stats"), true);
  toggleSection("projects", "stats");
  assert.equal(sectionCollapsed("projects", "stats"), false);
  const payload = JSON.parse(storage.get(INFO_PANEL_SECTIONS_KEY) as string);
  assert.deepEqual(payload, { projects: {} });
});

test("a malformed payload falls back to the expanded defaults", () => {
  storage.clear();
  storage.set(INFO_PANEL_SECTIONS_KEY, "not-json");
  freshPage();
  assert.equal(sectionCollapsed("planning", "stats"), false);
});

test("the wrapper keeps the section icon slot and disclosure affordances", () => {
  const src = source("../src/widgets/app-shell/ui/info-panel-collapsible-section.tsx");
  assert.ok(src.includes("icon?: LucideIcon"), "the InfoPanelSection icon slot survives");
  assert.ok(src.includes("aria-expanded"), "the header announces its disclosure state");
  assert.ok(src.includes("rotate-180"), "the chevron flips when a section hides");
  assert.ok(src.includes("focus-visible:ring-2"), "the header button carries a focus ring");
  assert.ok(src.includes("useSectionPrefs"), "collapse state comes from the prefs hook");
});

test("the hook wires the shared store through useSyncExternalStore", () => {
  const src = source("../src/widgets/app-shell/lib/use-section-prefs.ts");
  assert.ok(src.includes("useSyncExternalStore"), "reads go through the external store");
  assert.ok(src.includes("subscribeSectionPrefs"), "subscriptions live in the store module");
  assert.ok(src.includes("toggleSection"), "writes go through the store module");
});

test("planning, projects and entries panels fold their stats and tips blocks", () => {
  const flat = (src: string) => src.replace(/\s+/g, " ");
  const planning = flat(source("../src/pages/planning-page/ui/planning-info-panel.tsx"));
  assert.ok(
    planning.includes('<InfoPanelCollapsibleSection page="planning" section="stats"'),
  );
  assert.ok(planning.includes('<InfoPanelCollapsibleSection page="planning" section="tips"'));
  const projects = flat(source("../src/widgets/app-shell/ui/projects-info-panel.tsx"));
  assert.ok(projects.includes('<InfoPanelCollapsibleSection page="projects" section="stats"'));
  const entries = flat(source("../src/pages/entries-page/ui/entries-info-panel.tsx"));
  assert.ok(entries.includes('<InfoPanelCollapsibleSection page="entries" section="stats"'));
  assert.ok(entries.includes('<InfoPanelCollapsibleSection page="entries" section="tips"'));
});
