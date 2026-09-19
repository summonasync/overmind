interface InfoPanelStatProps {
  value: string | number;
  label: string;
}

export function InfoPanelStat({ value, label }: InfoPanelStatProps) {
  return (
    <div className="rounded-lg border border-border bg-surface p-3">
      <p className="text-2xl font-semibold text-foreground">{value}</p>
      <p className="text-xs text-muted">{label}</p>
    </div>
  );
}
