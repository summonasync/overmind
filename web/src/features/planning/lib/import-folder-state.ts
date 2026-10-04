import type { BadgeTone } from "@/shared/ui/badge";
import type {
  PlanningFeatureImportReport,
  PlanningImportScanEntry,
} from "@/features/planning/types";

export type ImportFolderState = "new" | "imported" | "error";

const STATE_LABELS: Record<ImportFolderState, string> = {
  new: "new",
  imported: "already imported",
  error: "error",
};

const STATE_TONES: Record<ImportFolderState, BadgeTone> = {
  new: "success",
  imported: "info",
  error: "danger",
};

export function importFolderState(entry: PlanningImportScanEntry): ImportFolderState {
  if (entry.error !== null) {
    return "error";
  }
  return entry.alreadyImported ? "imported" : "new";
}

export function importFolderStateLabel(state: ImportFolderState): string {
  return STATE_LABELS[state];
}

export function importFolderStateTone(state: ImportFolderState): BadgeTone {
  return STATE_TONES[state];
}

export function importConfirmText(
  entry: PlanningImportScanEntry,
  importedFolders?: ReadonlySet<string>,
): string | null {
  const reimport = importFolderState(entry) === "imported" || importedFolders?.has(entry.folder) === true;
  if (!reimport) {
    return null;
  }
  const feature = entry.slug ?? entry.folder;
  return `Folder "${entry.folder}" is already imported on the board as "${feature}". Importing again updates its artifacts from the backlog files.`;
}

export function importReportLine(report: PlanningFeatureImportReport): string {
  const verb = report.created ? "Imported" : "Re-imported";
  const counts = `${report.artifactsCreated} artifacts created, ${report.artifactsUpdated} updated`;
  return `${verb} ${report.slug} — ${counts}, ${report.trackedFiles} files tracked`;
}
