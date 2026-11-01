"use client";

import { Suspense } from "react";
import { LoadingState } from "@/shared/ui/loading-state";
import { LaneBoard } from "@/features/lanes";
import { MarshalHint } from "@/widgets/marshal-hint";

export function LanesPage() {
  return (
    <div className="flex flex-col gap-3">
      <MarshalHint />
      <Suspense fallback={<LoadingState label="Loading…" />}>
        <LaneBoard />
      </Suspense>
    </div>
  );
}
