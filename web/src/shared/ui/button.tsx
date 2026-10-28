import type { ButtonHTMLAttributes } from "react";
import { cn } from "@/shared/lib/cn";

type Variant = "primary" | "accent" | "secondary" | "danger" | "ghost";

const variantClasses: Record<Variant, string> = {
  primary: "bg-primary text-primary-foreground hover:bg-primary-hover",
  accent: "border border-primary/40 bg-transparent text-primary hover:bg-primary/10 hover:border-primary/60",
  secondary: "border border-border bg-surface text-foreground hover:bg-surface-raised hover:border-border-strong",
  danger: "border border-danger/40 bg-transparent text-danger hover:bg-danger/10",
  ghost: "text-muted hover:bg-surface-raised hover:text-foreground",
};

const SIZE_CLASSES = {
  xs: "px-2 py-0.5 text-xs",
  sm: "px-2 py-1 text-xs",
  md: "px-3 py-1.5 text-sm",
} as const;

const focusRing = "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 focus-visible:ring-offset-2 focus-visible:ring-offset-background";

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  size?: keyof typeof SIZE_CLASSES;
}

export function Button({ variant = "primary", size = "md", className, type = "button", ...rest }: ButtonProps) {
  return (
    <button
      type={type}
      className={cn(
        "inline-flex items-center justify-center gap-1.5 rounded-md",
        SIZE_CLASSES[size],
        "font-medium transition-colors",
        "disabled:cursor-not-allowed disabled:opacity-50",
        focusRing,
        variantClasses[variant],
        className,
      )}
      {...rest}
    />
  );
}
