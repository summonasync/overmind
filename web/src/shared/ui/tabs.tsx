"use client";

import { useRef, type KeyboardEvent } from "react";
import { cn } from "@/shared/lib/cn";

export interface TabsItem {
  key: string;
  label: string;
  href: string | null;
  title?: string;
}

interface TabsProps {
  items: TabsItem[];
  activeKey: string;
  ariaLabel: string;
  className?: string;
}

export function Tabs({ items, activeKey, ariaLabel, className }: TabsProps) {
  const tabRefs = useRef(new Map<string, HTMLAnchorElement>());

  const enabledKeys = items.filter((item) => item.href !== null).map((item) => item.key);

  const handleKeyDown = (event: KeyboardEvent<HTMLAnchorElement>, key: string) => {
    const currentIndex = enabledKeys.indexOf(key);
    if (currentIndex === -1) {
      return;
    }

    let nextIndex: number | null = null;
    switch (event.key) {
      case "ArrowRight":
        nextIndex = (currentIndex + 1) % enabledKeys.length;
        break;
      case "ArrowLeft":
        nextIndex = (currentIndex - 1 + enabledKeys.length) % enabledKeys.length;
        break;
      case "Home":
        nextIndex = 0;
        break;
      case "End":
        nextIndex = enabledKeys.length - 1;
        break;
      default:
        return;
    }

    event.preventDefault();
    tabRefs.current.get(enabledKeys[nextIndex])?.focus();
  };

  return (
    <div role="tablist" aria-label={ariaLabel} className={cn("flex items-end gap-1 border-b border-border", className)}>
      {items.map((item) => {
        const active = item.key === activeKey;
        const disabled = item.href === null;
        return (
          <a
            key={item.key}
            role="tab"
            href={item.href ?? undefined}
            aria-selected={active}
            aria-disabled={disabled || undefined}
            tabIndex={active && !disabled ? 0 : -1}
            title={item.title}
            onClick={disabled ? (event) => event.preventDefault() : undefined}
            onKeyDown={(event) => handleKeyDown(event, item.key)}
            ref={(node) => {
              if (node !== null) {
                tabRefs.current.set(item.key, node);
              } else {
                tabRefs.current.delete(item.key);
              }
            }}
            className={cn(
              "-mb-px border-b-2 px-3 py-2 text-sm font-medium transition-colors",
              "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
              active
                ? "border-primary text-primary"
                : "border-transparent text-muted hover:text-foreground",
              disabled && "cursor-not-allowed opacity-50 hover:text-muted",
            )}
          >
            {item.label}
          </a>
        );
      })}
    </div>
  );
}
