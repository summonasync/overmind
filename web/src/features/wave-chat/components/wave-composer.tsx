"use client";

import { useState, type FormEvent, type KeyboardEvent } from "react";
import type { UseMutationResult } from "@tanstack/react-query";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Input } from "@/shared/ui/input";
import { Textarea } from "@/shared/ui/textarea";
import { WAVE_TEXT_MAX_LENGTH, validateComposerText } from "@/features/wave-chat/lib/composer";
import { DEFAULT_OWNER_ALIAS } from "@/features/wave-chat/lib/owner-alias";
import type { WaveMessage } from "@/features/wave-chat/types";

interface WaveComposerProps {
  room: string;
  ownerAlias: string;
  onAliasChange: (alias: string) => void;
  post: UseMutationResult<WaveMessage, Error, { room: string; authorAlias: string; text: string }>;
}

export function WaveComposer({ room, ownerAlias, onAliasChange, post }: WaveComposerProps) {
  const [draft, setDraft] = useState("");
  const [rejection, setRejection] = useState<string | null>(null);

  const trimmedAlias = ownerAlias.trim();
  const canSend = trimmedAlias.length > 0 && !post.isPending;

  const submit = () => {
    const validation = validateComposerText(draft);
    if (!validation.valid) {
      setRejection(validation.reason);
      return;
    }
    setRejection(null);
    post.mutate(
      { room, authorAlias: trimmedAlias, text: validation.text },
      { onSuccess: () => setDraft("") },
    );
  };

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    submit();
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      submit();
    }
  };

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-3">
      <Textarea
        value={draft}
        onChange={(event) => setDraft(event.target.value)}
        onKeyDown={handleKeyDown}
        placeholder={`Signal ${room} — @alias to address an agent`}
        aria-label={`Message ${room}`}
        rows={3}
        maxLength={WAVE_TEXT_MAX_LENGTH}
      />
      <div className="flex flex-wrap items-center gap-2">
        <label className="flex items-center gap-1.5 text-xs text-muted">
          Posting as
          <Input
            value={ownerAlias}
            onChange={(event) => onAliasChange(event.target.value)}
            placeholder={DEFAULT_OWNER_ALIAS}
            aria-label="Owner alias"
            className="w-36"
          />
        </label>
        <Button type="submit" disabled={!canSend} className="ml-auto">
          Send
        </Button>
      </div>
      {rejection !== null ? (
        <p className="text-xs text-danger" role="alert">
          {rejection}
        </p>
      ) : null}
      <ErrorText error={post.error} />
    </form>
  );
}
