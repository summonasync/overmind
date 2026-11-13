import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { inboxHolderLabel, inboxRowHref } from "../src/features/planning/lib/human-inbox.ts";
import type { PlanningInboxItem } from "../src/features/planning/types.ts";

const panel = () =>
  readFileSync(
    new URL("../src/features/planning/components/human-inbox-panel.tsx", import.meta.url),
    "utf8",
  );

const planningPage = () =>
  readFileSync(new URL("../src/pages/planning-page/ui/planning-page.tsx", import.meta.url), "utf8");

const planningHooks = () =>
  readFileSync(new URL("../src/features/planning/lib/use-planning.ts", import.meta.url), "utf8");

const planningQueries = () =>
  readFileSync(new URL("../src/features/planning/queries.ts", import.meta.url), "utf8");

const item = (overrides: Partial<PlanningInboxItem>): PlanningInboxItem => ({
  slug: "227-awaiting-human-visibility",
  title: "Awaiting human visibility",
  phase: "awaiting-human",
  queuePosition: 3,
  claimedByAlias: "mark-wolff",
  claimState: "alive",
  pendingSince: "2026-11-13T09:50:00Z",
  link: null,
  ...overrides,
});

test("the api-provided link wins over the local builder", () => {
  const bound = "http://overmind.local/dashboard?project=p1&feature=227-awaiting-human-visibility&tab=result";
  assert.equal(inboxRowHref("p1", item({ link: bound })), bound);
});

test("a null or empty link falls back to the dashboard feature link", () => {
  assert.equal(
    inboxRowHref("p1", item({ link: null })),
    "/dashboard?project=p1&feature=227-awaiting-human-visibility&tab=result",
  );
  assert.equal(
    inboxRowHref("p1", item({ link: "" })),
    "/dashboard?project=p1&feature=227-awaiting-human-visibility&tab=result",
  );
});

test("released claims read as released, holders keep their alias", () => {
  assert.equal(inboxHolderLabel(item({ claimState: "released", claimedByAlias: null })), "released");
  assert.equal(inboxHolderLabel(item({ claimState: "stale" })), "mark-wolff");
  assert.equal(inboxHolderLabel(item({ claimState: "ended" })), "mark-wolff");
});

test("an unclaimed row shows no holder", () => {
  assert.equal(inboxHolderLabel(item({ claimedByAlias: null, claimState: null })), null);
});

test("rows render the server phase verbatim and never a client-side label", () => {
  const source = panel();
  assert.ok(source.includes(`import { inboxHolderLabel, inboxRowHref } from "@/features/planning/lib/human-inbox";`));
  assert.ok(source.includes(`{item.phase}`));
  assert.ok(!source.includes(`"awaiting-human"`));
});

test("each row links to the result tab through inboxRowHref", () => {
  const source = panel();
  assert.ok(source.includes("const href = inboxRowHref(projectId, item);"));
  assert.deepEqual(
    [...source.matchAll(/href=\{href\}/g)].length >= 2,
    true,
  );
});

test("rows show pending-since, holder and the shared number badge", () => {
  const source = panel();
  assert.ok(source.includes(`import { FeatureNumberBadge } from "@/features/planning/components/feature-number-badge";`));
  assert.ok(source.includes(`formatRelativeTime(item.pendingSince)`));
  assert.ok(source.includes(`{holder !== null ? (`));
});

test("an empty inbox shows the zero-state, not an error", () => {
  const source = panel();
  assert.ok(source.includes("All clear — nothing is waiting on you."));
  assert.ok(source.includes("items.length === 0"));
});

test("the panel mounts on the planning board next to the phase view", () => {
  const source = planningPage();
  assert.ok(source.includes(`<HumanInboxPanel projectId={projectId} numbering={numbering} />`));
  const panelAt = source.indexOf("<HumanInboxPanel");
  const boardAt = source.indexOf("<FeatureBoard");
  assert.ok(panelAt !== -1 && boardAt !== -1 && panelAt < boardAt);
});

test("the inbox key lives in the planning key factory with live sync", () => {
  const source = planningQueries();
  assert.ok(source.includes(`inbox: (projectId: string) => [...planningKeys.all, "inbox", projectId] as const,`));
  assert.ok(source.includes("humanInboxQueryOptions"));
  const options = source.slice(source.indexOf("humanInboxQueryOptions"));
  assert.ok(options.includes("refetchInterval: LIVE_SYNC_INTERVAL_MS"));
});

test("planning mutations invalidate the inbox like the board", () => {
  const source = planningHooks();
  assert.equal(
    [...source.matchAll(/planningKeys\.inbox\(projectId\)/g)].length,
    3,
  );
});
