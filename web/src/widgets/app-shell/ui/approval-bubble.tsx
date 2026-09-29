"use client";

import { useState } from "react";
import { Check, X } from "lucide-react";
import { APPROVAL_REPLY, useNotificationMutations } from "@/features/notifications";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Modal } from "@/shared/ui/modal";
import { Zerling } from "@/shared/ui/zerling";
import { cn } from "@/shared/lib/cn";
import { notificationHref } from "@/widgets/app-shell/lib/notification-href";
import { useApprovalBubble } from "@/widgets/app-shell/lib/use-approval-bubble";
import { useAwaitingTitle } from "@/widgets/app-shell/lib/use-awaiting-title";
import { NotificationCenter } from "@/widgets/app-shell/ui/notification-center";

export function ApprovalBubble() {
  const { surface, item, moreCount, deadTarget, dismiss, expand, awaitingCount, centerKind } =
    useApprovalBubble();
  const { reply } = useNotificationMutations();
  const [centerOpen, setCenterOpen] = useState(false);

  useAwaitingTitle(awaitingCount);

  if (surface === "hidden" || item === null) {
    return null;
  }

  if (surface === "icon") {
    return (
      <button
        type="button"
        onClick={expand}
        aria-label="Expand approval nudge"
        className="fixed left-1/2 top-[56px] z-30 -translate-x-1/2 rounded-full border border-warning/40 bg-surface-raised p-1.5 shadow-lg transition-colors hover:border-warning focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 focus-visible:ring-offset-2 focus-visible:ring-offset-background"
      >
        <span className="absolute -left-1 -top-1 flex h-4 w-4 items-center justify-center rounded-full bg-warning text-[10px] font-bold leading-none text-background ring-2 ring-background">
          !
        </span>
        <Zerling pose="sleeping" size={56} />
      </button>
    );
  }

  const replying = reply.isPending && reply.variables?.id === item.id;

  return (
    <>
      <div className="pointer-events-none fixed left-1/2 top-[100px] z-30 -translate-x-1/2">
        <div
          role="status"
          className="pointer-events-auto w-72 animate-bubble-pop rounded-lg border border-warning/40 bg-surface-raised p-3 shadow-xl"
        >
          <div className="flex items-start gap-2.5">
            <Zerling pose="menacing" size={40} />
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-2">
                <span
                  className={cn("truncate text-xs font-medium text-foreground", deadTarget && "opacity-60")}
                >
                  {item.agent}
                </span>
                {deadTarget ? (
                  <span className="shrink-0 text-[10px] uppercase tracking-wide text-warning">
                    target gone
                  </span>
                ) : null}
                <button
                  type="button"
                  onClick={dismiss}
                  aria-label="Dismiss approval nudge"
                  className="ml-auto shrink-0 rounded p-0.5 text-muted transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
                >
                  <X size={14} aria-hidden />
                </button>
              </div>
              <p
                title={item.text}
                className={cn("mt-1.5 line-clamp-4 text-sm text-foreground", deadTarget && "opacity-60")}
              >
                {item.text}
              </p>
            </div>
          </div>
          {item.kind === "approval" ? (
            <div className="mt-2.5 flex items-center gap-2">
              <Button
                variant="secondary"
                className="shrink-0 px-2 py-1 text-xs"
                disabled={replying}
                onClick={() => reply.mutate({ id: item.id, text: APPROVAL_REPLY.approve })}
                aria-label={`Approve request from ${item.agent}`}
              >
                <Check size={12} className="text-success" aria-hidden />
                Approve
              </Button>
              <Button
                variant="danger"
                className="shrink-0 px-2 py-1 text-xs"
                disabled={replying}
                onClick={() => reply.mutate({ id: item.id, text: APPROVAL_REPLY.reject })}
                aria-label={`Reject request from ${item.agent}`}
                title={`Sends reply "rejected"; it does not block the agent's validation by itself`}
              >
                <X size={12} aria-hidden />
                Reject
              </Button>
              <ErrorText error={reply.error && reply.variables?.id === item.id ? reply.error : null} />
            </div>
          ) : null}
          <div className="mt-2 flex items-center gap-2">
            {item.featureSlug ? (
              <a href={notificationHref(item)} className="text-xs font-medium text-primary hover:underline">
                Open →
              </a>
            ) : null}
            {moreCount > 0 ? (
              <button
                type="button"
                onClick={() => setCenterOpen(true)}
                className="ml-auto inline-flex items-center rounded border border-warning/40 bg-warning/10 px-1.5 py-0.5 text-xs font-medium text-warning transition-colors hover:border-warning focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
              >
                {moreCount} more → {centerKind === "all" ? "open all" : "open approvals"}
              </button>
            ) : null}
          </div>
        </div>
      </div>
      <Modal title="Notifications" open={centerOpen} onClose={() => setCenterOpen(false)}>
        <NotificationCenter initialKind={centerKind} />
      </Modal>
    </>
  );
}
