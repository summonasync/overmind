import { apiGet } from "@/shared/api/client";
import type { LanesRoster } from "@/features/lanes/types";

export function listLanes(projectId: string): Promise<LanesRoster> {
  return apiGet<LanesRoster>(`/projects/${projectId}/planning/lanes`);
}
