"use client";

import { useState, type FormEvent, type KeyboardEvent } from "react";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Textarea } from "@/shared/ui/textarea";
import { AGENT_MESSAGE_MAX_LENGTH } from "@/features/agent-chat/lib/limits";

interface AgentComposerProps {
  streaming: boolean;
  error: Error | null;
  onSend: (text: string) => void;
  onStop: () => void;
}

export function AgentComposer({ streaming, error, onSend, onStop }: AgentComposerProps) {
  const [draft, setDraft] = useState("");
  const [rejection, setRejection] = useState<string | null>(null);

  const canSend = draft.trim().length > 0 && !streaming;

  const submitDraft = () => {
    if (draft.trim().length === 0) {
      setRejection("Write a message before sending.");
      return;
    }
    setRejection(null);
    onSend(draft);
    setDraft("");
  };

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (streaming) {
      return;
    }
    submitDraft();
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      if (!streaming) {
        submitDraft();
      }
    }
  };

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-3">
      <Textarea
        value={draft}
        onChange={(event) => setDraft(event.target.value)}
        onKeyDown={handleKeyDown}
        placeholder="Instruct the agent — it drives Overmind through the same doors as coding agents"
        aria-label="Message the agent"
        rows={3}
        maxLength={AGENT_MESSAGE_MAX_LENGTH}
      />
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-xs text-muted">Enter to send · Shift+Enter for a new line</span>
        {streaming ? (
          <Button type="button" variant="secondary" onClick={onStop} className="ml-auto">
            Stop
          </Button>
        ) : null}
        <Button type="submit" disabled={!canSend} className={streaming ? "" : "ml-auto"}>
          Send
        </Button>
      </div>
      {rejection !== null ? (
        <p className="text-xs text-danger" role="alert">
          {rejection}
        </p>
      ) : null}
      <ErrorText error={error} />
    </form>
  );
}
