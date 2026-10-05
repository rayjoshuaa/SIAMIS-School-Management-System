import type { LucideIcon } from 'lucide-react';
import { Card } from '../ui/feedback';

export function StatisticCard({ label, icon: Icon }: { label: string; icon: LucideIcon }) {
  return (
    <Card className="p-4">
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-sm font-medium">{label}</h2>
        <Icon aria-hidden="true" className="size-5 text-primary" />
      </div>
      <p aria-label={`${label}: no data available`} className="mt-3 text-2xl font-semibold">
        —
      </p>
      <p className="mt-1 text-xs text-muted-foreground">No data available</p>
    </Card>
  );
}
