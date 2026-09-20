export type EntryType = "note" | "decision" | "gate" | "guard" | "skill";

export type AgentPlatform = "claude" | "kimiCode" | "codex" | "gemini" | "antigravity" | "qwen";

export const ENTRY_TYPES: readonly EntryType[] = ["note", "decision", "gate", "guard", "skill"];

export const AGENT_PLATFORMS: readonly AgentPlatform[] = [
  "claude",
  "kimiCode",
  "codex",
  "gemini",
  "antigravity",
  "qwen",
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

export interface QdrantHealthLeg {
  up: boolean;
  endpoint: string;
  error: string | null;
}

export interface EmbeddingHealthLeg {
  up: boolean;
  provider: string;
  model: string;
  dimensions: number | null;
  error: string | null;
}

export interface VectorSearchHealth {
  up: boolean;
  qdrant: QdrantHealthLeg;
  embedding: EmbeddingHealthLeg;
}
