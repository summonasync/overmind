import { ApiError } from "@/shared/api/client";

// Activation refusals arrive as Problem documents: the title carries the failure code for
// MandatoryGlobal, but a plain "Not Found" for every 404 — and the two arms 404 for different
// reasons (activate loses the kit or project row, deactivate finds no link), while the host
// detail is a pair of GUIDs. So each arm reframes its own honest cause and the identifiers
// never reach the screen.
export function kitActivateErrorCopy(error: Error | null): Error | null {
  if (!(error instanceof ApiError)) {
    return error;
  }

  if (error.code === "MandatoryGlobal") {
    return new Error(
      "This kit is mandatory-global: it lives in the agent home skill directories and cannot be activated for a single project.",
    );
  }

  if (error.status === 404) {
    return new Error(
      "The kit or the selected project is no longer in the library — refresh the list and pick again.",
    );
  }

  return error;
}

export function kitDeactivateErrorCopy(error: Error | null): Error | null {
  if (!(error instanceof ApiError) || error.status !== 404) {
    return error;
  }

  return new Error(
    "This kit has no active link in the selected project, or the project itself is gone — there is nothing to deactivate.",
  );
}
