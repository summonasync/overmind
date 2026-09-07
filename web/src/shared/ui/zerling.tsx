import type { ImgHTMLAttributes } from "react";
import { ZERLING_SPRITES, type ZerlingPose } from "@/shared/assets/zerlings";
import { cn } from "@/shared/lib/cn";

interface ZerlingProps extends ImgHTMLAttributes<HTMLImageElement> {
  pose: ZerlingPose;
  size?: number;
}

export const ZERLING_SIZES = {
  chip: 24,
  banner: 64,
  display: 96,
} as const;

export function Zerling({ pose, size = 32, className, ...rest }: ZerlingProps) {
  return (
    <img
      src={ZERLING_SPRITES[pose]}
      alt=""
      width={size}
      height={size}
      draggable={false}
      className={cn("shrink-0 select-none object-contain", className)}
      {...rest}
    />
  );
}
