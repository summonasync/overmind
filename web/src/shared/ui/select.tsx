import type { SelectHTMLAttributes } from "react";
import { cn } from "@/shared/lib/cn";

const SIZE_CLASSES = {
  sm: "px-1.5 py-0.5 text-xs",
  md: "px-2.5 py-1.5 text-sm",
} as const;

interface SelectProps extends Omit<SelectHTMLAttributes<HTMLSelectElement>, "size"> {
  size?: keyof typeof SIZE_CLASSES;
}

export function Select({ className, children, size = "md", ...rest }: SelectProps) {
  return (
    <select
      className={cn(
        "rounded-md border border-border bg-surface",
        SIZE_CLASSES[size],
        "text-foreground",
        "hover:border-border-strong focus-visible:border-border-strong focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
        className,
      )}
      {...rest}
    >
      {children}
    </select>
  );
}
