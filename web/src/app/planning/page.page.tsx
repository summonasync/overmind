"use client";

import { Suspense, useEffect } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { LoadingState } from "@/shared/ui/loading-state";

function PlanningRedirect() {
  const router = useRouter();
  const searchParams = useSearchParams();

  useEffect(() => {
    const query = searchParams?.toString() ?? "";
    router.replace(query.length > 0 ? `/dashboard?${query}` : "/dashboard");
  }, [router, searchParams]);

  return <LoadingState label="Redirecting…" />;
}

export default function PlanningRoute() {
  return (
    <Suspense fallback={<LoadingState label="Loading…" />}>
      <PlanningRedirect />
    </Suspense>
  );
}
