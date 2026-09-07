import { Zerling, ZERLING_SIZES } from "@/shared/ui/zerling";

interface ErrorTextProps {
  error: Error | null;
}

export function ErrorText({ error }: ErrorTextProps) {
  if (!error) {
    return null;
  }

  return (
    <p
      className="flex items-center gap-2 rounded-md border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-danger"
      role="alert"
    >
      <Zerling pose="yelling" size={ZERLING_SIZES.banner} />
      <span>{error.message}</span>
    </p>
  );
}
