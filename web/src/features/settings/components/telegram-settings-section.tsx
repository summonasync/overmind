"use client";

import { useState, type FormEvent } from "react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Field } from "@/shared/ui/field";
import { Input } from "@/shared/ui/input";
import { Textarea } from "@/shared/ui/textarea";
import { useSettingsMutations } from "@/features/settings/lib/use-settings";
import type { SettingsResponse, TelegramSettingsValues } from "@/features/settings/types";

interface TelegramSettingsSectionProps {
  settings: SettingsResponse;
}

export function TelegramSettingsSection({ settings }: TelegramSettingsSectionProps) {
  const { telegramSave } = useSettingsMutations();
  const [telegramEnabled, setTelegramEnabled] = useState(settings.telegramEnabled);
  const [telegramBotToken, setTelegramBotToken] = useState("");
  const [allowlistText, setAllowlistText] = useState(
    settings.telegramAllowedChatIds.map((id) => String(id)).join(", "),
  );
  const [parseError, setParseError] = useState<Error | null>(null);
  const [syncedSettings, setSyncedSettings] = useState(settings);

  if (syncedSettings !== settings) {
    setSyncedSettings(settings);
    setTelegramEnabled(settings.telegramEnabled);
    setTelegramBotToken("");
    setAllowlistText(settings.telegramAllowedChatIds.map((id) => String(id)).join(", "));
    setParseError(null);
  }

  const parseAllowlist = (raw: string): number[] | null => {
    const tokens = raw
      .split(/[,\n]/)
      .map((token) => token.trim())
      .filter((token) => token.length > 0);
    const ids: number[] = [];
    for (const token of tokens) {
      if (!/^\d{1,15}$/.test(token)) {
        setParseError(new Error(`'${token}' is not a Telegram chat id — ids are plain positive numbers.`));
        return null;
      }

      const id = Number(token);
      if (!ids.includes(id)) {
        ids.push(id);
      }
    }

    return ids;
  };

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    const ids = parseAllowlist(allowlistText);
    if (ids === null) {
      return;
    }

    setParseError(null);
    const values: TelegramSettingsValues = {
      telegramEnabled,
      telegramBotToken: telegramBotToken.trim(),
      telegramAllowedChatIds: ids,
    };
    telegramSave.mutate(values);
  };

  return (
    <section id="telegram" className="scroll-mt-20 rounded-lg border border-border bg-surface p-5">
      <h2 className="mb-1 text-base font-semibold text-foreground">Telegram</h2>
      <p className="mb-4 text-xs text-muted">
        Long-polling bot transport: gate notifications land in your Telegram chat and your replies
        count as owner proofs (approve/done from the phone). Messages from chats outside the
        allowlist are silently ignored.
      </p>
      <form onSubmit={handleSubmit} className="max-w-xl space-y-4">
        <label className="flex items-start gap-3">
          <input
            type="checkbox"
            checked={telegramEnabled}
            onChange={(event) => setTelegramEnabled(event.target.checked)}
            className="mt-1 h-4 w-4"
          />
          <span>
            <span className="block text-sm text-foreground">Telegram connector enabled</span>
            <span className="block text-xs text-muted">
              Polling runs while enabled and a bot token is set.
            </span>
          </span>
        </label>

        <Field label="Bot token" htmlFor="telegram-bot-token">
          <Input
            id="telegram-bot-token"
            type="password"
            value={telegramBotToken}
            onChange={(event) => setTelegramBotToken(event.target.value)}
            placeholder={settings.telegramBotTokenConfigured ? "Configured — enter to replace" : "Enter token from @BotFather"}
            autoComplete="off"
          />
        </Field>
        {settings.telegramBotTokenConfigured ? (
          <Badge tone="success">Bot token configured</Badge>
        ) : (
          <Badge tone="warning">No bot token configured</Badge>
        )}

        <Field label="Allowed chat ids" htmlFor="telegram-allowlist">
          <Textarea
            id="telegram-allowlist"
            rows={3}
            value={allowlistText}
            onChange={(event) => setAllowlistText(event.target.value)}
            placeholder="123456789, 987654321"
          />
        </Field>
        <p className="text-xs text-muted">
          Comma- or newline-separated. Only these chats can talk to the bot. To find your chat id,
          message the bot once and open{" "}
          <code className="rounded bg-muted px-1 py-0.5">https://api.telegram.org/bot&lt;token&gt;/getUpdates</code>{" "}
          — the id sits under result.chat.id. An empty allowlist answers nobody.
        </p>

        <ErrorText error={parseError ?? (telegramSave.error as Error | null)} />
        {telegramSave.isSuccess ? <p className="text-xs text-success">Telegram settings saved.</p> : null}
        <Button type="submit" disabled={telegramSave.isPending}>
          {telegramSave.isPending ? "Saving…" : "Save Telegram settings"}
        </Button>
      </form>
    </section>
  );
}
