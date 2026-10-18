"use client";

import { useEffect, useState } from "react";
import kerrigan1 from "@/shared/assets/brand/kerrigan/kerrigan-1.webp";
import kerrigan2 from "@/shared/assets/brand/kerrigan/kerrigan-2.webp";
import kerrigan3 from "@/shared/assets/brand/kerrigan/kerrigan-3.webp";
import kerrigan4 from "@/shared/assets/brand/kerrigan/kerrigan-4.webp";
import kerrigan5 from "@/shared/assets/brand/kerrigan/kerrigan-5.webp";
import kerrigan6 from "@/shared/assets/brand/kerrigan/kerrigan-6.webp";
import kerrigan7 from "@/shared/assets/brand/kerrigan/kerrigan-7.webp";
import { SidebarQuote } from "@/widgets/app-shell/ui/sidebar-quote";

const KERRIGAN_VARIANTS = [kerrigan1, kerrigan2, kerrigan3, kerrigan4, kerrigan5, kerrigan6, kerrigan7];

export function InfoPanelFooter() {
  const [kerrigan, setKerrigan] = useState<string | null>(null);

  useEffect(() => {
    const frame = requestAnimationFrame(() => {
      setKerrigan(KERRIGAN_VARIANTS[Math.floor(Math.random() * KERRIGAN_VARIANTS.length)].src);
    });
    return () => cancelAnimationFrame(frame);
  }, []);

  return (
    <div className="relative mt-auto min-h-32 pt-6">
      {kerrigan !== null && (
        <img
          src={kerrigan}
          alt=""
          aria-hidden
          className="pointer-events-none absolute bottom-0 right-0 h-32 w-44 object-cover object-top opacity-35 [mask-image:linear-gradient(to_left,white_30%,var(--color-surface)_98%)] [mask-mode:luminance]"
        />
      )}
      <div className="relative">
        <SidebarQuote />
      </div>
    </div>
  );
}
