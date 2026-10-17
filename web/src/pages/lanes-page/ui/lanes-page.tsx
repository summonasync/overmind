"use client";

import { Suspense } from "react";
import { LoadingState } from "@/shared/ui/loading-state";
import { LaneBoard } from "@/features/lanes";

export function LanesPage() {
  return (
    <Suspense fallback={<LoadingState label="Loading…" />}>
      <LaneBoard />
    </Suspense>
  );
}
