import type { AgentPlatform, EntryType } from "@/shared/api/types";

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
}
