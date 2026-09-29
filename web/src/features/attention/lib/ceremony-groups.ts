import type { CeremonyFeature } from "../types";

export interface CeremonyGroup {
  projectId: string;
  projectName: string;
  features: CeremonyFeature[];
}

export function groupCeremoniesByProject(features: CeremonyFeature[]): CeremonyGroup[] {
  const groups: CeremonyGroup[] = [];
  const byProject = new Map<string, CeremonyGroup>();
  for (const feature of features) {
    let group = byProject.get(feature.projectId);
    if (group === undefined) {
      group = { projectId: feature.projectId, projectName: feature.projectName, features: [] };
      byProject.set(feature.projectId, group);
      groups.push(group);
    }
    group.features.push(feature);
  }
  return groups;
}
