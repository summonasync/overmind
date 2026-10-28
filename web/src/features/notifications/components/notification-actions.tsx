"use client";

import { useState } from "react";
import { Check, Send, X } from "lucide-react";
import { APPROVAL_REPLY, approvalChoice } from "@/features/notifications/lib/approval-reply";
import { useNotificationMutations } from "@/features/notifications/lib/use-notification-mutations";
import type { NotificationItem } from "@/features/notifications/types";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Input } from "@/shared/ui/input";
import { cn } from "@/shared/lib/cn";

interface NotificationActionsProps {
  item: NotificationItem;
}

export function NotificationActions({ item }: NotificationActionsProps) {
  const [draft, setDraft] = useState("");
  const { reply } = useNotificationMutations();

  const existingReply = typeof item.replyText === "string" ? item.replyText : null;
  const replied = existingReply !== null;
  const choice = approvalChoice(existingReply);
  const replyingThis = reply.isPending && reply.variables?.id === item.id;

  const sendReply = () => {
    const text = draft.trim();
    if (!text) {
      return;
    }
    reply.mutate({ id: item.id, text }, { onSuccess: () => setDraft("") });
  };

  // No server denial semantics: reject posts a regular reply, 077 proof search reads any reply — backend left unchanged by user decision.
  if (item.kind === "approval") {
    return (
      <div className="mt-2 flex items-center gap-2">
        <Button
          variant="secondary"
          size="sm"
          className="shrink-0"
          disabled={replied || replyingThis}
          onClick={() => reply.mutate({ id: item.id, text: APPROVAL_REPLY.approve })}
          aria-label={`Approve request from ${item.agent}`}
        >
          <Check size={12} className="text-success" aria-hidden />
          Approve
        </Button>
        <Button
          variant="danger"
          size="sm"
          className="shrink-0"
          disabled={replied || replyingThis}
          onClick={() => reply.mutate({ id: item.id, text: APPROVAL_REPLY.reject })}
          aria-label={`Reject request from ${item.agent}`}
          title={`Sends reply "rejected"; it does not block the agent's validation by itself`}
        >
          <X size={12} aria-hidden />
          Reject
        </Button>
        {replied ? (
          <span
            className={cn(
              "text-xs font-medium",
              choice === "approve" && "text-success",
              choice === "reject" && "text-danger",
              choice === null && "text-muted",
            )}
          >
            Reply: {existingReply}
          </span>
        ) : null}
        <ErrorText error={reply.error} />
      </div>
    );
  }

  if (item.kind === "question") {
    return (
      <form
        className="mt-2 flex items-center gap-2"
        onSubmit={(event) => {
          event.preventDefault();
          sendReply();
        }}
      >
        <Input
          value={existingReply ?? draft}
          onChange={(event) => setDraft(event.target.value)}
          placeholder={replied ? undefined : "Reply to the agent…"}
          disabled={replied || replyingThis}
          aria-label={`Reply to ${item.agent}`}
          className="text-xs"
        />
        <Button
          type="submit"
          variant="accent"
          size="sm"
          className="shrink-0"
          disabled={replied || replyingThis || draft.trim().length === 0}
        >
          <Send size={12} aria-hidden />
          Send
        </Button>
        <ErrorText error={reply.error} />
      </form>
    );
  }

  return null;
}
