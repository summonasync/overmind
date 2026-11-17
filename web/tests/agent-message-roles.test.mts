import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import {
  messageRoleBodyClass,
  messageRoleLabel,
  messageRoleRowClass,
  messageRoleTextClass,
  toAgentMessageRole,
} from "../src/features/agent-chat/lib/message-role.ts";
import { parseAgentStreamFrame } from "../src/features/agent-chat/lib/stream-frame.ts";
import { latestReportableId } from "../src/features/agent-chat/lib/seen-tracker.ts";

const read = (relative: string) =>
  readFileSync(new URL(relative, import.meta.url), "utf8");

const typesSource = () => read("../src/features/agent-chat/types.ts");
const apiSource = () => read("../src/features/agent-chat/api.ts");
const listSource = () => read("../src/features/agent-chat/components/agent-message-list.tsx");
const chatSource = () => read("../src/features/agent-chat/lib/use-agent-chat.ts");

test("the client role union carries the host vocabulary", () => {
  assert.match(
    typesSource(),
    /AgentMessageRole\s*=\s*"user" \| "assistant" \| "system" \| "tool"/,
    "AgentMessageRole does not carry the four host roles",
  );
  for (const role of ["user", "assistant", "system", "tool"] as const) {
    assert.equal(toAgentMessageRole(role), role);
  }
});

test("an unrecognized wire role falls back to system, never onto the human", () => {
  assert.equal(toAgentMessageRole("Tool"), "system");
  assert.equal(toAgentMessageRole("function"), "system");
  assert.equal(toAgentMessageRole(""), "system");
  assert.equal(toAgentMessageRole("constructor"), "system");
  assert.notEqual(toAgentMessageRole("function"), "user");
});

test("the role predicate reads the exhaustive map, not a parallel list", () => {
  const source = read("../src/features/agent-chat/lib/message-role.ts");
  assert.ok(!source.includes("hostRoles"), "a hand-maintained role list is back — a fifth role would silently become system");
  assert.match(source, /isAgentMessageRole[\s\S]*Object\.hasOwn\(roleLabels, value\)/);
});

test("only the human role reads as You", () => {
  assert.equal(messageRoleLabel("user"), "You");
  assert.equal(messageRoleLabel("assistant"), "Agent");
  assert.equal(messageRoleLabel("tool"), "Tool");
  assert.equal(messageRoleLabel("system"), "System");

  const youLabels = (["user", "assistant", "system", "tool"] as const).filter(
    (role) => messageRoleLabel(role) === "You",
  );
  assert.deepEqual(youLabels, ["user"]);
});

test("the role styles are exclusive branches, not stacked overrides", () => {
  const roles = ["user", "assistant", "system", "tool"] as const;
  const rows = roles.map(messageRoleRowClass);
  const names = roles.map(messageRoleTextClass);
  const bodies = roles.map(messageRoleBodyClass);

  assert.equal(new Set(rows).size, 4, "two roles share a row style — cn() cannot override it");
  assert.equal(new Set(names).size, 4, "two roles share a name color — cn() cannot override it");
  assert.equal(rows.filter((value) => value.includes("bg-primary/5")).length, 1, "the human row style leaked");
  assert.equal(names.filter((value) => value === "text-primary").length, 1, "the human name color leaked");

  assert.equal(bodies[0], bodies[1], "human and agent prose should read alike");
  assert.notEqual(bodies[2], bodies[0], "a system notice reads as plain prose");
  assert.notEqual(bodies[3], bodies[0], "a tool line reads as plain prose");
  assert.ok(bodies[3].includes("font-mono"), "the tool line lost its machine styling");

  const bodyBase = listSource().match(/"([^"]*mt-1[^"]*)"/)?.[1];
  assert.ok(bodyBase !== undefined, "the message body lost its base class string");
  const baseTokens = bodyBase.split(" ").filter((token) => token.length > 0);
  assert.ok(baseTokens.length >= 3, `the base class string yielded ${baseTokens.length} tokens to check`);
  for (const role of roles) {
    for (const token of baseTokens) {
      assert.ok(
        !messageRoleBodyClass(role).includes(token),
        `the ${role} body repeats the base token ${token}`,
      );
    }
  }
});

test("history rows keep the role the store wrote", () => {
  const source = apiSource();
  assert.ok(!source.includes('response.role === "assistant" ? "assistant" : "user"'), "toMessage still collapses roles");
  assert.ok(source.includes("toAgentMessageRole(response.role)"), "toMessage does not map the wire role");
});

test("the stream parser reads a tool frame and names the tool", () => {
  const frame = parseAgentStreamFrame("tool", JSON.stringify({ tool: "get_board_context" }));
  assert.deepEqual(frame, { kind: "tool", toolName: "get_board_context" });
});

test("a tool frame without a usable name is dropped", () => {
  assert.equal(parseAgentStreamFrame("tool", JSON.stringify({ tool: 7 })), null);
  assert.equal(parseAgentStreamFrame("tool", "{}"), null);
  assert.equal(parseAgentStreamFrame("tool", "not json"), null);
});

test("an event name the client does not know is dropped, not thrown", () => {
  assert.doesNotThrow(() => parseAgentStreamFrame("mystery", JSON.stringify({ text: "hi" })));
  assert.equal(parseAgentStreamFrame("mystery", JSON.stringify({ text: "hi" })), null);
});

test("the live tool row is written the way the host persists it", () => {
  const source = chatSource();
  assert.ok(
    source.includes('content: JSON.stringify({ tool: toolName })'),
    "the live tool row no longer mirrors the host's tool label format",
  );
  const appender = source.slice(source.indexOf("const appendLiveTool"), source.indexOf("const attachRef"));
  assert.ok(appender.includes('role: "tool"'), "the live row is not appended as a tool row");
  assert.ok(!appender.includes('role: "assistant"'), "the live tool row claims the agent");
  assert.equal(
    source.split("onTool: (toolName) => appendLiveTool(").length - 1,
    2,
    "the tool handler must be wired on both the attach and the send stream",
  );
});

test("a tool call that precedes the first delta still lands above the answer", () => {
  const source = chatSource();
  const send = source.slice(source.indexOf("const send = async"));
  assert.ok(send.length > 0, "the send path was renamed — this pin needs its new marker");
  assert.ok(
    !send.includes('{ id: pendingAssistantId, role: "assistant"'),
    "send() appends the empty assistant row eagerly again, so live tool rows render under it",
  );
  assert.ok(send.includes("ensurePendingAssistant(activeId)"), "send() lost the lazy assistant placeholder");
  assert.ok(source.includes("ensurePendingAssistant(conversationId)"), "the attach path lost its lazy assistant placeholder");
  assert.ok(
    listSource().includes("{streaming ? ("),
    "the reply indicator no longer comes from the streaming flag, so the empty row may have been its only driver",
  );
});

test("pending tool rows are never reported as read", () => {
  assert.equal(latestReportableId(["a1", "pending-tool-4"]), "a1");
  assert.equal(latestReportableId(["pending-tool-4"]), null);
});

test("the message list renders roles through the presentation map", () => {
  const source = listSource();
  assert.ok(!source.includes('message.role === "user"'), "the list still branches on the binary role");
  for (const helper of ["messageRoleLabel", "messageRoleRowClass", "messageRoleTextClass", "messageRoleBodyClass"]) {
    assert.ok(source.includes(`${helper}(message.role)`), `MessageRow lost ${helper}`);
  }
  assert.ok(source.includes("data-agent-message-role"), "the row lost its role marker for inspection");
});

test("the feature public API still re-exports the widened role type", () => {
  const source = read("../src/features/agent-chat/index.ts");
  assert.ok(source.includes("AgentMessageRole,"), "index.ts stopped exporting AgentMessageRole");
});
