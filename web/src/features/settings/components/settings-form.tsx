"use client";

import { useState, type FormEvent } from "react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Field } from "@/shared/ui/field";
import { Input } from "@/shared/ui/input";
import { Select } from "@/shared/ui/select";
import type { SettingsFormValues, SettingsResponse } from "@/features/settings/types";

const PROVIDERS = ["onnx", "ollama", "openai"] as const;

interface SettingsFormProps {
  settings: SettingsResponse;
  pending: boolean;
  error: Error | null;
  onSubmit: (values: SettingsFormValues) => void;
}

export function SettingsForm({ settings, pending, error, onSubmit }: SettingsFormProps) {
  const [embeddingProvider, setEmbeddingProvider] = useState(settings.embeddingProvider);
  const [ollamaBaseUrl, setOllamaBaseUrl] = useState(settings.ollamaBaseUrl);
  const [openAiBaseUrl, setOpenAiBaseUrl] = useState(settings.openAiBaseUrl);
  const [openAiApiKey, setOpenAiApiKey] = useState("");
  const [syncedSettings, setSyncedSettings] = useState(settings);

  if (syncedSettings !== settings) {
    setSyncedSettings(settings);
    setEmbeddingProvider(settings.embeddingProvider);
    setOllamaBaseUrl(settings.ollamaBaseUrl);
    setOpenAiBaseUrl(settings.openAiBaseUrl);
    setOpenAiApiKey("");
  }

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    onSubmit({
      embeddingProvider,
      ollamaBaseUrl: ollamaBaseUrl.trim(),
      openAiBaseUrl: openAiBaseUrl.trim(),
      openAiApiKey: openAiApiKey.trim(),
    });
  };

  return (
    <form onSubmit={handleSubmit} className="max-w-xl space-y-4">
      <Field label="Embedding provider" htmlFor="embedding-provider">
        <Select
          id="embedding-provider"
          value={embeddingProvider}
          onChange={(event) => setEmbeddingProvider(event.target.value)}
          className="w-full"
        >
          {PROVIDERS.map((provider) => (
            <option key={provider} value={provider}>
              {provider}
            </option>
          ))}
        </Select>
      </Field>

      {embeddingProvider === "ollama" ? (
        <Field label="Ollama base URL" htmlFor="ollama-url">
          <Input
            id="ollama-url"
            value={ollamaBaseUrl}
            onChange={(event) => setOllamaBaseUrl(event.target.value)}
            placeholder="http://127.0.0.1:11434"
          />
        </Field>
      ) : null}

      {embeddingProvider === "openai" ? (
        <>
          <Field label="OpenAI base URL" htmlFor="openai-url">
            <Input
              id="openai-url"
              value={openAiBaseUrl}
              onChange={(event) => setOpenAiBaseUrl(event.target.value)}
              placeholder="https://api.openai.com/v1"
            />
          </Field>
          <Field label="OpenAI API key" htmlFor="openai-key">
            <Input
              id="openai-key"
              type="password"
              value={openAiApiKey}
              onChange={(event) => setOpenAiApiKey(event.target.value)}
              placeholder={settings.openAiApiKeyConfigured ? "Configured — enter to replace" : "Enter API key"}
              autoComplete="off"
            />
          </Field>
          {settings.openAiApiKeyConfigured ? (
            <Badge tone="success">API key configured</Badge>
          ) : (
            <Badge tone="warning">No API key configured</Badge>
          )}
        </>
      ) : null}

      <ErrorText error={error} />
      <Button type="submit" disabled={pending}>
        {pending ? "Saving…" : "Save settings"}
      </Button>
    </form>
  );
}
