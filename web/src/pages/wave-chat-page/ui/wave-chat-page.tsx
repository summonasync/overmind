import { Suspense } from "react";
import { LoadingState } from "@/shared/ui/loading-state";
import { WaveChatPanel } from "@/features/wave-chat";

export function WaveChatPage() {
  return (
    <Suspense fallback={<LoadingState label="Loading…" />}>
      <WaveChatPanel />
    </Suspense>
  );
}
