import { Suspense } from "react";
import { LoadingState } from "@/shared/ui/loading-state";
import { AttentionPage } from "@/pages/attention-page";

export default function AttentionRoute() {
  return (
    <Suspense fallback={<LoadingState label="Loading attention…" />}>
      <AttentionPage />
    </Suspense>
  );
}
