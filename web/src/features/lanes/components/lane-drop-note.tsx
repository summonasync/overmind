import { cn } from "@/shared/lib/cn";

interface LaneDropNoteProps {
  message: string;
  className?: string;
}

export function LaneDropNote({ message, className }: LaneDropNoteProps) {
  return (
    <p
      role="status"
      className={cn(
        "rounded-md border border-danger/40 bg-danger/10 px-2 py-1 text-xs text-danger",
        className,
      )}
    >
      {message}
    </p>
  );
}
