"use client";

import { useEffect, useState } from "react";
import kerrigan1 from "@/shared/assets/brand/kerrigan/kerrigan-1.webp";
import kerrigan2 from "@/shared/assets/brand/kerrigan/kerrigan-2.webp";
import kerrigan3 from "@/shared/assets/brand/kerrigan/kerrigan-3.webp";
import kerrigan4 from "@/shared/assets/brand/kerrigan/kerrigan-4.webp";
import kerrigan5 from "@/shared/assets/brand/kerrigan/kerrigan-5.webp";
import kerrigan6 from "@/shared/assets/brand/kerrigan/kerrigan-6.webp";
import kerrigan7 from "@/shared/assets/brand/kerrigan/kerrigan-7.webp";

const KERRIGAN_VARIANTS = [kerrigan1, kerrigan2, kerrigan3, kerrigan4, kerrigan5, kerrigan6, kerrigan7];

export function InfoPanelArt() {
  const [kerrigan, setKerrigan] = useState<string | null>(null);

  useEffect(() => {
    const frame = requestAnimationFrame(() => {
      setKerrigan(KERRIGAN_VARIANTS[Math.floor(Math.random() * KERRIGAN_VARIANTS.length)].src);
    });
    return () => cancelAnimationFrame(frame);
  }, []);

  if (kerrigan === null) {
    return null;
  }

  return (
    <img
      src={kerrigan}
      alt=""
      aria-hidden
      className="pointer-events-none absolute inset-y-0 right-0 z-0 h-full w-48 object-cover object-top opacity-15 [mask-image:linear-gradient(to_left,white_25%,var(--color-surface)_95%)] [mask-mode:luminance]"
    />
  );
}
