"use client";

import { useState, type FormEvent } from "react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Field } from "@/shared/ui/field";
import { Input } from "@/shared/ui/input";
import { Select } from "@/shared/ui/select";
import { Textarea } from "@/shared/ui/textarea";
import { TestVerdicts } from "@/features/settings/components/test-verdicts";
import type { VectorSearchHealth } from "@/shared/api/types";
import type {
  NotificationNudgeMode,
  PlanningMaterializationDefault,
  PlanningValidationDefault,
  SettingsFormValues,
  SettingsResponse,
  TestSettingsValues,
} from "@/features/settings/types";

const PROVIDERS = ["onnx", "ollama", "openai"] as const;
const MATERIALIZATION_DEFAULTS = ["off", "on", "archive"] as const satisfies readonly PlanningMaterializationDefault[];
const VALIDATION_DEFAULTS = ["on", "off"] as const satisfies readonly PlanningValidationDefault[];
const NUDGE_MODES = ["quiet", "nag", "mini"] as const satisfies readonly NotificationNudgeMode[];

interface SettingsFormProps {
  settings: SettingsResponse;
  pending: boolean;
  error: Error | null;
  onSubmit: (values: SettingsFormValues) => void;
  onTest: (values: TestSettingsValues) => void;
  testPending: boolean;
  testReport: VectorSearchHealth | null;
  testError: Error | null;
}

export function SettingsForm({ settings, pending, error, onSubmit, onTest, testPending, testReport, testError }: SettingsFormProps) {
  const [embeddingProvider, setEmbeddingProvider] = useState(settings.embeddingProvider);
  const [ollamaBaseUrl, setOllamaBaseUrl] = useState(settings.ollamaBaseUrl);
  const [ollamaModel, setOllamaModel] = useState(settings.ollamaModel);
  const [openAiBaseUrl, setOpenAiBaseUrl] = useState(settings.openAiBaseUrl);
  const [openAiApiKey, setOpenAiApiKey] = useState("");
  const [qdrantEndpoint, setQdrantEndpoint] = useState(settings.qdrantEndpoint);
  const [planningMaterializationDefault, setPlanningMaterializationDefault] = useState(
    settings.planningMaterializationDefault,
  );
  const [planningRequireHumanValidationDefault, setPlanningRequireHumanValidationDefault] = useState(
    settings.planningRequireHumanValidationDefault,
  );
  const [planningAgentNamingRulesDefault, setPlanningAgentNamingRulesDefault] = useState(
    settings.planningAgentNamingRulesDefault,
  );
  const [notificationNudgeMode, setNotificationNudgeMode] = useState(settings.notificationNudgeMode);
  const [notificationNudgeIntervalMinutes, setNotificationNudgeIntervalMinutes] = useState(
    String(settings.notificationNudgeIntervalMinutes),
  );
  const [intervalError, setIntervalError] = useState<Error | null>(null);
  const [syncedSettings, setSyncedSettings] = useState(settings);

  if (syncedSettings !== settings) {
    setSyncedSettings(settings);
    setEmbeddingProvider(settings.embeddingProvider);
    setOllamaBaseUrl(settings.ollamaBaseUrl);
    setOllamaModel(settings.ollamaModel);
    setOpenAiBaseUrl(settings.openAiBaseUrl);
    setOpenAiApiKey("");
    setQdrantEndpoint(settings.qdrantEndpoint);
    setPlanningMaterializationDefault(settings.planningMaterializationDefault);
    setPlanningRequireHumanValidationDefault(settings.planningRequireHumanValidationDefault);
    setPlanningAgentNamingRulesDefault(settings.planningAgentNamingRulesDefault);
    setNotificationNudgeMode(settings.notificationNudgeMode);
    setNotificationNudgeIntervalMinutes(String(settings.notificationNudgeIntervalMinutes));
    setIntervalError(null);
  }

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    const minutes = Number(notificationNudgeIntervalMinutes);
    if (!Number.isSafeInteger(minutes) || minutes <= 0) {
      setIntervalError(new Error("Nag interval must be a whole number of minutes greater than zero."));
      return;
    }
    onSubmit({
      embeddingProvider,
      ollamaBaseUrl: ollamaBaseUrl.trim(),
      ollamaModel: ollamaModel.trim(),
      openAiBaseUrl: openAiBaseUrl.trim(),
      openAiApiKey: openAiApiKey.trim(),
      qdrantEndpoint: qdrantEndpoint.trim(),
      planningMaterializationDefault,
      planningRequireHumanValidationDefault,
      planningAgentNamingRulesDefault: planningAgentNamingRulesDefault.trim(),
      notificationNudgeMode,
      notificationNudgeIntervalMinutes: minutes,
    });
  };

  const handleTest = () => {
    onTest({
      provider: embeddingProvider,
      ollamaBaseUrl: ollamaBaseUrl.trim(),
      ollamaModel: ollamaModel.trim(),
      openAiBaseUrl: openAiBaseUrl.trim(),
      qdrantEndpoint: qdrantEndpoint.trim(),
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
        <>
          <Field label="Ollama base URL" htmlFor="ollama-url">
            <Input
              id="ollama-url"
              value={ollamaBaseUrl}
              onChange={(event) => setOllamaBaseUrl(event.target.value)}
              placeholder="http://127.0.0.1:11434"
            />
          </Field>
          <Field label="Ollama embedding model" htmlFor="ollama-model">
            <Input
              id="ollama-model"
              value={ollamaModel}
              onChange={(event) => setOllamaModel(event.target.value)}
              placeholder="nomic-embed-text"
            />
          </Field>
        </>
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
        <h3 className="text-sm font-semibold text-foreground">Vector search</h3>
        <Field label="Qdrant endpoint" htmlFor="qdrant-endpoint">
          <Input
            id="qdrant-endpoint"
            value={qdrantEndpoint}
            onChange={(event) => setQdrantEndpoint(event.target.value)}
            placeholder="http://127.0.0.1:6334"
          />
        </Field>
        <Button type="button" variant="secondary" onClick={handleTest} disabled={testPending}>
          {testPending ? "Testing…" : "Test connection"}
        </Button>
        {testReport ? <TestVerdicts report={testReport} /> : null}
        <ErrorText error={testError} />
      </div>

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
        <Field label="Agent naming rules default" htmlFor="planning-agent-naming-rules-default">
          <Textarea
            id="planning-agent-naming-rules-default"
            rows={5}
            maxLength={2000}
            value={planningAgentNamingRulesDefault}
            onChange={(event) => setPlanningAgentNamingRulesDefault(event.target.value)}
            placeholder="Aliases look like Dan, Rex. One agent, one claim at a time…"
          />
        </Field>
        <p className="text-xs text-muted">{planningAgentNamingRulesDefault.length}/2000</p>
      </div>

      <div className="space-y-3 border-t border-border pt-4">
        <h3 className="text-sm font-semibold text-foreground">Notifications</h3>
        <p className="text-xs text-muted">
          Nudges remind you about work waiting on your approval. Quiet stays silent, mini shows a
          one-time hint, nag repeats on the interval below.
        </p>
        <Field label="Nudge mode" htmlFor="notification-nudge-mode">
          <Select
            id="notification-nudge-mode"
            value={notificationNudgeMode}
            onChange={(event) => {
              const mode = event.target.value as NotificationNudgeMode;
              setNotificationNudgeMode(mode);
              setIntervalError(null);
              if (mode !== "nag") {
                const minutes = Number(notificationNudgeIntervalMinutes);
                if (!Number.isSafeInteger(minutes) || minutes <= 0) {
                  setNotificationNudgeIntervalMinutes(String(settings.notificationNudgeIntervalMinutes));
                }
              }
            }}
            className="w-full"
          >
            {NUDGE_MODES.map((mode) => (
              <option key={mode} value={mode}>
                {mode}
              </option>
            ))}
          </Select>
        </Field>
        {notificationNudgeMode === "nag" ? (
          <Field label="Nag interval (minutes)" htmlFor="notification-nudge-interval-minutes">
            <Input
              id="notification-nudge-interval-minutes"
              type="number"
              min={1}
              step={1}
              value={notificationNudgeIntervalMinutes}
              onChange={(event) => {
                setNotificationNudgeIntervalMinutes(event.target.value);
                setIntervalError(null);
              }}
            />
          </Field>
        ) : null}
      </div>

      <ErrorText error={intervalError ?? error} />
      <Button type="submit" disabled={pending}>
        {pending ? "Saving…" : "Save settings"}
      </Button>
    </form>
  );
}
