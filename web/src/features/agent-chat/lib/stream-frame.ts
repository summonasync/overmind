import type { AgentRoutingSource, AgentStreamFrame } from "@/features/agent-chat/types";

export function parseAgentStreamFrame(name: string, data: string): AgentStreamFrame | null {
  let payload: unknown;
  try {
    payload = JSON.parse(data);
  } catch {
    return null;
  }
  if (payload === null || typeof payload !== "object" || Array.isArray(payload)) {
    return null;
  }

  const record = payload as Record<string, unknown>;
  if (name === "delta" && typeof record.text === "string") {
    return { kind: "delta", text: record.text };
  }
  if (name === "completed") {
    return {
      kind: "completed",
      messageId: typeof record.messageId === "string" ? record.messageId : null,
    };
  }
  if (name === "failed" && typeof record.error === "string") {
    return { kind: "failed", error: record.error };
  }
  if (name === "aborted") {
    return { kind: "aborted" };
  }
  if (name === "route" && typeof record.target === "string") {
    return {
      kind: "route",
      target: record.target,
      source: parseRoutingSource(record.source),
    };
  }
  if (name === "tool" && typeof record.tool === "string") {
    return { kind: "tool", toolName: record.tool };
  }
  return null;
}

function parseRoutingSource(value: unknown): AgentRoutingSource | null {
  return value === "structural" || value === "learned" || value === "classification" ? value : null;
}
