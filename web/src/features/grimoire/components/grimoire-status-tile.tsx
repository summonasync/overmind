"use client";

import { useState } from "react";
import { ExternalLink } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { detectionView, grimoireUiUrl, wiredView } from "@/features/grimoire/lib/grimoire-view";
import { useGrimoireMutations } from "@/features/grimoire/lib/use-grimoire-mutations";
import { useGrimoireStatus } from "@/features/grimoire/lib/use-grimoire";
import type { GrimoireStatusResponse } from "@/features/grimoire/types";
import { GrimoireWizard } from "./grimoire-wizard";

export function GrimoireStatusTile() {
  const { data: status, loading, error } = useGrimoireStatus();
  const { disconnect } = useGrimoireMutations();
  const [wizardOpen, setWizardOpen] = useState(false);
  const [disconnectFailure, setDisconnectFailure] = useState<string | null>(null);

  const handleDisconnect = () => {
    setDisconnectFailure(null);
    disconnect.mutate(undefined, {
      onSuccess: (result) => {
        if (!result.success && result.error !== null) {
          setDisconnectFailure(result.error);
        }
      },
    });
  };

  return (
    <section className="rounded-lg border border-border bg-surface p-5">
      <h2 className="mb-1 text-base font-semibold text-foreground">Grimoire memory</h2>
      <p className="mb-4 text-xs text-muted">
        Personal-layer memory lives in Grimoire: persona, communication style and relational notes.
        Detect it, wire &lsquo;grimoire mcp&rsquo; into your agents, or disconnect — Overmind keeps
        work knowledge on its own board.
      </p>

      {loading ? (
        <LoadingState label="Checking for Grimoire…" />
      ) : error !== null || status === null ? (
        <div className="space-y-2">
          <ErrorText error={error as Error | null} />
          <p className="text-xs text-muted">
            Detection failed — make sure the Overmind host is healthy and try again later.
          </p>
        </div>
      ) : (
        <GrimoireTileBody
          status={status}
          wizardOpen={wizardOpen}
          disconnectPending={disconnect.isPending}
          disconnectError={disconnect.error as Error | null}
          disconnectFailure={disconnectFailure}
          onWizardOpen={() => {
            setDisconnectFailure(null);
            setWizardOpen(true);
          }}
          onWizardClose={() => setWizardOpen(false)}
          onDisconnect={handleDisconnect}
        />
      )}
    </section>
  );
}

interface GrimoireTileBodyProps {
  status: GrimoireStatusResponse;
  wizardOpen: boolean;
  disconnectPending: boolean;
  disconnectError: Error | null;
  disconnectFailure: string | null;
  onWizardOpen: () => void;
  onWizardClose: () => void;
  onDisconnect: () => void;
}

function GrimoireTileBody({
  status,
  wizardOpen,
  disconnectPending,
  disconnectError,
  disconnectFailure,
  onWizardOpen,
  onWizardClose,
  onDisconnect,
}: GrimoireTileBodyProps) {
  const detection = detectionView(status);
  const wired = wiredView(status.wired);
  const uiUrl = grimoireUiUrl(status);

  return (
    <div>
      <div className="flex flex-wrap items-center gap-2">
        <Badge tone={detection.tone}>{detection.label}</Badge>
        <Badge tone={wired.tone}>{wired.label}</Badge>
        {detection.detail ? <span className="text-xs text-muted">{detection.detail}</span> : null}
      </div>

      <dl className="mt-3 space-y-1 text-sm">
        <div className="flex gap-2">
          <dt className="text-muted">Memory book</dt>
          <dd className="text-foreground">{status.bookName.length > 0 ? status.bookName : "Not set"}</dd>
        </div>
        {status.binaryPath ? (
          <div className="flex gap-2">
            <dt className="text-muted">Binary</dt>
            <dd className="truncate text-foreground" title={status.binaryPath}>
              {status.binaryPath}
            </dd>
          </div>
        ) : null}
        {status.overridePath.length > 0 ? (
          <div className="flex gap-2">
            <dt className="text-muted">Override path</dt>
            <dd className="truncate text-foreground">{status.overridePath}</dd>
          </div>
        ) : null}
      </dl>

      <div className="mt-4 flex flex-wrap items-center gap-2">
        {status.wired ? (
          <>
            <Button onClick={onWizardOpen}>Reconfigure</Button>
            <Button variant="danger" disabled={disconnectPending} onClick={onDisconnect}>
              {disconnectPending ? "Disconnecting…" : "Disconnect"}
            </Button>
          </>
        ) : (
          <Button onClick={onWizardOpen}>
            {status.status === "notInstalled" ? "Set up Grimoire" : "Wire Grimoire"}
          </Button>
        )}
        {uiUrl !== null ? (
          <a
            href={uiUrl}
            target="_blank"
            rel="noreferrer"
            className="inline-flex items-center justify-center gap-1.5 rounded-md border border-border bg-surface px-3 py-1.5 text-sm font-medium text-foreground transition-colors hover:border-border-strong hover:bg-surface-raised focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 focus-visible:ring-offset-2 focus-visible:ring-offset-background"
          >
            <ExternalLink size={14} aria-hidden />
            Open Grimoire UI
          </a>
        ) : null}
      </div>

      {disconnectFailure !== null ? (
        <p
          className="mt-3 rounded-md border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-danger"
          role="alert"
        >
          {disconnectFailure}
        </p>
      ) : (
        <ErrorText error={disconnectError} />
      )}

      {wizardOpen ? <GrimoireWizard status={status} onClose={onWizardClose} /> : null}
    </div>
  );
}
