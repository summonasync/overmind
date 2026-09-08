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

export interface TreeRoot {
  name: string;
  path: string;
  containsProjects: boolean;
}

export interface TreeRootsResponse {
  roots: TreeRoot[];
}

export interface DirectoryEntry {
  name: string;
  path: string;
  accessible: boolean;
  projectId: string | null;
  projectName: string | null;
}

export interface ListDirectoryResponse {
  path: string;
  truncated: boolean;
  entries: DirectoryEntry[];
}
