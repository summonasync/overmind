import type { FeatureNumberStyle } from "@/shared/lib/feature-number";

export interface ProjectResponse {
  id: string;
  name: string;
  diskPath: string;
  description: string;
  manifest: string | null;
  numberStyle: FeatureNumberStyle;
  numberPrefix: string | null;
  isFavorite: boolean;
  autoActOnReplies: boolean;
  openFeatureCount: number;
  createdAt: string;
}

export interface ProjectFormValues {
  name: string;
  diskPath: string;
  description: string;
}

export type ProjectUpdateValues = ProjectFormValues & {
  manifest?: string | null;
  setManifest?: boolean;
  numberStyle?: FeatureNumberStyle;
  numberPrefix?: string;
};

export interface ProjectManifestImportResponse {
  sourceFile: string;
  manifest: string;
}

export interface ProjectAutoActResponse {
  enabled: boolean;
}

export interface ClaimSettingsResponse {
  claimStaleThresholdMinutes: number;
  agentNamingRules: string | null;
}

export type DelegateApprovalsOverride = "unset" | "on" | "off";

export const DELEGATE_APPROVALS_OVERRIDES: readonly DelegateApprovalsOverride[] = [
  "unset",
  "on",
  "off",
];

export interface ApprovalSettingsResponse {
  delegateApprovals: DelegateApprovalsOverride;
  effectiveDelegateApprovals: boolean;
}
