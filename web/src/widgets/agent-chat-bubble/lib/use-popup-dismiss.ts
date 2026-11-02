import { useEffect, type RefObject } from "react";

export function usePopupDismiss(
  open: boolean,
  surfaceRef: RefObject<HTMLElement | null>,
  onClose: () => void,
): void {
  useEffect(() => {
    if (!open) {
      return;
    }
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        onClose();
      }
    };
    const handlePointerDown = (event: PointerEvent) => {
      const surface = surfaceRef.current;
      if (surface !== null && event.target instanceof Node && !surface.contains(event.target)) {
        onClose();
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    document.addEventListener("pointerdown", handlePointerDown, true);
    return () => {
      window.removeEventListener("keydown", handleKeyDown);
      document.removeEventListener("pointerdown", handlePointerDown, true);
    };
  }, [open, onClose, surfaceRef]);
}
