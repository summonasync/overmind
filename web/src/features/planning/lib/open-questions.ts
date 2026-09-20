const OPEN_QUESTIONS_SECTION = /<open-questions>([\s\S]*?)<\/open-questions>/;

export function extractOpenQuestions(specBody: string): string | null {
  const match = OPEN_QUESTIONS_SECTION.exec(specBody);
  if (match === null) {
    return null;
  }
  const content = match[1].trim();
  return content.length > 0 ? content : null;
}
