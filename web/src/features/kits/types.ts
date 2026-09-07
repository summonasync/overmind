import type { AgentPlatform } from "@/shared/api/types";

export interface KitResponse {
  id: string;
  name: string;
  version: number;
  description: string;
  entryIds: string[];
}

export interface KitFormValues {
  name: string;
  version: number;
  description: string;
}

export interface MaterializedFileResponse {
  platform: AgentPlatform;
  relativePath: string;
  sha256: string;
}

export interface MaterializationReportResponse {
  projectId: string;
  kitId: string;
  files: MaterializedFileResponse[];
}

export interface DriftItemResponse {
  platform: AgentPlatform;
  relativePath: string;
  kind: "missing" | "modified";
}
