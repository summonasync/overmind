import type { ReactNode } from "react";

interface FieldProps {
  label: string;
  htmlFor?: string;
  children: ReactNode;
}

export function Field({ label, htmlFor, children }: FieldProps) {
  return (
    <label htmlFor={htmlFor} className="block text-sm font-medium text-foreground">
      {label}
      <span className="mt-1 block font-normal">{children}</span>
    </label>
  );
}
