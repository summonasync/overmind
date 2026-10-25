const MAX_SLUG_LENGTH = 64;
const FALLBACK_PART = "feature";

const CYRILLIC_LATIN: Record<string, string> = {
  а: "a",
  б: "b",
  в: "v",
  г: "g",
  д: "d",
  е: "e",
  ё: "yo",
  ж: "zh",
  з: "z",
  и: "i",
  й: "y",
  к: "k",
  л: "l",
  м: "m",
  н: "n",
  о: "o",
  п: "p",
  р: "r",
  с: "s",
  т: "t",
  у: "u",
  ф: "f",
  х: "kh",
  ц: "ts",
  ч: "ch",
  ш: "sh",
  щ: "shch",
  ъ: "",
  ы: "y",
  ь: "",
  э: "e",
  ю: "yu",
  я: "ya",
};

export function featureSlugPart(title: string): string {
  let slug = "";
  let dashPending = false;
  const value = title.trim().toLowerCase();

  for (let i = 0; i < value.length; i += 1) {
    const char = value[i];
    const latin = CYRILLIC_LATIN[char];
    if (latin !== undefined) {
      if (latin === "") {
        continue;
      }
      if (dashPending) {
        slug += "-";
      }
      dashPending = false;
      slug += latin;
    } else if ((char >= "a" && char <= "z") || (char >= "0" && char <= "9")) {
      if (dashPending) {
        slug += "-";
      }
      dashPending = false;
      slug += char;
    } else {
      dashPending = slug.length > 0;
    }
  }

  return slug.length === 0 ? FALLBACK_PART : slug;
}

export function buildFeatureSlug(number: number, part: string): string {
  const prefix = String(number).padStart(3, "0") + "-";
  const budget = MAX_SLUG_LENGTH - prefix.length;
  const trimmed = part.length > budget ? part.slice(0, budget).replace(/-+$/, "") : part;
  return prefix + trimmed;
}

export function composeFeatureSlug(takenSlugs: readonly string[], title: string): string {
  const part = featureSlugPart(title);
  const taken = new Set(takenSlugs);
  const numbers = new Set<number>();
  let max = 0;

  for (const slug of takenSlugs) {
    const number = slugNumber(slug);
    if (number !== null) {
      numbers.add(number);
      max = Math.max(max, number);
    }
  }

  let candidate = max + 1;
  while (numbers.has(candidate) || taken.has(buildFeatureSlug(candidate, part))) {
    candidate += 1;
  }

  return buildFeatureSlug(candidate, part);
}

function slugNumber(slug: string): number | null {
  const match = /^(\d+)-/.exec(slug);
  if (match === null) {
    return null;
  }
  const parsed = Number(match[1]);
  return Number.isSafeInteger(parsed) ? parsed : null;
}

// Cosmetic mirror of the server-side generator — the server result is the truth.
