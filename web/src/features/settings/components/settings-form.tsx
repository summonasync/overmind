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
  AgentDelegateApprovalsDefault,
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
const DELEGATE_APPROVALS_DEFAULTS = ["on", "off"] as const satisfies readonly AgentDelegateApprovalsDefault[];

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
  const [agentBaseUrl, setAgentBaseUrl] = useState(settings.agentBaseUrl);
  const [agentModel, setAgentModel] = useState(settings.agentModel);
  const [agentApiKey, setAgentApiKey] = useState("");
  const [agentPerTurnMaxTokens, setAgentPerTurnMaxTokens] = useState(String(settings.agentPerTurnMaxTokens));
  const [agentDailyBudget, setAgentDailyBudget] = useState(String(settings.agentDailyBudget));
  const [agentInstructions, setAgentInstructions] = useState(settings.agentInstructions);
  const [agentDelegateApprovalsDefault, setAgentDelegateApprovalsDefault] = useState(
    settings.agentDelegateApprovalsDefault,
  );
  const [agentError, setAgentError] = useState<Error | null>(null);
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
    setAgentBaseUrl(settings.agentBaseUrl);
    setAgentModel(settings.agentModel);
    setAgentApiKey("");
    setAgentPerTurnMaxTokens(String(settings.agentPerTurnMaxTokens));
    setAgentDailyBudget(String(settings.agentDailyBudget));
    setAgentInstructions(settings.agentInstructions);
    setAgentDelegateApprovalsDefault(settings.agentDelegateApprovalsDefault);
    setAgentError(null);
  }

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    const minutes = Number(notificationNudgeIntervalMinutes);
    if (!Number.isSafeInteger(minutes) || minutes <= 0) {
      setIntervalError(new Error("Nag interval must be a whole number of minutes greater than zero."));
      return;
    }
    const perTurnMaxTokens = Number(agentPerTurnMaxTokens);
    if (!Number.isSafeInteger(perTurnMaxTokens) || perTurnMaxTokens <= 0) {
      setAgentError(new Error("Per-turn max tokens must be a whole number greater than zero."));
      return;
    }
    const dailyBudget = Number(agentDailyBudget);
    if (!Number.isSafeInteger(dailyBudget) || dailyBudget < 0) {
      setAgentError(new Error("Daily budget must be a whole number of zero or greater."));
      return;
    }
    setAgentError(null);
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
      agentBaseUrl: agentBaseUrl.trim(),
      agentModel: agentModel.trim(),
      agentApiKey: agentApiKey.trim(),
      agentPerTurnMaxTokens: perTurnMaxTokens,
      agentDailyBudget: dailyBudget,
      agentInstructions: agentInstructions.trim(),
      agentDelegateApprovalsDefault,
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

      <div className="space-y-3 border-t border-border pt-4">
        <h3 className="text-sm font-semibold text-foreground">Agent</h3>
        <p className="text-xs text-muted">
          OpenAI-compatible endpoint for the embedded management agent that chats on the Agent chat
          page. The API key is stored server-side and never rendered back.
        </p>
        <Field label="Base URL" htmlFor="agent-base-url">
          <Input
            id="agent-base-url"
            value={agentBaseUrl}
            onChange={(event) => setAgentBaseUrl(event.target.value)}
            placeholder="https://open.bigmodel.cn/api/paas/v4"
          />
        </Field>
        <Field label="Model" htmlFor="agent-model">
          <Input
            id="agent-model"
            value={agentModel}
            onChange={(event) => setAgentModel(event.target.value)}
            placeholder="glm-4.6"
          />
        </Field>
        <Field label="API key" htmlFor="agent-api-key">
          <Input
            id="agent-api-key"
            type="password"
            value={agentApiKey}
            onChange={(event) => setAgentApiKey(event.target.value)}
            placeholder={settings.agentApiKeyConfigured ? "Configured — enter to replace" : "Enter API key"}
            autoComplete="off"
          />
        </Field>
        {settings.agentApiKeyConfigured ? (
          <Badge tone="success">API key configured</Badge>
        ) : (
          <Badge tone="warning">No API key configured</Badge>
        )}
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field label="Per-turn max tokens" htmlFor="agent-per-turn-max-tokens">
            <Input
              id="agent-per-turn-max-tokens"
              type="number"
              min={1}
              step={1}
              value={agentPerTurnMaxTokens}
              onChange={(event) => setAgentPerTurnMaxTokens(event.target.value)}
            />
          </Field>
          <Field label="Daily budget (tokens)" htmlFor="agent-daily-budget">
            <Input
              id="agent-daily-budget"
              type="number"
              min={0}
              step={1}
              value={agentDailyBudget}
              onChange={(event) => setAgentDailyBudget(event.target.value)}
            />
          </Field>
        </div>
        <p className="text-xs text-muted">A daily budget of 0 keeps the agent unrestricted.</p>
        <Field label="Delegate approvals default" htmlFor="agent-delegate-approvals-default">
          <Select
            id="agent-delegate-approvals-default"
            value={agentDelegateApprovalsDefault}
            onChange={(event) =>
              setAgentDelegateApprovalsDefault(event.target.value as AgentDelegateApprovalsDefault)
            }
            className="w-full"
          >
            {DELEGATE_APPROVALS_DEFAULTS.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </Select>
        </Field>
        <p className="text-xs text-muted">
          Projects set to Inherit follow this default: when on, the embedded agent may execute
          validate and done actions on the owner&apos;s explicit chat instruction. Override it per
          project in the project&apos;s agent approvals section.
        </p>
        <Field label="Instructions" htmlFor="agent-instructions">
          <Textarea
            id="agent-instructions"
            rows={5}
            maxLength={4000}
            value={agentInstructions}
            onChange={(event) => setAgentInstructions(event.target.value)}
            placeholder="Extra instructions appended to the agent's system prompt…"
          />
        </Field>
        <p className="text-xs text-muted">{agentInstructions.length}/4000 · Applied on the agent&apos;s next turn.</p>
      </div>

      <ErrorText error={intervalError ?? agentError ?? error} />
      <Button type="submit" disabled={pending}>
        {pending ? "Saving…" : "Save settings"}
      </Button>
    </form>
  );
}
