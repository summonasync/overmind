"use client";

import { useState, type FormEvent } from "react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Field } from "@/shared/ui/field";
import { Input } from "@/shared/ui/input";
import { Select } from "@/shared/ui/select";
import type {
  PlanningMaterializationDefault,
  PlanningValidationDefault,
  SettingsFormValues,
  SettingsResponse,
} from "@/features/settings/types";

const PROVIDERS = ["onnx", "ollama", "openai"] as const;
const MATERIALIZATION_DEFAULTS = ["off", "on", "archive"] as const satisfies readonly PlanningMaterializationDefault[];
const VALIDATION_DEFAULTS = ["on", "off"] as const satisfies readonly PlanningValidationDefault[];

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
  const [planningMaterializationDefault, setPlanningMaterializationDefault] = useState(
    settings.planningMaterializationDefault,
  );
  const [planningRequireHumanValidationDefault, setPlanningRequireHumanValidationDefault] = useState(
    settings.planningRequireHumanValidationDefault,
  );
  const [syncedSettings, setSyncedSettings] = useState(settings);

  if (syncedSettings !== settings) {
    setSyncedSettings(settings);
    setEmbeddingProvider(settings.embeddingProvider);
    setOllamaBaseUrl(settings.ollamaBaseUrl);
    setOpenAiBaseUrl(settings.openAiBaseUrl);
    setOpenAiApiKey("");
    setPlanningMaterializationDefault(settings.planningMaterializationDefault);
    setPlanningRequireHumanValidationDefault(settings.planningRequireHumanValidationDefault);
  }

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    onSubmit({
      embeddingProvider,
      ollamaBaseUrl: ollamaBaseUrl.trim(),
      openAiBaseUrl: openAiBaseUrl.trim(),
      openAiApiKey: openAiApiKey.trim(),
      planningMaterializationDefault,
      planningRequireHumanValidationDefault,
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

      <div className="space-y-3 border-t border-border pt-4">
        <h3 className="text-sm font-semibold text-foreground">Planning workflow</h3>
        <p className="text-xs text-muted">
          Projects on inherit or unset follow these defaults: materialization decides whether spec,
          plan and task files are written into the project folder; human validation forces agents to
          wait for an approval in this UI.
        </p>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field label="Materialization default" htmlFor="planning-materialization-default">
            <Select
              id="planning-materialization-default"
              value={planningMaterializationDefault}
              onChange={(event) => setPlanningMaterializationDefault(event.target.value as PlanningMaterializationDefault)}
              className="w-full"
            >
              {MATERIALIZATION_DEFAULTS.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Require human validation default" htmlFor="planning-validation-default">
            <Select
              id="planning-validation-default"
              value={planningRequireHumanValidationDefault}
              onChange={(event) =>
                setPlanningRequireHumanValidationDefault(event.target.value as PlanningValidationDefault)
              }
              className="w-full"
            >
              {VALIDATION_DEFAULTS.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </Select>
          </Field>
        </div>
      </div>

      <ErrorText error={error} />
      <Button type="submit" disabled={pending}>
        {pending ? "Saving…" : "Save settings"}
      </Button>
    </form>
  );
}
