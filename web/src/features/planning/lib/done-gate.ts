import type { ApiError } from "@/shared/api/client";
import type { PlanningArtifactResponse } from "@/features/planning/types";

export type DoneGateDecision =
  | { kind: "ready" }
  | { kind: "missing-result" }
  | { kind: "unvalidated-result"; resultArtifactId: string };

export type DoneGateScenario = Exclude<DoneGateDecision["kind"], "ready"> | "refused";

const DONE_GATE_REFUSAL_CODES = ["FeatureResultRequired", "FeatureDoneTapRequired"];

export function decideDoneGate(artifacts: readonly PlanningArtifactResponse[]): DoneGateDecision {
  const result = artifacts.find((artifact) => artifact.kind === "result");
  if (result === undefined) {
    return { kind: "missing-result" };
  }
  if (result.status !== "validated") {
    return { kind: "unvalidated-result", resultArtifactId: result.id };
  }
  return { kind: "ready" };
}

export function doneGateRefusal(error: unknown): string | null {
  if (!(error instanceof Error)) {
    return null;
  }
  const code = (error as ApiError).code;
  return typeof code === "string" && DONE_GATE_REFUSAL_CODES.includes(code) ? error.message : null;
}

export function featureResultTabHref(projectId: string, slug: string): string {
  return `/dashboard?project=${projectId}&feature=${slug}&tab=result`;
}

export function doneGateDialogTitle(scenario: DoneGateScenario): string {
  if (scenario === "missing-result") {
    return "Done needs a result";
  }
  if (scenario === "unvalidated-result") {
    return "Approve & done";
  }
  return "Done was refused";
}

export function missingResultCopy(title: string): string {
  return `"${title}" has no result artifact yet. The done flip stays gated until the outcome is recorded.`;
}

export function unvalidatedResultCopy(title: string): string {
  return `The result artifact of "${title}" is still draft. Approve & done validates it and flips the feature to done in one go.`;
}
