"use client";

import { PageHeader } from "@/shared/ui/page-header";
import { NotificationCenter } from "@/widgets/app-shell";
import { useNotificationAnchor } from "@/pages/attention-page/lib/use-notification-anchor";
import { AwaitingSection } from "@/pages/attention-page/ui/awaiting-section";
import { CeremoniesSection } from "@/pages/attention-page/ui/ceremonies-section";

export function AttentionPage() {
  const highlightId = useNotificationAnchor();

  return (
    <div className="space-y-6">
      <PageHeader
        title="Attention"
        description="Everything waiting on you: unanswered pings, stuck ceremonies, and the full notification history."
      />
      <AwaitingSection highlightId={highlightId} />
      <CeremoniesSection />
      <section aria-labelledby="history-heading" className="space-y-3">
        <h2 id="history-heading" className="text-base font-semibold text-foreground">
          History
        </h2>
        <NotificationCenter highlightId={highlightId} />
      </section>
    </div>
  );
}
