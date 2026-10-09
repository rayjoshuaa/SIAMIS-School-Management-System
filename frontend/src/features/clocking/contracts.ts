export const arrangements = [
  { value: 'OnCampus', label: 'On Campus' },
  { value: 'OnlineClass', label: 'Online Class' },
  { value: 'RemoteWork', label: 'Remote Work' },
] as const;
export type WorkArrangement = (typeof arrangements)[number]['value'];
export type ClockSession = {
  sessionId: string;
  workArrangement: WorkArrangement;
  inEventId: string;
  clockedInAtUtc: string;
  inBusinessDate: string;
  outEventId: string | null;
  clockedOutAtUtc: string | null;
  outBusinessDate: string | null;
  businessTimeZone: string;
  isOpen: boolean;
};
export type ClockResult = { session: ClockSession; isReplay: boolean };
export type SessionPage = {
  items: ClockSession[];
  page: number;
  pageSize: number;
  totalCount: number;
};
export type ClockCommand =
  | { direction: 'in'; body: { requestKey: string; workArrangement: WorkArrangement } }
  | { direction: 'out'; body: { requestKey: string; sessionId: string } };
export function arrangementLabel(value: string) {
  return arrangements.find((item) => item.value === value)?.label ?? 'Unknown arrangement';
}
