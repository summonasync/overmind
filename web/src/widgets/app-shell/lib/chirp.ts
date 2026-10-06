"use client";

let context: AudioContext | null = null;

function tone(
  ctx: AudioContext,
  at: number,
  from: number,
  to: number,
  duration: number,
): void {
  const osc = ctx.createOscillator();
  const gain = ctx.createGain();
  osc.type = "sine";
  osc.frequency.setValueAtTime(from, at);
  osc.frequency.exponentialRampToValueAtTime(to, at + duration);
  gain.gain.setValueAtTime(0.0001, at);
  gain.gain.exponentialRampToValueAtTime(0.12, at + 0.02);
  gain.gain.exponentialRampToValueAtTime(0.0001, at + duration);
  osc.connect(gain);
  gain.connect(ctx.destination);
  osc.start(at);
  osc.stop(at + duration + 0.02);
}

export function playChirp(): void {
  try {
    const Ctor =
      window.AudioContext ??
      (window as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (typeof Ctor === "undefined") {
      return;
    }
    context ??= new Ctor();
    if (context.state === "suspended") {
      void context.resume().catch(() => undefined);
    }
    const now = context.currentTime;
    tone(context, now, 880, 1180, 0.09);
    tone(context, now + 0.12, 1180, 1520, 0.11);
  } catch {
    return;
  }
}
