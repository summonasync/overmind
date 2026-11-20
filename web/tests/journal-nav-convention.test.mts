import { strict as assert } from "node:assert";
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { test } from "node:test";

const sourceOf = (relative: string) =>
  readFileSync(new URL(relative, import.meta.url), "utf8");

// The two main-nav surfaces duplicate their item list by hand. Until they share one
// module, this test IS the primitive: any third NAV_ITEMS declaration must be
// registered here, and both lists must agree with the pinned set below.
const NAV_SURFACES = [
  ["topbar", "../src/widgets/app-shell/ui/app-topbar.tsx"],
  ["mobile", "../src/widgets/app-shell/ui/mobile-nav.tsx"],
] as const;

// Order is the nav order; Journal sits after Attention so the capture surfaces cluster.
const PINNED_ITEMS = [
  { href: "/", label: "Projects", icon: "Folder" },
  { href: "/attention", label: "Attention", icon: "BellRing" },
  { href: "/journal", label: "Journal", icon: "NotebookPen" },
  { href: "/agent-chat", label: "Agent chat", icon: "Bot" },
  { href: "/search", label: "Search", icon: "Search" },
  { href: "/wiki", label: "Wiki", icon: "BookOpen" },
  { href: "/settings", label: "Settings", icon: "Settings" },
];

const routeFileFor = (href: string) =>
  href === "/" ? "../src/app/page.page.tsx" : `../src/app${href}/page.page.tsx`;

const readItems = (source: string) => {
  const block = source.match(/const NAV_ITEMS: NavItem\[\] = \[([\s\S]*?)\n\];/);
  assert.ok(block, "no NAV_ITEMS block — the surface stopped declaring its item list");
  return [...block[1].matchAll(/href: "([^"]+)", label: "([^"]+)", icon: ([A-Za-z0-9_]+)/g)].map(
    (match) => ({ href: match[1], label: match[2], icon: match[3] }),
  );
};

test("the shell mounts every pinned nav surface", () => {
  const shell = sourceOf("../src/widgets/app-shell/ui/app-shell.tsx");
  assert.ok(shell.includes("<AppTopbar"), "the desktop nav surface is gone from the shell");
  assert.ok(shell.includes("<MobileNav"), "the mobile nav surface is gone from the shell");
});

test("every nav surface carries the pinned item set, in order", () => {
  for (const [name, path] of NAV_SURFACES) {
    assert.deepEqual(readItems(sourceOf(path)), PINNED_ITEMS, `${name} drifted from the pinned nav set`);
  }
});

test("nav hrefs are extensionless — a .html suffix silently kills the bare-N quick key", () => {
  // task 6 scopes the capture hotkey by pathname === "/journal"; the static export
  // serves /journal.html, so a hand-written suffixed href would never match.
  for (const [name, path] of NAV_SURFACES) {
    for (const item of readItems(sourceOf(path))) {
      assert.ok(!item.href.endsWith(".html"), `${name} links ${item.href} with a .html suffix`);
      assert.match(item.href, /^\//, `${name} href ${item.href} is not rooted`);
    }
  }
  const journal = PINNED_ITEMS.find((item) => item.label === "Journal");
  assert.equal(journal?.href, "/journal", "the Journal item must land on the extensionless route");
});

test("every nav href has a route to render", () => {
  for (const item of PINNED_ITEMS) {
    assert.ok(existsSync(new URL(routeFileFor(item.href), import.meta.url)), `${item.href} has no page route`);
  }
});

test("no app-shell surface hand-rolls nav outside the pinned lists", () => {
  const uiDir = new URL("../src/widgets/app-shell/ui/", import.meta.url);
  const declared = new Set(NAV_SURFACES.map(([, path]) => path.replace("../src/widgets/app-shell/ui/", "")));
  const extra = readdirSync(uiDir).filter(
    (file) => file.endsWith(".tsx") && !declared.has(file) && sourceOf(`../src/widgets/app-shell/ui/${file}`).includes("const NAV_ITEMS"),
  );
  assert.deepEqual(extra, [], "a nav list appeared that this test does not pin");
});

test("the Journal route is wired through the pages layer, not inlined in the shell", () => {
  const route = sourceOf("../src/app/journal/page.page.tsx");
  assert.ok(route.includes('from "@/pages/journal-page"'), "the journal route bypasses the pages layer");
  assert.ok(!route.includes('from "@/features/'), "the journal route reaches into a feature directly");
});
