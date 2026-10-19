interface GrimoireInstructionsProps {
  steps: string[];
}

export function GrimoireInstructions({ steps }: GrimoireInstructionsProps) {
  if (steps.length === 0) {
    return null;
  }

  return (
    <ol className="mt-3 list-decimal space-y-1.5 pl-5 text-sm text-foreground">
      {steps.map((step) => (
        <li key={step}>{step}</li>
      ))}
    </ol>
  );
}
