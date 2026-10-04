import type { PlanningArtifactResponse } from "@/features/planning/types";
import { taskHeading } from "./task-heading.ts";

export interface ArtifactDeletePreview {
  target: string;
  consequences: string[];
  warnings: string[];
}

export function artifactDeleteLabel(artifact: PlanningArtifactResponse): string {
  if (artifact.kind !== "task") {
    return artifact.kind;
  }
  return `task #${artifact.order} "${taskHeading(artifact.body, artifact.order)}"`;
}

export function artifactDependentLabels(
  artifact: PlanningArtifactResponse,
  siblings: PlanningArtifactResponse[],
): string[] {
  if (artifact.kind !== "task") {
    return [];
  }
  return siblings
    .filter(
      (candidate) =>
        candidate.kind === "task" && candidate.blockedBy?.includes(artifact.order) === true,
    )
    .map((dependent) => `#${dependent.order} ${taskHeading(dependent.body, dependent.order)}`);
}

export function artifactDeletePreview(
  artifact: PlanningArtifactResponse,
  siblings: PlanningArtifactResponse[],
): ArtifactDeletePreview {
  const consequences = [
    "The artifact and its change log disappear from the feature page and the board immediately.",
    "Its version history is removed from view; the rows stay in the database.",
    "The materialized BACKLOG file is removed on the next materialization pass.",
  ];

  const warnings: string[] = [];
  const dependents = artifactDependentLabels(artifact, siblings);
  if (dependents.length > 0) {
    warnings.push(
      `Clean up dependencies first: ${dependents.join(", ")} still ` +
        `${dependents.length === 1 ? "depends" : "depend"} on it — the server refuses the delete until then.`,
    );
  }
  if (artifact.frozenAt !== null) {
    warnings.push(`The ${artifact.kind} is validated; deletion is permanent.`);
  }

  return { target: artifactDeleteLabel(artifact), consequences, warnings };
}
