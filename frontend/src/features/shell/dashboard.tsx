import {
  Users,
  GraduationCap,
  School,
  ContactRound,
  ChartNoAxesCombined,
  CalendarDays,
  Activity,
  Zap,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { Card } from '../../components/ui/feedback';
import { StatisticCard } from '../../components/shared/statistic-card';

function DashboardRegion({
  title,
  icon: Icon,
  message,
  detail,
  chart = false,
}: {
  title: string;
  icon: LucideIcon;
  message: string;
  detail: string;
  chart?: boolean;
}) {
  return (
    <Card className="flex h-full flex-col p-4">
      <div className="flex items-center gap-2 border-b border-border pb-3">
        <Icon aria-hidden="true" className="size-4 text-muted-foreground" />
        <h2 className="text-sm font-semibold">{title}</h2>
      </div>
      <div
        className={`flex flex-1 flex-col items-center justify-center px-2 py-6 text-center ${chart ? 'min-h-44' : 'min-h-36'}`}
      >
        <Icon aria-hidden="true" className="mb-3 size-7 text-muted-foreground" />
        <p className="text-sm font-medium">{message}</p>
        <p className="mt-2 max-w-xs text-xs text-muted-foreground">{detail}</p>
      </div>
    </Card>
  );
}
export function SchoolDashboard() {
  return (
    <div className="space-y-4">
      <p className="text-xs text-muted-foreground">
        School dashboard preview · school data and actions are not connected.
      </p>
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatisticCard label="Students" icon={Users} />
        <StatisticCard label="Teachers" icon={GraduationCap} />
        <StatisticCard label="Classes" icon={School} />
        <StatisticCard label="Admissions" icon={ContactRound} />
      </div>
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <div className="lg:col-span-2">
          <DashboardRegion
            title="Enrollment overview"
            icon={ChartNoAxesCombined}
            chart
            message="Enrollment data is not connected"
            detail="Enrollment trends will appear here when School Management is available."
          />
        </div>
        <DashboardRegion
          title="Student population"
          icon={Users}
          chart
          message="Student data is not connected"
          detail="Population summaries will use verified school records."
        />
      </div>
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
        <DashboardRegion
          title="Recent activities"
          icon={Activity}
          message="Activity feed is not connected"
          detail="School activities will appear here when the relevant modules are available."
        />
        <DashboardRegion
          title="Upcoming events"
          icon={CalendarDays}
          message="School calendar is not connected"
          detail="Upcoming school events will appear here when a calendar is available."
        />
        <DashboardRegion
          title="Quick actions"
          icon={Zap}
          message="No actions available"
          detail="Available actions will follow your permissions when school workflows are connected."
        />
      </div>
    </div>
  );
}
