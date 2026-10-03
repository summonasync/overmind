"use client";

import { useState } from "react";
import { Check, FolderUp, Lock } from "lucide-react";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Select } from "@/shared/ui/select";
import { Textarea } from "@/shared/ui/textarea";
import { issueActionAvailability } from "@/features/issues/lib/issue-actions";
import type { IssueCard } from "@/features/issues/types";

export interface IssueActionHandlers {
  onTriage: (issueId: string) => void;
  onClose: (input: { issueId: string; reason: string }) => void;
  onPromote: (input: { issueId: string; featureSlug: string }) => void;
}

export interface IssueActionPending {
  triage: boolean;
  close: boolean;
  promote: boolean;
}

interface IssueActionsProps {
  issue: IssueCard;
  features: { slug: string; title: string }[];
  handlers: IssueActionHandlers;
  pending: IssueActionPending;
  error: Error | null;
}

export function IssueActions({ issue, features, handlers, pending, error }: IssueActionsProps) {
  const { canTriage, canClose, canPromote } = issueActionAvailability(issue.status);
  const [reason, setReason] = useState("");
  const [featureSlug, setFeatureSlug] = useState("");

  if (!canTriage && !canClose && !canPromote) {
    return null;
  }

  return (
    <div className="space-y-3">
      <ErrorText error={error} />
      <div className="flex flex-wrap items-center gap-2">
        {canTriage ? (
          <Button
            variant="accent"
            disabled={pending.triage}
            onClick={() => handlers.onTriage(issue.id)}
            aria-label="Mark issue as triaged"
          >
            <Check size={14} aria-hidden />
            {pending.triage ? "Triaging…" : "Triage"}
          </Button>
        ) : null}
        {canPromote ? (
          <div className="flex items-center gap-2">
            <Select
              aria-label="Target feature for promotion"
              value={featureSlug}
              onChange={(event) => setFeatureSlug(event.target.value)}
              disabled={features.length === 0}
            >
              <option value="">{features.length === 0 ? "No features on board" : "Pick a feature…"}</option>
              {features.map((feature) => (
                <option key={feature.slug} value={feature.slug}>
                  {feature.title}
                </option>
              ))}
            </Select>
            <Button
              variant="primary"
              disabled={pending.promote || featureSlug.length === 0}
              onClick={() =>
                handlers.onPromote({ issueId: issue.id, featureSlug })
              }
              aria-label="Promote issue to a feature task"
            >
              <FolderUp size={14} aria-hidden />
              {pending.promote ? "Promoting…" : "Promote"}
            </Button>
          </div>
        ) : null}
        {canClose ? (
          <Button
            variant="danger"
            disabled={pending.close}
            onClick={() => handlers.onClose({ issueId: issue.id, reason })}
            aria-label="Close issue"
          >
            <Lock size={14} aria-hidden />
            {pending.close ? "Closing…" : "Close"}
          </Button>
        ) : null}
      </div>
      {canClose ? (
        <Textarea
          value={reason}
          onChange={(event) => setReason(event.target.value)}
          placeholder="Closing reason — appended to the trail (optional)"
          aria-label="Closing reason"
          rows={2}
        />
      ) : null}
    </div>
  );
}
