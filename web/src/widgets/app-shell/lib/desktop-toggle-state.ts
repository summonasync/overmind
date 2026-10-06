import type { DesktopPermission } from "@/widgets/app-shell/lib/attention-plan";

export interface DesktopToggleState {
  checked: boolean;
  disabled: boolean;
  hint: string;
  warning: boolean;
}

export function desktopToggleState(
  permission: DesktopPermission,
  enabled: boolean,
): DesktopToggleState {
  if (permission === "unsupported") {
    return {
      checked: false,
      disabled: true,
      hint: "This browser has no desktop notification support.",
      warning: false,
    };
  }
  if (permission === "denied") {
    return {
      checked: false,
      disabled: true,
      hint: "Blocked by the browser — allow notifications for this site in the browser settings.",
      warning: true,
    };
  }
  return {
    checked: enabled && permission === "granted",
    disabled: false,
    hint:
      permission === "granted"
        ? "Toasts fire for approvals and questions while the tab is hidden."
        : "Turning this on asks the browser for notification permission.",
    warning: false,
  };
}
