import { apiGet, apiPut } from "@/shared/api/client";
import type { LaneUpsertInput, LanesRoster } from "@/features/lanes/types";

export function listLanes(projectId: string): Promise<LanesRoster> {
  return apiGet<LanesRoster>(`/projects/${projectId}/planning/lanes`);
}

export function setLanes(projectId: string, lanes: LaneUpsertInput[]): Promise<LanesRoster> {
  return apiPut<LanesRoster>(`/projects/${projectId}/planning/lanes`, { lanes });
}
