import { ApiError } from "@/shared/api/client";

// Activation refusals arrive as Problem documents; the title carries the failure code
// (MandatoryGlobal) or "Not Found" (NotLinked — the detail there is a pair of GUIDs,
// useless to a human, so both arms get reframed copy).
export function kitActivationErrorCopy(error: Error | null): Error | null {
  if (!(error instanceof ApiError)) {
    return error;
  }

  if (error.code === "MandatoryGlobal") {
    return new Error(
      "This kit is mandatory-global: it lives in the agent home skill directories and cannot be activated for a single project.",
    );
  }

  if (error.status === 404) {
    return new Error("This kit is not linked to the selected project — activate it before deactivating.");
  }

  return error;
}
