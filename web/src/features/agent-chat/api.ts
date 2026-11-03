import { apiDelete, apiGet, apiPost, apiStream, apiStreamGet } from "@/shared/api/client";
import type {
  AgentConversation,
  AgentConversationsPayload,
  AgentHistoryPayload,
  AgentMessageRole,
  AgentReadState,
  AgentRouteTrace,
  AgentUsage,
} from "@/features/agent-chat/types";
import { readSseEvents } from "@/shared/lib/sse";
import { parseAgentStreamFrame } from "@/features/agent-chat/lib/stream-frame";

interface ConversationResponse {
  id: string;
  projectId: string | null;
  title: string | null;
  createdAt: string;
  updatedAt: string;
}

interface MessageResponse {
  id: string;
  role: string;
  content: string;
  createdAt: string;
}

interface ReadStateResponse {
  conversationId: string;
  lastReadMessageId: string | null;
}

export interface AgentHistoryQuery {
  limit?: number;
  before?: string;
}

export interface AgentStreamHandlers {
  onDelta: (text: string) => void;
  onCompleted: (messageId: string | null) => void;
  onFailed: (error: string) => void;
  onAborted?: () => void;
  onAttached?: () => void;
  onRoute?: (trace: AgentRouteTrace) => void;
  signal?: AbortSignal;
}

function toConversation(response: ConversationResponse): AgentConversation {
  return {
    id: response.id,
    projectId: response.projectId,
    title: response.title,
    createdAt: response.createdAt,
    updatedAt: response.updatedAt,
  };
}

function toMessage(response: MessageResponse) {
  const role: AgentMessageRole = response.role === "assistant" ? "assistant" : "user";
  return { id: response.id, role, content: response.content, createdAt: response.createdAt };
}

export function listAgentConversations(projectId: string | null): Promise<AgentConversationsPayload> {
  const suffix = projectId === null ? "" : `?projectId=${encodeURIComponent(projectId)}`;
  return apiGet<AgentConversationsPayload>(`/agent/conversations${suffix}`);
}

export function createAgentConversation(
  projectId: string | null,
  title: string | null,
): Promise<AgentConversation> {
  return apiPost<ConversationResponse>("/agent/conversations", { projectId, title }).then(toConversation);
}

export function deleteAgentConversation(conversationId: string): Promise<void> {
  return apiDelete(`/agent/conversations/${encodeURIComponent(conversationId)}`);
}

export function markAgentConversationRead(
  conversationId: string,
  lastReadMessageId: string,
): Promise<AgentReadState> {
  return apiPost<ReadStateResponse>(
    `/agent/conversations/${encodeURIComponent(conversationId)}/read`,
    { lastReadMessageId },
  );
}

export async function fetchAgentUnreadCount(): Promise<number> {
  const response = await apiGet<{ count: number }>("/agent/conversations/unread");
  return response.count;
}

export function fetchAgentUsage(): Promise<AgentUsage> {
  return apiGet<AgentUsage>("/agent/usage");
}

export async function listAgentHistory(
  conversationId: string,
  query: AgentHistoryQuery = {},
): Promise<AgentHistoryPayload> {
  const search = new URLSearchParams();
  if (query.limit !== undefined) {
    search.set("limit", String(query.limit));
  }
  if (query.before !== undefined) {
    search.set("before", query.before);
  }
  const queryString = search.toString();
  const suffix = queryString.length > 0 ? `?${queryString}` : "";
  const payload = await apiGet<{ items: MessageResponse[] }>(
    `/agent/conversations/${encodeURIComponent(conversationId)}/messages${suffix}`,
  );
  return { items: payload.items.map(toMessage) };
}

export async function attachAgentTurn(
  conversationId: string,
  handlers: AgentStreamHandlers,
): Promise<"attached" | "idle"> {
  const response = await apiStreamGet(
    `/agent/conversations/${encodeURIComponent(conversationId)}/turn/stream`,
    handlers.signal,
  );
  if (response.status === 204) {
    return "idle";
  }
  if (!response.ok) {
    throw new Error(`The chat turn attach failed (${response.status}).`);
  }
  if (!response.body) {
    throw new Error("The chat turn attach returned no body.");
  }

  handlers.onAttached?.();

  await readSseEvents(response.body, (event) => {
    const frame = parseAgentStreamFrame(event.name, event.data);
    if (frame === null) {
      return;
    }
    if (frame.kind === "delta") {
      handlers.onDelta(frame.text);
    } else if (frame.kind === "completed") {
      handlers.onCompleted(frame.messageId);
    } else if (frame.kind === "aborted") {
      handlers.onAborted?.();
    } else if (frame.kind === "route") {
      handlers.onRoute?.({ target: frame.target, source: frame.source });
    } else {
      handlers.onFailed(frame.error);
    }
  });
  return "attached";
}

export function stopAgentTurn(conversationId: string): Promise<boolean> {
  return apiPost<{ stopped: boolean }>(
    `/agent/conversations/${encodeURIComponent(conversationId)}/turn/stop`,
    {},
  ).then((payload) => payload.stopped);
}

export async function streamAgentReply(
  conversationId: string,
  text: string,
  handlers: AgentStreamHandlers,
): Promise<void> {
  const response = await apiStream(
    `/agent/conversations/${encodeURIComponent(conversationId)}/messages/stream`,
    { text },
    handlers.signal,
  );
  if (!response.body) {
    throw new Error("The chat stream returned no body.");
  }

  await readSseEvents(response.body, (event) => {
    const frame = parseAgentStreamFrame(event.name, event.data);
    if (frame === null) {
      return;
    }
    if (frame.kind === "delta") {
      handlers.onDelta(frame.text);
    } else if (frame.kind === "completed") {
      handlers.onCompleted(frame.messageId);
    } else if (frame.kind === "aborted") {
      handlers.onAborted?.();
    } else if (frame.kind === "route") {
      handlers.onRoute?.({ target: frame.target, source: frame.source });
    } else {
      handlers.onFailed(frame.error);
    }
  });
}
