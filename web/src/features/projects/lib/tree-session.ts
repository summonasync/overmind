import { readJson, writeJson } from "@/shared/lib/session-json";

const EXPANDED_PATHS_KEY = "overmind.tree.expandedPaths";
const PROJECTS_OPEN_KEY = "overmind.tree.projectsOpen";
const LAST_PROJECT_KEY = "overmind.tree.lastProjectId";

export function getExpandedPaths(): string[] {
  const paths = readJson<unknown>(EXPANDED_PATHS_KEY, []);
  return Array.isArray(paths) ? paths.filter((path): path is string => typeof path === "string") : [];
}

export function saveExpandedPaths(paths: string[]): void {
  writeJson(EXPANDED_PATHS_KEY, paths);
}

export function isProjectsSectionOpen(): boolean {
  return readJson<boolean>(PROJECTS_OPEN_KEY, false);
}

export function saveProjectsSectionOpen(open: boolean): void {
  writeJson(PROJECTS_OPEN_KEY, open);
}

export function getLastActiveProjectId(): string | null {
  return readJson<string | null>(LAST_PROJECT_KEY, null);
}

export function saveLastActiveProjectId(projectId: string | null): void {
  writeJson(LAST_PROJECT_KEY, projectId);
}
