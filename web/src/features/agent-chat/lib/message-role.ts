import type { AgentMessageRole } from "@/features/agent-chat/types";

const roleLabels: Record<AgentMessageRole, string> = {
  user: "You",
  assistant: "Agent",
  system: "System",
  tool: "Tool",
};

const roleRowClasses: Record<AgentMessageRole, string> = {
  user: "bg-primary/5",
  assistant: "bg-surface-raised/50",
  system: "bg-surface-raised/30 border border-dashed border-border",
  tool: "bg-background/40 border border-border",
};

const roleTextClasses: Record<AgentMessageRole, string> = {
  user: "text-primary",
  assistant: "text-agent",
  system: "text-muted",
  tool: "text-info",
};

const roleBodyClasses: Record<AgentMessageRole, string> = {
  user: "text-sm text-foreground",
  assistant: "text-sm text-foreground",
  system: "text-xs italic text-muted",
  tool: "font-mono text-xs text-muted",
};

export function toAgentMessageRole(value: string): AgentMessageRole {
  return isAgentMessageRole(value) ? value : "system";
}

export function messageRoleLabel(role: AgentMessageRole): string {
  return roleLabels[role];
}

export function messageRoleRowClass(role: AgentMessageRole): string {
  return roleRowClasses[role];
}

export function messageRoleTextClass(role: AgentMessageRole): string {
  return roleTextClasses[role];
}

export function messageRoleBodyClass(role: AgentMessageRole): string {
  return roleBodyClasses[role];
}

function isAgentMessageRole(value: string): value is AgentMessageRole {
  return Object.hasOwn(roleLabels, value);
}
