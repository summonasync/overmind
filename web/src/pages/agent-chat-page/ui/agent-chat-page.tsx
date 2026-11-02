import { Suspense } from "react";
import { LoadingState } from "@/shared/ui/loading-state";
import { AgentChatPanel } from "@/features/agent-chat";
import { AgentSetupHint } from "@/pages/agent-chat-page/ui/agent-setup-hint";

export function AgentChatPage() {
  return (
    <Suspense fallback={<LoadingState label="Loading…" />}>
      <AgentChatPanel hint={<AgentSetupHint />} />
    </Suspense>
  );
}
