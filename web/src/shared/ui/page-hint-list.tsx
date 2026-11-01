import { cn } from "@/shared/lib/cn";
import { PAGE_HINTS, pageHintId } from "@/shared/lib/page-hints";
import { Hint } from "@/shared/ui/hint";

interface PageHintListProps {
  path: string;
  className?: string;
}

export function PageHintList({ path, className }: PageHintListProps) {
  const hints = PAGE_HINTS[path];

  if (!hints?.length) {
    return null;
  }

  return (
    <div className={cn("flex flex-wrap items-center gap-2", className)}>
      {hints.map((hint, index) => (
        <Hint key={hint.title} id={pageHintId(path, index)} label={hint.title}>
          {hint.text}
        </Hint>
      ))}
    </div>
  );
}
