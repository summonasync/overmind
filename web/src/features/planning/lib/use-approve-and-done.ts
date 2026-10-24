import { useMutation, useQueryClient } from "@tanstack/react-query";
import { updatePlanningFeature, validatePlanningArtifact } from "@/features/planning/api";
import { planningKeys } from "@/features/planning/queries";

export function useApproveAndDone(projectId: string, slug: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (resultArtifactId: string) => {
      await validatePlanningArtifact(resultArtifactId);
      await updatePlanningFeature(projectId, slug, { status: "done" });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: planningKeys.lists() });
      queryClient.invalidateQueries({ queryKey: planningKeys.feature(projectId, slug) });
    },
  });
}
