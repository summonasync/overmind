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

export type AgentRoutingSource = "structural" | "learned" | "classification";

export interface AgentRouteTrace {
  target: string;
  source: AgentRoutingSource | null;
}

export type AgentStreamFrame =
  | { kind: "delta"; text: string }
  | { kind: "completed"; messageId: string | null }
  | { kind: "failed"; error: string }
  | { kind: "aborted" }
  | { kind: "route"; target: string; source: AgentRoutingSource | null };

export interface AgentReadState {
  conversationId: string;
  lastReadMessageId: string | null;
}

export interface AgentUsage {
  dailyBudget: number;
  spentToday: number;
  remainingToday: number | null;
  perTurnMaxTokens: number;
}

export type ChatTurnOutcome =
  | { kind: "idle" }
  | { kind: "completed"; delegated: boolean; completedAtMs: number }
  | { kind: "failed"; failedAtMs: number };

export interface AgentChatActivity {
  streaming: boolean;
  lastTurn: ChatTurnOutcome;
}
