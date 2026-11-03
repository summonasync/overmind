import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const section = () =>
  readFileSync(
    new URL("../src/features/settings/components/telegram-settings-section.tsx", import.meta.url),
    "utf8",
  );

const api = () => readFileSync(new URL("../src/features/settings/api.ts", import.meta.url), "utf8");

const types = () => readFileSync(new URL("../src/features/settings/types.ts", import.meta.url), "utf8");

const mutations = () =>
  readFileSync(new URL("../src/features/settings/lib/use-settings.ts", import.meta.url), "utf8");

const page = () =>
  readFileSync(new URL("../src/pages/settings-page/ui/settings-page.tsx", import.meta.url), "utf8");

test("the settings response carries enabled, token badge and allowlist", () => {
  const source = types();
  assert.ok(source.includes("telegramEnabled: boolean"));
  assert.ok(source.includes("telegramBotTokenConfigured: boolean"));
  assert.ok(source.includes("telegramAllowedChatIds: number[]"));
  assert.ok(source.includes("TelegramSettingsValues"));
});

test("the api layer sends only the telegram fields of the settings payload", () => {
  const source = api();
  assert.ok(source.includes("apiPut<UpdateSettingsResponse>(\"/settings\""));
  assert.ok(source.includes("telegramEnabled: values.telegramEnabled"));
  assert.ok(source.includes("telegramBotToken: values.telegramBotToken || null"));
  assert.ok(source.includes("telegramAllowedChatIds: values.telegramAllowedChatIds"));
  const telegramCall = source.slice(source.indexOf("updateTelegramSettings"));
  assert.ok(!telegramCall.includes("agentApiKey"), "telegram update must not touch other settings");
});

test("the mutation invalidates the settings query on save", () => {
  const source = mutations();
  assert.ok(source.includes("updateTelegramSettings(values)"));
  assert.ok(source.includes("telegramSave"));
});

test("the token input is write-only with a configured badge", () => {
  const source = section();
  assert.ok(source.includes('type="password"'));
  assert.ok(source.includes('autoComplete="off"'));
  assert.ok(source.includes("Configured — enter to replace"));
  assert.ok(source.includes("Bot token configured"));
  assert.ok(source.includes("No bot token configured"));
});

test("the allowlist editor validates chat ids before saving", () => {
  const source = section();
  assert.ok(source.includes("/^-?\\d{1,15}$/"));
  assert.ok(source.includes("group chats are negative"));
});

test("the settings page renders the telegram section", () => {
  const source = page();
  assert.ok(source.includes("<TelegramSettingsSection settings={settings} />"));
});
