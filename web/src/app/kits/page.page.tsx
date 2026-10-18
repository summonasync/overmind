"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";

export default function KitsRedirectRoute() {
  const router = useRouter();

  useEffect(() => {
    router.replace("/settings/kits");
  }, [router]);

  return null;
}
