import type { ReactNode } from "react";
import { cn } from "@/shared/lib/cn";

interface FieldProps {
  label: string;
  htmlFor?: string;
  children: ReactNode;
  // fill turns the field into a growing flex column: the child (a textarea, usually)
  // can then own all leftover height. Without it the label is a plain block and the
  // child collapses to its own intrinsic height.
  fill?: boolean;
}

export function Field({ label, htmlFor, children, fill = false }: FieldProps) {
  return (
    <label
      htmlFor={htmlFor}
      className={cn(
        "text-sm font-medium text-foreground",
        fill ? "flex min-h-0 flex-1 flex-col" : "block",
      )}
    >
      {label}
      <span className={cn("mt-1 font-normal", fill ? "flex min-h-0 flex-1 flex-col" : "block")}>
        {children}
      </span>
    </label>
  );
}
