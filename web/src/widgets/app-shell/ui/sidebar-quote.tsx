"use client";

import { useEffect, useState } from "react";

const QUOTES = [
  "Knowledge is power. And I intend to take it all. — Kerrigan",
  "Evolution through understanding. — Overmind",
  "Same data. Different minds. A higher intelligence. — Overmind",
] as const;

const ROTATE_INTERVAL_MS = 45_000;

export function SidebarQuote() {
  const [index, setIndex] = useState(0);

  useEffect(() => {
    const start = window.setTimeout(() => {
      setIndex(Math.floor(Math.random() * QUOTES.length));
    }, 0);
    const timer = window.setInterval(() => {
      setIndex((current) => (current + 1) % QUOTES.length);
    }, ROTATE_INTERVAL_MS);
    return () => {
      window.clearTimeout(start);
      window.clearInterval(timer);
    };
  }, []);

  return (
    <blockquote className="border-l-2 border-primary/40 pl-3 text-xs leading-relaxed text-muted">
      {QUOTES[index]}
    </blockquote>
  );
}
