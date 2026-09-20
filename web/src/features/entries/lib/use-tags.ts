"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { updateTagColor } from "@/features/entries/api";
import { entryKeys, entryTagsQueryOptions } from "@/features/entries/queries";
import type { TagResponse } from "@/features/entries/types";

export function useTagList() {
  const query = useQuery(entryTagsQueryOptions());
  return { data: query.data ?? [], isPending: query.isPending, error: query.error };
}

export function useTagColorUpdate() {
  const queryClient = useQueryClient();
  const tagsKey = entryKeys.tags();

  return useMutation({
    mutationFn: ({ id, color }: { id: string; color: string }) => updateTagColor(id, color),
    onMutate: async ({ id, color }) => {
      await queryClient.cancelQueries({ queryKey: tagsKey });
      const snapshot = queryClient.getQueryData<TagResponse[]>(tagsKey);
      queryClient.setQueryData<TagResponse[]>(tagsKey, (tags) =>
        tags === undefined
          ? tags
          : tags.map((tag) =>
              tag.id === id ? { ...tag, color: color.length > 0 ? color : null } : tag,
            ),
      );
      return { snapshot };
    },
    onError: (_error, _variables, context) => {
      if (context?.snapshot !== undefined) {
        queryClient.setQueryData(tagsKey, context.snapshot);
      }
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: tagsKey });
      queryClient.invalidateQueries({ queryKey: entryKeys.lists() });
    },
  });
}
