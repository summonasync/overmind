import type { AgentPlatform, EntryType } from "@/shared/api/types";

export type EntryState = "active" | "lifted" | "superseded";

export type EntryStateFilter = EntryState | "all";

export const ENTRY_STATES: readonly EntryState[] = ["active", "lifted", "superseded"];

export interface SkillFileDto {
  platform: AgentPlatform;
  relativePath: string;
  content: string;
}

export interface EntryResponse {
  id: string;
  projectId: string;
  type: EntryType;
  title: string;
  content: string;
  currentVersion: number;
  createdAt: string;
  updatedAt: string;
  tags: string[];
  skillFiles: SkillFileDto[];
  authorAlias?: string | null;
  state?: EntryState;
  stateAt?: string | null;
  stateBy?: string | null;
  stateReason?: string | null;
  stateProof?: string | null;
  supersededById?: string | null;
  enforces?: boolean;
}

export interface EntryVersionResponse {
  version: number;
  content: string;
  createdAt: string;
}

export interface TagResponse {
  id: string;
  name: string;
  color: string | null;
}

export interface EntryFormValues {
  type: EntryType;
  title: string;
  content: string;
  tags: string[];
  skillFiles: SkillFileDto[];
}

export interface EntryListFilters {
  type: EntryType | "";
  tag: string;
  state?: EntryStateFilter;
  since?: string;
  until?: string;
  author?: string;
}
