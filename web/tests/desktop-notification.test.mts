import { strict as assert } from "node:assert";
import { test } from "node:test";
import {
  desktopNotificationsSupported,
  desktopPermission,
  requestDesktopPermission,
  showDesktopNotification,
} from "../src/widgets/app-shell/lib/desktop-notification.ts";

interface ToastOptions {
  body?: string;
  tag?: string;
  icon?: string;
  silent?: boolean;
}

class FakeNotification {
  static permission: "granted" | "denied" | "default" = "granted";
  static requestResult: "granted" | "denied" | "default" = "granted";
  static failRequest = false;
  static failConstruction = false;
  static created: { title: string; options: ToastOptions; toast: FakeNotification }[] = [];

  title: string;
  options: ToastOptions;
  onclick: (() => void) | null = null;
  closed = false;

  constructor(title: string, options: ToastOptions) {
    if (FakeNotification.failConstruction) {
      throw new Error("toast engine exploded");
    }
    this.title = title;
    this.options = options;
    const record = { title, options, toast: this };
    FakeNotification.created.push(record);
  }

  close(): void {
    this.closed = true;
  }

  static async requestPermission(): Promise<"granted" | "denied" | "default"> {
    if (FakeNotification.failRequest) {
      throw new Error("prompt crashed");
    }
    return FakeNotification.requestResult;
  }

  static reset(): void {
    FakeNotification.permission = "granted";
    FakeNotification.requestResult = "granted";
    FakeNotification.failRequest = false;
    FakeNotification.failConstruction = false;
    FakeNotification.created = [];
  }
}

function install(): void {
  FakeNotification.reset();
  Object.assign(globalThis, { window: {}, Notification: FakeNotification });
}

function uninstall(): void {
  Reflect.deleteProperty(globalThis, "window");
  Reflect.deleteProperty(globalThis, "Notification");
}

test("without browser globals every surface reports unsupported", async () => {
  assert.equal(desktopNotificationsSupported(), false);
  assert.equal(desktopPermission(), "unsupported");
  assert.equal(showDesktopNotification({ title: "t", body: "b", tag: "n", icon: null, onClick: () => {} }), false);
  assert.equal(await requestDesktopPermission(), "unsupported");
});

test("granted permission fires a silent toast that focuses and closes on click", (t) => {
  install();
  t.after(uninstall);

  let clicked = false;
  const shown = showDesktopNotification({
    title: "Mindlash Studio",
    body: "095-attention · coder\nSpec needs your validation",
    tag: "n1",
    icon: "/icon.png",
    onClick: () => {
      clicked = true;
    },
  });

  assert.equal(shown, true);
  assert.equal(FakeNotification.created.length, 1);
  const { title, options, toast } = FakeNotification.created[0];
  assert.equal(title, "Mindlash Studio");
  assert.equal(options.body, "095-attention · coder\nSpec needs your validation");
  assert.equal(options.tag, "n1");
  assert.equal(options.silent, true);
  assert.equal(options.icon, "/icon.png");

  assert.notEqual(toast.onclick, null);
  toast.onclick?.();
  assert.equal(clicked, true);
  assert.equal(toast.closed, true);
});

test("a null icon is omitted from the toast options", (t) => {
  install();
  t.after(uninstall);

  showDesktopNotification({ title: "t", body: "b", tag: "n", icon: null, onClick: () => {} });
  assert.ok(!("icon" in FakeNotification.created[0].options));
});

test("denied permission mutes the toast without touching the constructor", (t) => {
  install();
  t.after(uninstall);
  FakeNotification.permission = "denied";

  assert.equal(desktopPermission(), "denied");
  assert.equal(
    showDesktopNotification({ title: "t", body: "b", tag: "n", icon: null, onClick: () => {} }),
    false,
  );
  assert.equal(FakeNotification.created.length, 0);
});

test("a crashing toast engine degrades to a silent false", (t) => {
  install();
  t.after(uninstall);
  FakeNotification.failConstruction = true;

  assert.equal(
    showDesktopNotification({ title: "t", body: "b", tag: "n", icon: null, onClick: () => {} }),
    false,
  );
});

test("permission request resolves with the browser answer", async (t) => {
  install();
  t.after(uninstall);

  FakeNotification.requestResult = "granted";
  assert.equal(await requestDesktopPermission(), "granted");

  FakeNotification.requestResult = "denied";
  assert.equal(await requestDesktopPermission(), "denied");

  FakeNotification.failRequest = true;
  FakeNotification.permission = "default";
  assert.equal(await requestDesktopPermission(), "denied");
});
