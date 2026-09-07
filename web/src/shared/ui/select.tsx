import type { SelectHTMLAttributes } from "react";
import { cn } from "@/shared/lib/cn";

export function Select({ className, children, ...rest }: SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <select
      className={cn(
        "rounded-md border border-border bg-surface px-2.5 py-1.5 text-sm text-foreground",
        "hover:border-border-strong focus-visible:border-border-strong focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
        className,
      )}
      {...rest}
    >
      {children}
    </select>
  );
}
