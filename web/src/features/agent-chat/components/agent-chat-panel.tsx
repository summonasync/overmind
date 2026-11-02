"use client";

import { useState, type ReactNode } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useSearchParams, useRouter } from "next/navigation";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { PageHeader } from "@/shared/ui/page-header";
import { deleteAgentConversation } from "@/features/agent-chat/api";
import { AgentComposer } from "@/features/agent-chat/components/agent-composer";
import { AgentConversationList } from "@/features/agent-chat/components/agent-conversation-list";
import { AgentMessageList } from "@/features/agent-chat/components/agent-message-list";
import { AgentProjectSelect } from "@/features/agent-chat/components/agent-project-select";
import { agentChatKeys, agentConversationsQueryOptions } from "@/features/agent-chat/queries";
import { useAgentChat } from "@/features/agent-chat/lib/use-agent-chat";

const PANEL_DESCRIPTION =
  "Talk to the embedded Overmind agent. It answers from the configured LLM provider and can drive planning, entries and wave coordination on your instruction.";

interface AgentChatPanelProps {
  hint?: ReactNode;
}

export function AgentChatPanel({ hint }: AgentChatPanelProps) {
  const searchParams = useSearchParams();
  const router = useRouter();
  const queryClient = useQueryClient();
  const projectParam = searchParams?.get("project") ?? "";
  const projectContext = projectParam.length > 0 ? projectParam : null;
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [listError, setListError] = useState<Error | null>(null);

  const conversations = useQuery(agentConversationsQueryOptions(projectContext));
  const chat = useAgentChat({
    conversationId: selectedId,
    projectId: projectContext,
    onConversationCreated: (conversation) => setSelectedId(conversation.id),
  });

  const changeProject = (value: string) => {
    setListError(null);
    setSelectedId(null);
    router.replace(value.length > 0 ? `/agent-chat?project=${encodeURIComponent(value)}` : "/agent-chat");
  };

  const removeConversation = async (conversationId: string) => {
    setListError(null);
    try {
      await deleteAgentConversation(conversationId);
      if (selectedId === conversationId) {
        setSelectedId(null);
      }
      queryClient.invalidateQueries({ queryKey: agentChatKeys.conversations(projectContext) });
    } catch (error) {
      setListError(error instanceof Error ? error : new Error(String(error)));
    }
  };

  return (
    <div>
      <PageHeader title="Agent chat" description={PANEL_DESCRIPTION} />
      {hint}
      <ErrorText error={conversations.error ?? chat.historyError ?? listError} />
      <div className="mb-4">
        <AgentProjectSelect value={projectParam} onChange={changeProject} />
      </div>
      {conversations.isPending ? (
        <LoadingState label="Loading conversations…" />
      ) : (
        <div className="grid items-start gap-4 lg:grid-cols-[260px_minmax(0,1fr)]">
          <AgentConversationList
            conversations={conversations.data?.items ?? []}
            activeId={selectedId}
            onOpen={setSelectedId}
            onDelete={removeConversation}
          />
          <section aria-label="Conversation" className="flex min-w-0 flex-col gap-3">
            <AgentMessageList
              messages={chat.messages}
              streaming={chat.streaming}
              historyLoading={selectedId !== null && chat.historyLoading}
              canLoadOlder={chat.canLoadOlder}
              loadingOlder={chat.loadingOlder}
              onLoadOlder={chat.loadOlder}
              scrollKey={selectedId ?? "new"}
            />
            <AgentComposer
              streaming={chat.streaming}
              error={chat.sendError}
              onSend={chat.send}
              onStop={chat.stop}
            />
          </section>
        </div>
      )}
    </div>
  );
}
