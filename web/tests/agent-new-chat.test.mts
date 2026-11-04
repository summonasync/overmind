import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const read = (path: string) => readFileSync(new URL(path, import.meta.url), "utf8");

test("the conversation list carries the new chat action", () => {
  const source = read("../src/features/agent-chat/components/agent-conversation-list.tsx");
  assert.ok(source.includes("onNewChat: () => void"), "the list lost the new chat prop");
  assert.ok(source.includes("New chat"), "the list lost the new chat label");
});

test("both chat surfaces clear the selection for a fresh conversation", () => {
  const panel = read("../src/features/agent-chat/components/agent-chat-panel.tsx");
  assert.ok(panel.includes("onNewChat={startNewChat}"), "the panel is not wired to the new chat action");
  const popup = read("../src/widgets/agent-chat-bubble/ui/agent-chat-popup.tsx");
  assert.ok(popup.includes("onNewChat={startNewChat}"), "the popup is not wired to the new chat action");
  assert.ok(
    popup.includes("onConversationChange(null)") && popup.includes("setListOpen(false)"),
    "the popup new chat must drop the selection and close the conversation list",
  );
});
