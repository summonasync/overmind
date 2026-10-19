import type { BadgeTone } from "@/shared/ui/badge";
import type { GrimoireAgentOutcome, GrimoireStatusResponse } from "@/features/grimoire/types";

export const GRIMOIRE_BOOK_NAME_MAX = 100;

export interface GrimoireDetectionView {
  label: string;
  tone: BadgeTone;
  detail: string;
}

export type GrimoireWizardOffer = "manual" | "assisted" | "wire" | "reconfigure";

export function detectionView(status: GrimoireStatusResponse): GrimoireDetectionView {
  switch (status.status) {
    case "running":
      return { label: "Running", tone: "success", detail: runDetail(status) };
    case "installed":
      return { label: "Installed", tone: "info", detail: versionDetail(status.version) };
    case "notInstalled":
      return { label: "Not installed", tone: "neutral", detail: "" };
  }
}

export function wiredView(wired: boolean): { label: string; tone: BadgeTone } {
  return wired ? { label: "Wired", tone: "success" } : { label: "Not wired", tone: "warning" };
}

export function wizardOffers(status: GrimoireStatusResponse): GrimoireWizardOffer[] {
  if (status.wired) {
    return ["reconfigure"];
  }
  return status.status === "notInstalled" ? ["manual", "assisted"] : ["wire"];
}

export function grimoireUiUrl(status: GrimoireStatusResponse): string | null {
  return status.status === "running" && status.port !== null
    ? `http://127.0.0.1:${status.port}`
    : null;
}

export function agentOutcomeTone(outcome: GrimoireAgentOutcome): BadgeTone {
  if (outcome.error !== null) {
    return "danger";
  }
  return outcome.outcome === "skipped" ? "neutral" : "success";
}

export function showGrimoireBanner(status: GrimoireStatusResponse | null): boolean {
  return status !== null && !status.wired;
}

export function validateBookName(name: string): string | null {
  const trimmed = name.trim();
  if (trimmed.length === 0) {
    return "Book name is required — the memory book is keyed to its owner.";
  }
  if (trimmed.length > GRIMOIRE_BOOK_NAME_MAX) {
    return `Must be at most ${GRIMOIRE_BOOK_NAME_MAX} characters.`;
  }
  return null;
}

function runDetail(status: GrimoireStatusResponse): string {
  const parts = [
    versionDetail(status.version),
    status.port === null ? "" : `UI on port ${status.port}`,
  ].filter((part) => part.length > 0);
  return parts.join(" · ");
}

function versionDetail(version: string | null): string {
  return version === null ? "" : `version ${version}`;
}
