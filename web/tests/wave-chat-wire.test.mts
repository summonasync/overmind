import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const sliceFile = (file: string) =>
  readFileSync(new URL(`../src/features/wave-chat/${file}`, import.meta.url), "utf8");

const wholeSlice = () =>
  [
    "types.ts",
    "api.ts",
    "queries.ts",
    "index.ts",
    "lib/instant.ts",
    "lib/message-stream.ts",
    "lib/room-order.ts",
    "lib/unread-counts.ts",
    "lib/composer.ts",
    "lib/feed-store.ts",
    "lib/owner-alias.ts",
    "lib/roster.ts",
    "lib/role-color.ts",
    "lib/use-wave-chat.ts",
    "components/wave-chat-panel.tsx",
    "components/wave-roster.tsx",
    "components/wave-room-header.tsx",
    "components/wave-room-list.tsx",
    "components/wave-message-list.tsx",
    "components/wave-composer.tsx",
  ]
    .map(sliceFile)
    .join("\n");

test("every wave-chat read and write goes through the shared api client", () => {
  const api = sliceFile("api.ts");
  assert.ok(api.includes('from "@/shared/api/client"'));
  assert.ok(!api.includes("fetch("));
  assert.ok(!wholeSlice().match(/\bfetch\(/), "no direct fetch anywhere in the slice");
});

test("the human door paths carry no /api prefix because the client owns it", () => {
  const api = sliceFile("api.ts");
  assert.ok(api.includes("`/projects/${projectId}/wave/rooms`"));
  assert.ok(api.includes("/wave/rooms/${encodeURIComponent(room)}/unread?"));
  assert.ok(api.includes("/wave/rooms/${encodeURIComponent(room)}/messages`"));
  assert.ok(!api.includes('"/api'));
});

test("the unread poll targets the reader alias as a query parameter", () => {
  assert.ok(sliceFile("api.ts").includes("new URLSearchParams({ alias })"));
});

test("the composer posts exactly the owner identity and the text", () => {
  assert.ok(sliceFile("api.ts").includes("{ authorAlias, text }"));
});

test("rooms, feeds and the roster poll on the shared live-sync interval", () => {
  const queries = sliceFile("queries.ts");
  assert.equal(queries.split("refetchInterval: LIVE_SYNC_INTERVAL_MS").length - 1, 3);
  assert.ok(queries.includes('import { LIVE_SYNC_INTERVAL_MS } from "@/shared/lib/live-sync"'));
});

test("the roster reads the participants endpoint through the shared client", () => {
  const api = sliceFile("api.ts");
  assert.ok(api.includes("`/projects/${projectId}/wave/participants`"));
});

test("the roster keys off the project beside rooms and feeds", () => {
  assert.ok(
    sliceFile("queries.ts").includes('[...waveChatKeys.all, "participants", projectId]'),
  );
});

test("the sidebar stacks the roster above the rooms and the header carries presence", () => {
  const panel = sliceFile("components/wave-chat-panel.tsx");
  const rosterAt = panel.indexOf("<WaveRoster");
  const roomsAt = panel.indexOf("<WaveRoomList");
  assert.ok(rosterAt !== -1 && roomsAt !== -1 && rosterAt < roomsAt);
  assert.ok(panel.includes("<WaveRoomHeader"));
});

test("feeds key off the alias so a changed owner re-reads from its own watermark", () => {
  assert.ok(
    sliceFile("queries.ts").includes('[...waveChatKeys.all, "feed", projectId, alias, room]'),
  );
});

test("the feed query folds its poll into the shared stream store", () => {
  const queries = sliceFile("queries.ts");
  assert.ok(queries.includes("absorbMessages(projectId, page.items)"));
  assert.ok(queries.includes("return roomStream(projectId, room)"));
});

test("an empty owner alias disables the feed poll instead of 400ing the host", () => {
  assert.ok(sliceFile("queries.ts").includes("enabled: alias.trim().length > 0"));
});

test("the panel feeds every room through one dynamic query batch", () => {
  const panel = sliceFile("components/wave-chat-panel.tsx");
  assert.equal(panel.split("useQueries(").length - 1, 1);
  assert.ok(panel.includes("useQueries({ queries: feedOptions })"));
});

test("the badge math lives in unreadCounts and the room the reader sits in stays clear", () => {
  const panel = sliceFile("components/wave-chat-panel.tsx");
  assert.ok(panel.includes("unreadCounts(streams, seenCounts)"));
  assert.ok(panel.includes("[activeRoom]: 0"));
});

test("opening a room stamps what the reader has seen", () => {
  const panel = sliceFile("components/wave-chat-panel.tsx");
  assert.ok(panel.includes("setSeenCounts((current) => ({ ...current, [activeRoom]: activeStream.length }))"));
});

test("the slice holds exactly one effect and it only restores the stored alias", () => {
  const panel = sliceFile("components/wave-chat-panel.tsx");
  assert.equal(wholeSlice().split("useEffect(").length - 1, 1);
  const effect = panel.slice(panel.indexOf("useEffect("), panel.indexOf("}, []);"));
  assert.ok(effect.includes("loadOwnerAlias()"));
  assert.ok(!effect.includes("listWave") && !effect.includes("postWave"));
});

test("the owner alias persists through the shared session store", () => {
  const owner = sliceFile("lib/owner-alias.ts");
  assert.ok(owner.includes('from "@/shared/lib/session-json"'));
  assert.ok(owner.includes('DEFAULT_OWNER_ALIAS = "owner"'));
});

test("the composer validates before mutating and sends only valid text", () => {
  const composer = sliceFile("components/wave-composer.tsx");
  assert.ok(composer.includes("validateComposerText(draft)"));
  assert.ok(composer.includes("post.mutate("));
  assert.ok(composer.includes("maxLength={WAVE_TEXT_MAX_LENGTH}"));
});

test("the chat rides the project tab bar next to lanes", () => {
  const tabs = readFileSync(
    new URL("../src/widgets/app-shell/ui/project-tabs.tsx", import.meta.url),
    "utf8",
  );
  const lanes = tabs.indexOf('{ key: "lanes"');
  const chat = tabs.indexOf('{ key: "chat", label: "Wave chat", pathname: "/wave-chat" }');
  assert.ok(lanes > -1);
  assert.ok(chat > lanes);
});

test("the route and the page slice exist", () => {
  const route = readFileSync(new URL("../src/app/wave-chat/page.page.tsx", import.meta.url), "utf8");
  assert.ok(route.includes('import { WaveChatPage } from "@/pages/wave-chat-page"'));
  assert.ok(route.includes("<WaveChatPage />"));
  const page = readFileSync(new URL("../src/pages/wave-chat-page/index.ts", import.meta.url), "utf8");
  assert.ok(page.includes('export { WaveChatPage } from "./ui/wave-chat-page"'));
});
