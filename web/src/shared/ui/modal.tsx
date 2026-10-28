"use client";

import { useEffect, useState, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { X } from "lucide-react";
import { Button } from "@/shared/ui/button";

interface ModalProps {
  title: string;
  open: boolean;
  onClose: () => void;
  children: ReactNode;
  size?: "md" | "xl";
}

export function Modal({ title, open, onClose, children, size = "md" }: ModalProps) {
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    const frame = requestAnimationFrame(() => setMounted(true));
    return () => cancelAnimationFrame(frame);
  }, []);

  useEffect(() => {
    if (!open) {
      return;
    }
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        onClose();
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [open, onClose]);

  if (!open || !mounted) {
    return null;
  }

  return createPortal(
    <div
      className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/60 p-4"
      onClick={onClose}
      role="presentation"
    >
      <div
        className={
          size === "xl"
            ? "mt-[4vh] flex h-[88vh] w-[94vw] max-w-none flex-col rounded-lg border border-border-strong bg-surface-raised p-5 shadow-xl"
            : "mt-16 w-full max-w-lg rounded-lg border border-border-strong bg-surface-raised p-5 shadow-xl"
        }
        onClick={(event) => event.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-label={title}
      >
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-base font-semibold text-foreground">{title}</h2>
          <Button variant="ghost" onClick={onClose} aria-label="Close">
            <X size={16} aria-hidden />
          </Button>
        </div>
        <div className={size === "xl" ? "min-h-0 flex-1 overflow-y-auto" : undefined}>{children}</div>
      </div>
    </div>,
    document.body,
  );
}
