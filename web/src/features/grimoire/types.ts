export type GrimoireDetectionState = "notInstalled" | "installed" | "running";

export interface GrimoireAgentOutcome {
  agent: string;
  outcome: string;
  configPath: string | null;
  error: string | null;
}

export interface GrimoireStatusResponse {
  status: GrimoireDetectionState;
  binaryPath: string | null;
  version: string | null;
  port: number | null;
  bookName: string;
  overridePath: string;
  wired: boolean;
  installChannel: string;
}

export interface GrimoireWireInput {
  bookName: string;
  overridePath: string | null;
}

export interface GrimoireWireResponse {
  success: boolean;
  wired: boolean;
  agents: GrimoireAgentOutcome[];
  error: string | null;
}

export interface GrimoireAssistedInstallResponse {
  degraded: boolean;
  success: boolean;
  status: GrimoireDetectionState | null;
  binaryPath: string | null;
  instructions: string[];
  error: string | null;
}

export interface GrimoireDisconnectResponse {
  success: boolean;
  wired: boolean;
  error: string | null;
}
