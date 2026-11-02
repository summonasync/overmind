"use client";

import { useState, type ReactNode } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { PageHeader } from "@/shared/ui/page-header";
import { AgentComposer } from "@/features/agent-chat/components/agent-composer";
import { AgentConversationList } from "@/features/agent-chat/components/agent-conversation-list";
import { AgentMessageList } from "@/features/agent-chat/components/agent-message-list";
import { AgentProjectSelect } from "@/features/agent-chat/components/agent-project-select";
import { useAgentChat } from "@/features/agent-chat/lib/use-agent-chat";
import { useAgentConversationList } from "@/features/agent-chat/lib/use-agent-conversation-list";

const PANEL_DESCRIPTION =
  "Talk to the embedded Overmind agent. It answers from the configured LLM provider and can drive planning, entries and wave coordination on your instruction.";

interface AgentChatPanelProps {
  hint?: ReactNode;
}

export function AgentChatPanel({ hint }: AgentChatPanelProps) {
  const searchParams = useSearchParams();
  const router = useRouter();
  const projectParam = searchParams?.get("project") ?? "";
  const projectContext = projectParam.length > 0 ? projectParam : null;
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const list = useAgentConversationList({
    projectId: projectContext,
    onRemoved: (removedId) => {
      if (selectedId === removedId) {
        setSelectedId(null);
      }
    },
  });
  const chat = useAgentChat({
    conversationId: selectedId,
    projectId: projectContext,
    onConversationCreated: (conversation) => setSelectedId(conversation.id),
  });

  const changeProject = (value: string) => {
    list.clearError();
    setSelectedId(null);
    router.replace(value.length > 0 ? `/agent-chat?project=${encodeURIComponent(value)}` : "/agent-chat");
  };

  return (
    <div>
      <PageHeader title="Agent chat" description={PANEL_DESCRIPTION} />
      {hint}
      <ErrorText error={list.error ?? chat.historyError} />
      <div className="mb-4">
        <AgentProjectSelect value={projectParam} onChange={changeProject} />
      </div>
      {list.isPending ? (
        <LoadingState label="Loading conversations…" />
      ) : (
        <div className="grid items-start gap-4 lg:grid-cols-[260px_minmax(0,1fr)]">
          <AgentConversationList
            conversations={list.conversations}
            activeId={selectedId}
            onOpen={setSelectedId}
            onDelete={list.remove}
          />
          <section aria-label="Conversation" className="flex min-w-0 flex-col gap-3">
            <AgentMessageList
              conversationId={selectedId}
              messages={chat.messages}
              streaming={chat.streaming}
              historyLoading={selectedId !== null && chat.historyLoading}
              canLoadOlder={chat.canLoadOlder}
              loadingOlder={chat.loadingOlder}
              onLoadOlder={chat.loadOlder}
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
