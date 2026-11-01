import { Suspense } from "react";
import { LoadingState } from "@/shared/ui/loading-state";
import { WaveChatPanel } from "@/features/wave-chat";
import { MarshalHint } from "@/widgets/marshal-hint";

export function WaveChatPage() {
  return (
    <Suspense fallback={<LoadingState label="Loading…" />}>
      <WaveChatPanel hint={<MarshalHint />} />
    </Suspense>
  );
}
