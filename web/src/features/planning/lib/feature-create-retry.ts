import { ApiError } from "../../../shared/api/client.ts";
import { buildFeatureSlug, featureSlugPart } from "../../../shared/lib/feature-slug.ts";
import type { PlanningFeatureFormValues, PlanningFeatureResponse } from "../types";

const NEXT_FREE_NUMBER = /next free number: (\d+)/;

type CreateFeature = (
  projectId: string,
  values: PlanningFeatureFormValues,
) => Promise<PlanningFeatureResponse>;

function suggestedNumber(error: unknown): number | null {
  if (!(error instanceof ApiError) || error.status !== 409 || error.code !== "NumberCollision") {
    return null;
  }
  const match = NEXT_FREE_NUMBER.exec(error.message);
  if (match === null) {
    return null;
  }
  const parsed = Number(match[1]);
  return Number.isSafeInteger(parsed) ? parsed : null;
}

export async function createFeatureWithCollisionRetry(
  projectId: string,
  values: PlanningFeatureFormValues,
  create: CreateFeature,
): Promise<PlanningFeatureResponse> {
  try {
    return await create(projectId, values);
  } catch (error) {
    if (values.slug.trim().length > 0) {
      throw error;
    }
    const number = suggestedNumber(error);
    if (number === null) {
      throw error;
    }
    return create(projectId, { ...values, slug: buildFeatureSlug(number, featureSlugPart(values.title)) });
  }
}
