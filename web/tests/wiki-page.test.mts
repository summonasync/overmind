import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { WIKI_SECTIONS } from "../src/pages/wiki-page/lib/wiki-sections.ts";

const route = () =>
  readFileSync(new URL("../src/app/wiki/page.page.tsx", import.meta.url), "utf8");

const toc = () =>
  readFileSync(new URL("../src/pages/wiki-page/ui/wiki-toc.tsx", import.meta.url), "utf8");

const page = () =>
  readFileSync(new URL("../src/pages/wiki-page/ui/wiki-page.tsx", import.meta.url), "utf8");

const content = () =>
  readFileSync(new URL("../src/pages/wiki-page/ui/wiki-content.tsx", import.meta.url), "utf8");

const topbar = () =>
  readFileSync(new URL("../src/widgets/app-shell/ui/app-topbar.tsx", import.meta.url), "utf8");

const mobileNav = () =>
  readFileSync(new URL("../src/widgets/app-shell/ui/mobile-nav.tsx", import.meta.url), "utf8");

test("the route mounts the wiki page slice through its barrel", () => {
  const source = route();
  assert.ok(source.includes('from "@/pages/wiki-page"'), "the slice barrel is the import edge");
  assert.ok(source.includes("<WikiPage />"), "the route renders the page composition");
});

test("the spec sections exist as toc-linkable anchors", () => {
  assert.equal(WIKI_SECTIONS.length, 8, "the spec pins eight sections");
  const source = content();
  const ids = WIKI_SECTIONS.map((section) => section.id);
  assert.equal(new Set(ids).size, ids.length, "section ids are unique");
  for (const section of WIKI_SECTIONS) {
    assert.ok(
      source.includes(`id="${section.id}"`),
      `section ${section.id} has no anchor in the content`,
    );
    assert.ok(
      source.includes(section.title),
      `section ${section.id} is missing its heading`,
    );
  }
});

test("the toc renders from the shared meta and links every anchor", () => {
  const source = toc();
  assert.ok(source.includes("WIKI_SECTIONS"), "the toc renders from the shared section meta");
  assert.ok(source.includes("href={`#${section.id}`}"), "anchor hrefs derive from the meta ids");
});

test("every section clears the sticky topbar when an anchor scrolls to it", () => {
  const source = content();
  const sectionTag = source.match(/<section id=\{id\}[^>]*>/)?.[0] ?? "";
  assert.ok(sectionTag.includes("scroll-mt-20"), "the shared section wrapper offsets the sticky topbar");
});

test("the toc rides the info panel on desktop and a native collapse on mobile", () => {
  const source = page();
  assert.ok(source.includes("useInfoPanel(<WikiToc />)"), "the desktop toc lives in the info panel");
  assert.ok(source.includes("<details"), "mobile falls back to a native details collapse");
  assert.ok(source.includes("xl:hidden"), "the mobile collapse stays off the desktop layout");
});

test("sections with a live page reuse the page-hints mapping, no private copies", () => {
  const source = content();
  assert.ok(source.includes("<PageHintList"), "hints render through the shared renderer");
  for (const path of ["/planning", "/lanes", "/wave-chat"]) {
    assert.ok(source.includes(`hintPath="${path}"`), `${path} hints surface in the wiki`);
  }
});

test("the nav item sits between Search and Settings carrying BookOpen", () => {
  for (const [name, source] of [
    ["topbar", topbar()],
    ["mobile nav", mobileNav()],
  ] as const) {
    const search = source.indexOf('"/search"');
    const wiki = source.indexOf('"/wiki"');
    const settings = source.indexOf('"/settings"');
    assert.ok(
      search !== -1 && wiki > search && wiki < settings,
      `${name}: nav order is Search → Wiki → Settings`,
    );
    assert.ok(source.includes("BookOpen"), `${name}: the wiki item carries the BookOpen icon`);
    assert.ok(source.includes('label: "Wiki"'), `${name}: the item is labeled Wiki`);
  }
});
