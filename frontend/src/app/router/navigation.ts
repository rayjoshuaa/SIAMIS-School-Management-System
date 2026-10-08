import {
  LayoutDashboard,
  Building2,
  Users,
  CalendarDays,
  Clock3,
  Wallet,
  FileLock2,
  ShieldCheck,
  GraduationCap,
  ContactRound,
  Landmark,
  BookOpen,
  ListTodo,
  ChartNoAxesCombined,
  Settings,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
export type RouteMeta = {
  path: string;
  label: string;
  breadcrumb: string;
  description: string;
  icon: LucideIcon;
  group: 'workspace' | 'hr' | 'modules';
  capability?: string;
  visible: boolean;
  status: 'foundation' | 'planned';
};
export const routes: RouteMeta[] = [
  {
    path: '/',
    label: 'Dashboard',
    breadcrumb: 'Dashboard',
    description: 'A school-wide view of people, learning and operations.',
    icon: LayoutDashboard,
    group: 'workspace',
    visible: true,
    status: 'foundation',
  },
  {
    path: '/hr',
    label: 'HR Overview',
    breadcrumb: 'HR',
    description: 'People, employment and school operations.',
    icon: Building2,
    group: 'hr',
    capability: 'Reporting.Read',
    visible: true,
    status: 'foundation',
  },
  {
    path: '/hr/employees',
    label: 'Employees',
    breadcrumb: 'Employees',
    description: 'Employee information and employment records.',
    icon: Users,
    group: 'hr',
    capability: 'Employee.Read',
    visible: true,
    status: 'foundation',
  },
  {
    path: '/hr/leave',
    label: 'Leave',
    breadcrumb: 'Leave',
    description: 'Leave requests, policies and entitlement.',
    icon: CalendarDays,
    group: 'hr',
    capability: 'Leave.Read',
    visible: true,
    status: 'foundation',
  },
  {
    path: '/hr/attendance',
    label: 'Attendance',
    breadcrumb: 'Attendance',
    description: 'Attendance evidence, review and reporting.',
    icon: Clock3,
    group: 'hr',
    capability: 'Attendance.Read',
    visible: true,
    status: 'foundation',
  },
  {
    path: '/hr/payroll',
    label: 'Payroll',
    breadcrumb: 'Payroll',
    description: 'Payroll operations and payslips.',
    icon: Wallet,
    group: 'hr',
    capability: 'Payroll.Read',
    visible: true,
    status: 'foundation',
  },
  {
    path: '/hr/documents',
    label: 'Documents',
    breadcrumb: 'Documents',
    description: 'Confidential HR documents and evidence.',
    icon: FileLock2,
    group: 'hr',
    capability: 'HRDocuments.Read',
    visible: true,
    status: 'foundation',
  },
  {
    path: '/hr/security',
    label: 'Accounts & Security',
    breadcrumb: 'Accounts & Security',
    description: 'Account administration and security.',
    icon: ShieldCheck,
    group: 'hr',
    capability: 'Security.Manage',
    visible: true,
    status: 'foundation',
  },
  {
    path: '/school-management',
    label: 'School Management',
    breadcrumb: 'School Management',
    description: 'The next major SIAMIS module.',
    icon: GraduationCap,
    group: 'modules',
    visible: true,
    status: 'planned',
  },
];
// Planned items deliberately have no business routes or invented capabilities.
export const schoolNavigation = [
  'School Setup',
  'Academic Sessions',
  'Admissions',
  'Enrollment',
  'Students',
  'Parents / Guardians',
  'Classes & Sections',
  'Subjects',
  'Teachers',
  'Attendance',
  'Timetable',
  'Examinations',
  'Assessment & Grading',
  'Results & Report Cards',
  'Student Documents',
  'Reports',
];
export const modules = [
  { id: 'school', label: 'School Management', icon: GraduationCap, status: 'planned' },
  { id: 'admissions', label: 'Admissions & CRM', icon: ContactRound, status: 'planned' },
  { id: 'hr', label: 'Human Resources', icon: Building2, status: 'backend' },
  { id: 'finance', label: 'Accounting & Finance', icon: Landmark, status: 'planned' },
  { id: 'learning', label: 'Teacher Learning', icon: BookOpen, status: 'planned' },
  { id: 'projects', label: 'Projects & Tasks', icon: ListTodo, status: 'planned' },
  { id: 'reports', label: 'Reports', icon: ChartNoAxesCombined, status: 'planned' },
  { id: 'system', label: 'System Administration', icon: Settings, status: 'planned' },
] as const;
export const paths = {
  dashboard: routes[0].path,
  hr: '/hr',
  school: routes.find((route) => route.group === 'modules')!.path,
  designSystem: import.meta.env.DEV ? '/dev/ui' : '',
};
export function visibleRoutes(capabilities: readonly string[]) {
  return routes.filter(
    (route) => route.visible && (!route.capability || capabilities.includes(route.capability)),
  );
}
export function activeRoute(path: string) {
  return routes.find((route) => route.path === path);
}
export function hrIsActive(path: string) {
  return path === paths.hr || path.startsWith(`${paths.hr}/`);
}
export function breadcrumbs(path: string, capabilities: readonly string[]) {
  const route = activeRoute(path);
  if (!route) return [{ label: 'Page not found' }];
  if (route.group !== 'hr' || path === paths.hr) return [{ label: route.breadcrumb }];
  return [
    { label: 'HR', href: capabilities.includes('Reporting.Read') ? paths.hr : undefined },
    { label: route.breadcrumb },
  ];
}

// Module membership is presentation metadata. Existing route capabilities and
// backend authorization remain authoritative; empty future modules stay hidden.
export type WorkspaceModule = {
  id: string;
  label: string;
  icon: LucideIcon;
  destinations: readonly string[];
};
export const workspaceModules: WorkspaceModule[] = [
  { id: 'workspace', label: 'Workspace', icon: LayoutDashboard, destinations: ['/'] },
  {
    id: 'school',
    label: 'School Management',
    icon: GraduationCap,
    destinations: ['/school-management'],
  },
  { id: 'admissions', label: 'Admissions & CRM', icon: ContactRound, destinations: [] },
  {
    id: 'hr',
    label: 'Human Resources',
    icon: Building2,
    destinations: [
      '/hr',
      '/hr/employees',
      '/hr/attendance',
      '/hr/leave',
      '/hr/payroll',
      '/hr/documents',
    ],
  },
  { id: 'finance', label: 'Accounting & Finance', icon: Landmark, destinations: [] },
  { id: 'learning', label: 'Professional Development', icon: BookOpen, destinations: [] },
  { id: 'communication', label: 'Communication', icon: ContactRound, destinations: [] },
  { id: 'projects', label: 'Projects & Tasks', icon: ListTodo, destinations: [] },
  { id: 'reports', label: 'Reports', icon: ChartNoAxesCombined, destinations: [] },
  { id: 'system', label: 'Administration', icon: Settings, destinations: ['/hr/security'] },
];
export function moduleDestinations(module: WorkspaceModule, capabilities: readonly string[]) {
  const visible = visibleRoutes(capabilities);
  return module.destinations.flatMap((path) => visible.filter((route) => route.path === path));
}
export function availableModules(capabilities: readonly string[]) {
  return workspaceModules.filter((module) => moduleDestinations(module, capabilities).length > 0);
}
export function currentModule(path: string) {
  return workspaceModules.find((module) => module.destinations.includes(path));
}
export function destinationLabel(route: RouteMeta) {
  return route.path === '/hr' || route.path === '/school-management' ? 'Overview' : route.label;
}
