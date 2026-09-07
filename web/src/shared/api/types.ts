export type EntryType = "note" | "decision" | "gate" | "guard" | "skill";

export type AgentPlatform = "claude" | "kimiCode" | "codex" | "gemini" | "antigravity";

export const ENTRY_TYPES: readonly EntryType[] = ["note", "decision", "gate", "guard", "skill"];

export const AGENT_PLATFORMS: readonly AgentPlatform[] = [
  "claude",
  "kimiCode",
  "codex",
  "gemini",
  "antigravity",
];

export interface BrowseFolderResponse {
  path: string;
}
