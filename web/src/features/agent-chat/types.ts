export type AgentMessageRole = "user" | "assistant";

export interface AgentConversation {
  id: string;
  projectId: string | null;
  title: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface AgentMessage {
  id: string;
  role: AgentMessageRole;
  content: string;
  createdAt: string;
}

export interface AgentConversationsPayload {
  items: AgentConversation[];
}

export interface AgentHistoryPayload {
  items: AgentMessage[];
}

export type AgentStreamFrame =
  | { kind: "delta"; text: string }
  | { kind: "completed"; messageId: string | null }
  | { kind: "failed"; error: string };
