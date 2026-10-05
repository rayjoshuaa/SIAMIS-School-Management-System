let epoch = 0;
const listeners = new Set<() => void>();
export const sessionEpoch = () => epoch;
export function advanceSession() {
  epoch++;
}
export function reportSessionLoss(started: number) {
  if (started === epoch) listeners.forEach((listener) => listener());
}
export function onSessionLoss(listener: () => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}
