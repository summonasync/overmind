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
    "lib/history-window.ts",
    "lib/seen.ts",
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
  assert.ok(api.includes("/wave/rooms/${encodeURIComponent(room)}/messages"));
  assert.ok(!api.includes('"/api'));
});

test("the room feed reads the history endpoint and the unread door is gone from web", () => {
  const api = sliceFile("api.ts");
  assert.ok(api.includes("listWaveHistory"));
  assert.ok(api.includes('search.set("limit"'));
  assert.ok(api.includes('search.set("before"'));
  assert.ok(!api.includes("listWaveUnread"));
  assert.ok(!api.includes("/unread"), "the unread endpoint receives zero web calls");
  assert.ok(!wholeSlice().includes("WaveUnreadPayload"));
  assert.ok(!wholeSlice().includes("WaveWatermark"));
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

test("feed keys are scoped by project and room, no alias segment", () => {
  const queries = sliceFile("queries.ts");
  assert.ok(queries.includes('[...waveChatKeys.all, "feed", projectId, room]'));
  assert.ok(!queries.includes("alias"));
});

test("the feed query fetches the tail and grows the window while unread exceeds it", () => {
  const queries = sliceFile("queries.ts");
  assert.ok(queries.includes("historyWindowSize(countUnread(roomStream(projectId, room), boundary))"));
  assert.ok(
    queries.includes("while (size < HISTORY_WINDOW_MAX && unreadExceedsWindow(page.items, size, boundary))"),
  );
  assert.ok(queries.includes("size = Math.min(HISTORY_WINDOW_MAX, size * 2)"));
  assert.ok(queries.includes("absorbMessages(projectId, page.items)"));
  assert.ok(queries.includes("return roomStream(projectId, room)"));
});

test("manual paging lands in the shared store and refreshes the feed cache", () => {
  const queries = sliceFile("queries.ts");
  assert.ok(queries.includes("setQueryData"));
  const panel = sliceFile("components/wave-chat-panel.tsx");
  assert.ok(panel.includes("limit: HISTORY_PAGE_LIMIT"));
  assert.ok(panel.includes("before: oldest.createdAt"));
  assert.ok(panel.includes("limit: HISTORY_WINDOW_MAX"));
  assert.ok(panel.includes("syncFeedCache(queryClient, projectId, activeRoom)"));
});

test("the panel feeds every room through one dynamic query batch", () => {
  const panel = sliceFile("components/wave-chat-panel.tsx");
  assert.equal(panel.split("useQueries(").length - 1, 1);
  assert.ok(panel.includes("useQueries({ queries: feedOptions })"));
});

test("feeds stay gated until the seen stamps are restored from storage", () => {
  const panel = sliceFile("components/wave-chat-panel.tsx");
  assert.ok(panel.includes("setSeenReady(true)"));
  const options = sliceFile("queries.ts");
  assert.ok(options.includes("enabled,"));
});

test("the badge math lives in unreadCounts and the room the reader sits in stays clear", () => {
  const panel = sliceFile("components/wave-chat-panel.tsx");
  assert.ok(panel.includes("unreadCounts(streams, seen)"));
  assert.ok(panel.includes("[activeRoom]: 0"));
});

test("opening a room stamps the seen state for the room being left", () => {
  const panel = sliceFile("components/wave-chat-panel.tsx");
  assert.ok(panel.includes("markSeen(projectId, activeRoom, leaving.createdAt)"));
  assert.ok(panel.includes("setSeen((current) => ({ ...current, [activeRoom]: leaving.createdAt }))"));
});

test("the open room keeps marking its newest message seen while it stays open", () => {
  const panel = sliceFile("components/wave-chat-panel.tsx");
  const restoreEnd = panel.indexOf("}, [projectId]);");
  const seenEffect = panel.slice(panel.indexOf("useEffect(", restoreEnd), panel.indexOf("const openRoom"));
  assert.ok(seenEffect.includes("markSeen(projectId, activeRoom, newestMessage.createdAt)"));
});

test("panel effects restore state and stamp seen, they never fetch", () => {
  const panel = sliceFile("components/wave-chat-panel.tsx");
  assert.equal(panel.split("useEffect(").length - 1, 2);
  const restoreEnd = panel.indexOf("}, [projectId]);");
  const restoreEffect = panel.slice(panel.indexOf("useEffect("), restoreEnd);
  assert.ok(restoreEffect.includes("loadOwnerAlias()"));
  assert.ok(restoreEffect.includes("loadSeenMap(projectId)"));
  assert.ok(!restoreEffect.includes("listWave") && !restoreEffect.includes("postWave"));
  const seenEffect = panel.slice(panel.indexOf("useEffect(", restoreEnd), panel.indexOf("const openRoom"));
  assert.ok(!seenEffect.includes("listWave") && !seenEffect.includes("postWave"));
});

test("the unread window anchors at the open-time stamp and the first arrival", () => {
  const panel = sliceFile("components/wave-chat-panel.tsx");
  assert.ok(panel.includes("anchor.room === activeRoom ? anchor.from : (seen[activeRoom] ?? null)"));
  assert.ok(panel.includes("anchor.room === activeRoom ? anchor.at : null"));
  assert.ok(panel.includes("from: seen[activeRoom] ?? null,"));
  assert.ok(panel.includes("at: newestMessage?.createdAt ?? null,"));
});

test("seen state persists through the shared session store under the wave-chat namespace", () => {
  const seen = sliceFile("lib/seen.ts");
  assert.ok(seen.includes('session-json'));
  assert.ok(seen.includes('"overmind.wave-chat.seen."'));
  assert.ok(seen.includes("export function loadLastSeen(projectId: string, room: string)"));
  assert.ok(seen.includes("export function markSeen(projectId: string, room: string, at: string)"));
});

test("the tail math mirrors the server contract: 20 floor, 500 ceiling, plus-five context", () => {
  const window = sliceFile("lib/history-window.ts");
  assert.ok(window.includes("HISTORY_WINDOW_MIN = 20"));
  assert.ok(window.includes("HISTORY_WINDOW_MAX = 500"));
  assert.ok(window.includes("Math.max(HISTORY_WINDOW_MIN, unreadCount + 5)"));
});

test("the message list renders show more and show all above a dimmed context tail", () => {
  const list = sliceFile("components/wave-message-list.tsx");
  assert.ok(list.includes("Show more"));
  assert.ok(list.indexOf("Show more") < list.indexOf("Show all"));
  assert.ok(list.includes("read.slice(-CONTEXT_TAIL_LIMIT)"));
  assert.ok(list.includes("CONTEXT_TAIL_LIMIT = 5"));
  assert.ok(list.includes("opacity-55"));
});

test("the unread block highlights with a left border and tint that survive role tints", () => {
  const list = sliceFile("components/wave-message-list.tsx");
  assert.ok(list.includes('"border-l-2 border-l-primary bg-primary/10"'));
  assert.ok(list.includes("aria-current={unread ?"));
});

test("scroll anchors to the first unread message or the bottom when nothing is unread", () => {
  const list = sliceFile("components/wave-message-list.tsx");
  assert.ok(list.includes('scrollIntoView({ block: "start" })'));
  assert.ok(list.includes("container.scrollTop = container.scrollHeight"));
  assert.ok(list.includes("[scrollKey, hasUnread, ready]"));
  assert.ok(list.includes("const ready = messages.length > 0"));
  assert.ok(list.includes("if (!ready)"));
});

test("paging keeps the viewport instead of jumping the reader around", () => {
  const list = sliceFile("components/wave-message-list.tsx");
  assert.ok(list.includes("container.scrollTop = container.scrollHeight - previousHeight + previousTop"));
});

test("the slice exports history reads and drops the unread door from its public api", () => {
  const index = sliceFile("index.ts");
  assert.ok(index.includes("listWaveHistory"));
  assert.ok(!index.includes("listWaveUnread"));
  assert.ok(!index.includes("WaveUnreadPayload"));
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
