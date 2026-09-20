import { cn } from "@/shared/lib/cn";

interface WritePreviewToggleProps {
  showPreview: boolean;
  onShowPreviewChange: (showPreview: boolean) => void;
}

export function WritePreviewToggle({ showPreview, onShowPreviewChange }: WritePreviewToggleProps) {
  return (
    <div className="mb-1 flex items-center gap-1">
      <button
        type="button"
        onClick={() => onShowPreviewChange(false)}
        className={cn(
          "rounded-md px-2 py-0.5 text-xs font-medium focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 focus-visible:ring-offset-2 focus-visible:ring-offset-background",
          !showPreview ? "bg-surface-raised text-foreground" : "text-muted hover:bg-surface-raised hover:text-foreground",
        )}
      >
        Write
      </button>
      <button
        type="button"
        onClick={() => onShowPreviewChange(true)}
        className={cn(
          "rounded-md px-2 py-0.5 text-xs font-medium focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 focus-visible:ring-offset-2 focus-visible:ring-offset-background",
          showPreview ? "bg-surface-raised text-foreground" : "text-muted hover:bg-surface-raised hover:text-foreground",
        )}
      >
        Preview
      </button>
    </div>
  );
}
