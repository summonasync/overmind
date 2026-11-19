"use client";

import { useEffect, useState, type TextareaHTMLAttributes } from "react";
import { cn } from "@/shared/lib/cn";
import { MarkdownViewer } from "@/shared/ui/markdown-viewer";
import { Textarea } from "@/shared/ui/textarea";
import { WritePreviewToggle } from "@/shared/ui/write-preview-toggle";

const PREVIEW_DEBOUNCE_MS = 200;

// The field's own attributes, minus the two the editor owns: `value` is the controlled line it
// feeds from its caller, and `className` carries the write/preview visibility rules. A caller
// that passes `onChange` takes over the field's change wiring — it must call `onValueChange`
// itself, which is the point: an autocomplete overlay needs the keystroke and the caret, not
// just the resulting string.
export type MarkdownEditorBodyProps = Omit<
  TextareaHTMLAttributes<HTMLTextAreaElement>,
  "value" | "className"
>;

interface MarkdownEditorProps {
  value: string;
  onValueChange: (value: string) => void;
  placeholder?: string;
  className?: string;
  bodyProps?: MarkdownEditorBodyProps;
}

export function MarkdownEditor({
  value,
  onValueChange,
  placeholder,
  className,
  bodyProps,
}: MarkdownEditorProps) {
  const [showPreview, setShowPreview] = useState(false);
  const [preview, setPreview] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => setPreview(value), PREVIEW_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [value]);

  return (
    <div className={cn("flex flex-col", className)}>
      <div className="xl:hidden">
        <WritePreviewToggle showPreview={showPreview} onShowPreviewChange={setShowPreview} />
      </div>
      <div className="flex min-h-0 flex-1 flex-col gap-2 xl:flex-row">
        <Textarea
          rows={10}
          className={cn("min-h-0 flex-1 resize-none", showPreview ? "hidden xl:block" : "block")}
          value={value}
          onChange={(event) => onValueChange(event.target.value)}
          placeholder={placeholder}
          {...bodyProps}
        />
        <div
          className={cn(
            "min-h-0 min-w-0 flex-1 overflow-y-auto rounded-md border border-border bg-surface px-3 py-2",
            showPreview ? "block" : "hidden xl:block",
          )}
        >
          {preview.trim() ? (
            <MarkdownViewer content={preview} />
          ) : (
            <p className="text-sm text-muted">Nothing to preview.</p>
          )}
        </div>
      </div>
    </div>
  );
}
