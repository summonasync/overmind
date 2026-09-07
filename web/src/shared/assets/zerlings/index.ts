import angry from "./angry.png";
import celebrating from "./celebrating.png";
import confused from "./confused.png";
import love from "./love.png";
import menacing from "./menacing.png";
import silly from "./silly.png";
import sleeping from "./sleeping.png";
import smug from "./smug.png";
import surprised from "./surprised.png";
import yelling from "./yelling.png";

export type ZerlingPose =
  | "angry"
  | "celebrating"
  | "confused"
  | "love"
  | "menacing"
  | "silly"
  | "sleeping"
  | "smug"
  | "surprised"
  | "yelling";

export const ZERLING_SPRITES: Record<ZerlingPose, string> = {
  angry: angry.src,
  celebrating: celebrating.src,
  confused: confused.src,
  love: love.src,
  menacing: menacing.src,
  silly: silly.src,
  sleeping: sleeping.src,
  smug: smug.src,
  surprised: surprised.src,
  yelling: yelling.src,
};
