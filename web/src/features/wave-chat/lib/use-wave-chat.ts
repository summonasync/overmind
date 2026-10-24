"use client";

import { useMemo } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { postWaveMessage } from "@/features/wave-chat/api";
import { sortRooms } from "@/features/wave-chat/lib/room-order";
import { waveChatKeys, waveRoomsQueryOptions } from "@/features/wave-chat/queries";

export function useWaveRooms(projectId: string) {
  const query = useQuery(waveRoomsQueryOptions(projectId));
  const rooms = useMemo(() => (query.data ? sortRooms(query.data.items) : []), [query.data]);
  return { rooms, loading: query.isPending, error: query.error };
}

export function useWaveMessageMutations(projectId: string) {
  const queryClient = useQueryClient();

  const post = useMutation({
    mutationFn: (input: { room: string; authorAlias: string; text: string }) =>
      postWaveMessage(projectId, input.room, input.authorAlias, input.text),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: waveChatKeys.all });
    },
  });

  return { post };
}
