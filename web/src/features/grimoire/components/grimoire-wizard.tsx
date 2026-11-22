"use client";

import { useState, type FormEvent } from "react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Field } from "@/shared/ui/field";
import { Input } from "@/shared/ui/input";
import { LoadingState } from "@/shared/ui/loading-state";
import { Modal } from "@/shared/ui/modal";
import {
  GRIMOIRE_BOOK_NAME_MAX,
  detectionView,
  installArmHint,
  installPendingLabel,
  validateBookName,
  wiredView,
} from "@/features/grimoire/lib/grimoire-view";
import { useGrimoireMutations } from "@/features/grimoire/lib/use-grimoire-mutations";
import type {
  GrimoireAssistedInstallResponse,
  GrimoireInstallIntent,
  GrimoireStatusResponse,
} from "@/features/grimoire/types";
import { GrimoireAgentOutcomes } from "./grimoire-agent-outcomes";
import { GrimoireInstructions } from "./grimoire-instructions";

type WizardStep =
  | { name: "status" }
  | { name: "choice" }
  | { name: "instructions"; intent: "manual" | "assisted" }
  | { name: "wire"; mode: "initial" | "reconfigure" }
  | { name: "wireResult"; mode: "initial" | "reconfigure" };

interface GrimoireWizardProps {
  status: GrimoireStatusResponse;
  onClose: () => void;
}

export function GrimoireWizard({ status, onClose }: GrimoireWizardProps) {
  const [step, setStep] = useState<WizardStep>({ name: "status" });
  const [bookName, setBookName] = useState(status.bookName);
  const [overridePath, setOverridePath] = useState(status.overridePath);
  const { wire, assisted } = useGrimoireMutations();

  const bookError = validateBookName(bookName);

  const openWireStep = (mode: "initial" | "reconfigure") => {
    wire.reset();
    setStep({ name: "wire", mode });
  };

  const openInstructionsStep = (intent: GrimoireInstallIntent) => {
    assisted.reset();
    assisted.mutate({ intent, channel: null });
    setStep({ name: "instructions", intent });
  };

  function handleWire(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (bookError !== null || wire.isPending) {
      return;
    }
    const mode = step.name === "wire" ? step.mode : "initial";
    wire.mutate(
      {
        bookName: bookName.trim(),
        overridePath: overridePath.trim(),
      },
      { onSuccess: () => setStep({ name: "wireResult", mode }) },
    );
  }

  return (
    <Modal title="Grimoire setup" open onClose={onClose}>
      {step.name === "status" ? (
        <StatusStep
          status={status}
          onClose={onClose}
          onChoose={() => setStep({ name: "choice" })}
          onWire={() => openWireStep("initial")}
          onReconfigure={() => openWireStep("reconfigure")}
        />
      ) : null}

      {step.name === "choice" ? (
        <div className="space-y-3">
          <p className="text-sm text-muted">
            Grimoire was not found on this machine. Pick how you want to get it — or skip and decide later.
          </p>
          <ChoiceButton
            title="Install it yourself"
            hint={installArmHint("manual")}
            onClick={() => openInstructionsStep("manual")}
          />
          <ChoiceButton
            title="Let Overmind install it"
            hint={installArmHint("assisted")}
            onClick={() => openInstructionsStep("assisted")}
          />
          <ChoiceButton
            title="Skip for now"
            hint="No config changes. You can come back to this any time from Settings."
            onClick={onClose}
          />
        </div>
      ) : null}

      {step.name === "instructions" ? (
        <InstructionsStep
          intent={step.intent}
          pending={assisted.isPending}
          error={assisted.error as Error | null}
          result={assisted.data ?? null}
          onBack={() => setStep({ name: "choice" })}
          onRetry={() => assisted.mutate({ intent: step.intent, channel: null })}
          onContinue={() => openWireStep("initial")}
          onClose={onClose}
        />
      ) : null}

      {step.name === "wire" ? (
        <form onSubmit={handleWire} className="space-y-3">
          <Field label="Memory book name" htmlFor="grimoire-book">
            <Input
              id="grimoire-book"
              value={bookName}
              onChange={(event) => setBookName(event.target.value)}
              placeholder="e.g. johan-crew"
              maxLength={GRIMOIRE_BOOK_NAME_MAX}
            />
          </Field>
          {bookError !== null ? (
            <p className="text-xs text-danger" role="alert">
              {bookError}
            </p>
          ) : null}
          <Field label="Binary override path (optional)" htmlFor="grimoire-override">
            <Input
              id="grimoire-override"
              value={overridePath}
              onChange={(event) => setOverridePath(event.target.value)}
              placeholder="/usr/local/bin/grimoire"
            />
          </Field>
          <p className="text-xs text-muted">
            One shared book keyed to you, not to individual agents. Wiring registers
            &lsquo;grimoire mcp&rsquo; for every detected agent and leaves a backup of each config.
          </p>
          <ErrorText error={wire.error as Error | null} />
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setStep({ name: "status" })}>
              Back
            </Button>
            <Button type="submit" disabled={bookError !== null || wire.isPending}>
              {wire.isPending ? "Wiring…" : step.mode === "reconfigure" ? "Save and rewire" : "Wire agents"}
            </Button>
          </div>
        </form>
      ) : null}

      {step.name === "wireResult" ? (
        <div className="space-y-3">
          {wire.data === undefined ? null : wire.data.success ? (
            <>
              <p className="text-sm text-success" role="status">
                Grimoire is wired for your agents.
              </p>
              <GrimoireAgentOutcomes agents={wire.data.agents} />
            </>
          ) : (
            <>
              <p
                className="rounded-md border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-danger"
                role="alert"
              >
                {wire.data.error ?? "Wiring did not complete."}
              </p>
              <GrimoireAgentOutcomes agents={wire.data.agents} />
            </>
          )}
          <div className="flex justify-end gap-2">
            {wire.data?.success ? (
              <Button onClick={onClose}>Done</Button>
            ) : (
              <>
                <Button variant="secondary" onClick={onClose}>
                  Close
                </Button>
                <Button onClick={() => openWireStep(step.mode)}>Back to wiring</Button>
              </>
            )}
          </div>
        </div>
      ) : null}
    </Modal>
  );
}

interface StatusStepProps {
  status: GrimoireStatusResponse;
  onClose: () => void;
  onChoose: () => void;
  onWire: () => void;
  onReconfigure: () => void;
}

function StatusStep({ status, onClose, onChoose, onWire, onReconfigure }: StatusStepProps) {
  const detection = detectionView(status);
  const wired = wiredView(status.wired);

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-2">
        <Badge tone={detection.tone}>{detection.label}</Badge>
        <Badge tone={wired.tone}>{wired.label}</Badge>
        {detection.detail ? <span className="text-xs text-muted">{detection.detail}</span> : null}
      </div>
      <p className="text-sm text-muted">
        {status.wired
          ? `Your agents write into the shared memory book${status.bookName ? ` "${status.bookName}"` : ""}. Reconfigure the book or the binary path any time.`
          : "Overmind owns work knowledge; Grimoire owns persona, communication style and relational memory. Wire it to give your agents the personal layer."}
      </p>
      <div className="flex justify-end gap-2">
        <Button variant="secondary" onClick={onClose}>
          {status.wired ? "Close" : "Not now"}
        </Button>
        {status.wired ? (
          <Button onClick={onReconfigure}>Reconfigure</Button>
        ) : status.status === "notInstalled" ? (
          <Button onClick={onChoose}>Set up Grimoire</Button>
        ) : (
          <Button onClick={onWire}>Wire agents</Button>
        )}
      </div>
    </div>
  );
}

interface ChoiceButtonProps {
  title: string;
  hint: string;
  onClick: () => void;
}

function ChoiceButton({ title, hint, onClick }: ChoiceButtonProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="w-full rounded-md border border-border bg-surface px-3 py-2.5 text-left transition-colors hover:border-border-strong hover:bg-surface-raised focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
    >
      <span className="block text-sm font-medium text-foreground">{title}</span>
      <span className="mt-0.5 block text-xs text-muted">{hint}</span>
    </button>
  );
}

interface InstructionsStepProps {
  intent: "manual" | "assisted";
  pending: boolean;
  error: Error | null;
  result: GrimoireAssistedInstallResponse | null;
  onBack: () => void;
  onRetry: () => void;
  onContinue: () => void;
  onClose: () => void;
}

function InstructionsStep({
  intent,
  pending,
  error,
  result,
  onBack,
  onRetry,
  onContinue,
  onClose,
}: InstructionsStepProps) {
  if (pending) {
    return <LoadingState label={installPendingLabel(intent)} />;
  }

  if (error !== null || result === null) {
    return (
      <div className="space-y-3">
        <ErrorText error={error} />
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={onBack}>
            Back
          </Button>
          <Button onClick={onRetry}>Try again</Button>
        </div>
      </div>
    );
  }

  if (result.success) {
    return (
      <div className="space-y-3">
        <p className="text-sm text-success" role="status">
          Grimoire is installed and detectable now.
        </p>
        {result.binaryPath ? <p className="text-xs text-muted">{result.binaryPath}</p> : null}
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={onClose}>
            Close
          </Button>
          <Button onClick={onContinue}>Wire agents</Button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      {result.error ? (
        <p
          className="rounded-md border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-danger"
          role="alert"
        >
          {result.error}
        </p>
      ) : null}
      <p className="text-sm text-foreground">
        {result.degraded
          ? intent === "manual"
            ? "Follow Grimoire's own install steps, then come back and wire your agents."
            : "No install channel is configured, so Overmind cannot run the install for you yet. These steps get you there by hand."
          : "The install channel did not finish cleanly. These steps get you there by hand."}
      </p>
      <GrimoireInstructions steps={result.instructions} />
      <div className="flex justify-end gap-2">
        <Button variant="secondary" onClick={onBack}>
          Back
        </Button>
        <Button onClick={onContinue}>I installed it — wire now</Button>
      </div>
    </div>
  );
}
