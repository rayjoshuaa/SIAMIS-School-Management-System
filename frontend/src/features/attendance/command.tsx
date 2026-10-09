import { useEffect, useRef, useState, type ComponentProps } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { AlertDialog } from '../../components/ui/overlays';
import { Input } from '../../components/ui/controls';
import { FormField } from '../../components/shared/form-field';
import { Button } from '../../components/ui/button';
import { api } from '../../lib/api/client';
import { ApiError } from '../../lib/api/errors';
import type { Review } from './contracts';

export type Command =
  'manual' | 'corrections' | 'adjudications' | 'confirm-absence' | 'finalize' | 'reopen';
const titles: Record<Command, string> = {
  manual: 'Record manual evidence',
  corrections: 'Add correction evidence',
  adjudications: 'Include / exclude evidence',
  'confirm-absence': 'Confirm potential absence',
  finalize: 'Finalize attendance day',
  reopen: 'Reopen finalized day',
};
export function AttendanceCommand({
  action,
  review,
  onClose,
  onCloseAutoFocus,
}: {
  action: Command;
  review: Review;
  onClose: () => void;
  onCloseAutoFocus: ComponentProps<typeof AlertDialog>['onCloseAutoFocus'];
}) {
  const [reason, setReason] = useState('');
  const [occurredAt, setOccurredAt] = useState('');
  const [direction, setDirection] = useState('In');
  const [eventId, setEventId] = useState('');
  const [included, setIncluded] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [discardRequested, setDiscardRequested] = useState(false);
  const keepEditing = useRef<HTMLButtonElement | null>(null);
  const editFocus = useRef<HTMLElement | null>(null);
  const dirty = !!(reason || occurredAt || eventId || included || direction !== 'In');
  useEffect(() => {
    if (discardRequested) keepEditing.current?.focus();
  }, [discardRequested]);
  const [manualRequestKey] = useState(() => crypto.randomUUID());
  // Freeze concurrency tokens for this explicit decision. A conflict requires a new review.
  const [tokens] = useState(() => ({
    expectedVersion: review.version,
    expectedSourceFingerprint: review.sourceFingerprint,
  }));
  const busy = useRef(false);
  const cache = useQueryClient();
  const update = useMutation({
    mutationFn: (body: Record<string, unknown>) =>
      api(
        action === 'manual'
          ? `/api/employees/${review.employeeId}/attendance-events/manual`
          : `/api/employees/${review.employeeId}/attendance-days/${review.businessDate}/${action}`,
        { method: 'POST', body },
      ),
    onSuccess: async () => {
      await Promise.all([
        cache.invalidateQueries({ queryKey: ['attendance'] }),
        cache.invalidateQueries({ queryKey: ['hr-dashboard'] }),
      ]);
      onClose();
    },
    onSettled: () => {
      busy.current = false;
    },
  });
  const conflict = update.error instanceof ApiError && update.error.status === 409;
  function submit() {
    if (busy.current || conflict || discardRequested) return;
    const validation: Record<string, string> = {};
    if (!reason.trim()) validation.reason = 'A reason is required.';
    if (reason.length > 2000) validation.reason = 'Use at most 2,000 characters.';
    if (
      ['manual', 'corrections'].includes(action) &&
      !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$/.test(occurredAt)
    )
      validation.occurredAt =
        'Enter an ISO timestamp with seconds and an explicit Z or numeric offset.';
    if (action === 'adjudications' && !eventId) validation.eventId = 'Select recorded evidence.';
    if (action === 'adjudications' && !included)
      validation.included = 'Choose Include or Exclude explicitly.';
    if (
      action !== 'manual' &&
      (!Number.isSafeInteger(tokens.expectedVersion) ||
        !/^[A-F0-9]{64}$/.test(tokens.expectedSourceFingerprint))
    )
      validation.version =
        'Review tokens are unavailable or outside safe precision. Close and refresh the day.';
    setErrors(validation);
    if (Object.keys(validation).length) return;
    busy.current = true;
    const body: Record<string, unknown> = {
      ...(action === 'manual' ? {} : tokens),
      reason: reason.trim(),
    };
    if (['manual', 'corrections'].includes(action))
      Object.assign(body, { occurredAt, direction, manualRequestKey });
    if (action === 'adjudications')
      Object.assign(body, { attendanceEventId: eventId, included: included === 'true' });
    update.mutate(body);
  }
  return (
    <AlertDialog
      open
      onOpenChange={(open) => {
        if (open || update.isPending) return;
        if (dirty) {
          editFocus.current = document.activeElement as HTMLElement;
          setDiscardRequested(true);
        } else onClose();
      }}
      size="md"
      confirmDisabled={discardRequested || conflict}
      variant={action === 'reopen' || action === 'confirm-absence' ? 'destructive' : 'primary'}
      title={titles[action]}
      description={
        action === 'manual'
          ? 'Add immutable HR-authorized evidence. Original events and historical revisions remain unchanged; current validity may become stale. This is not employee self-service clocking.'
          : 'Confirm an explicit, reasoned attendance decision for this employee and business date. Original evidence and previous revisions remain unchanged. This has no automatic leave, payroll or disciplinary effect.'
      }
      confirmLabel={`Confirm ${titles[action].toLowerCase()}`}
      loading={update.isPending}
      closeOnConfirm={false}
      onConfirm={submit}
      onCloseAutoFocus={onCloseAutoFocus}
    >
      <div className="space-y-4">
        {discardRequested && (
          <div role="alert" className="ui-overlay-discard">
            <p className="font-semibold">Discard unsaved attendance decision?</p>
            <p className="text-sm">Nothing has been submitted by closing this form.</p>
            <div className="mt-3 flex flex-wrap gap-2">
              <Button
                ref={keepEditing}
                variant="outline"
                onClick={() => {
                  setDiscardRequested(false);
                  editFocus.current?.focus();
                }}
              >
                Keep editing
              </Button>
              <Button variant="destructive" onClick={onClose}>
                Discard changes
              </Button>
            </div>
          </div>
        )}
        <p className="break-all text-sm">
          {review.employeeId} · {review.businessDate} · {review.calculation.businessTimeZone}
        </p>
        {['manual', 'corrections'].includes(action) && (
          <>
            <FormField
              id="attendance-instant"
              label="Occurred at (explicit offset)"
              required
              error={errors.occurredAt}
              hint={`Example format: ${review.businessDate}T07:30:00.0000001+07:00. The server validates the instant and Bangkok business date.`}
            >
              {(props) => (
                <Input
                  {...props}
                  maxLength={40}
                  value={occurredAt}
                  disabled={update.isPending}
                  onChange={(e) => setOccurredAt(e.target.value)}
                />
              )}
            </FormField>
            <FormField id="attendance-direction" label="Direction" required>
              {(props) => (
                <select
                  {...props}
                  className="ui-control min-h-11 w-full px-3 py-2"
                  value={direction}
                  disabled={update.isPending}
                  onChange={(e) => setDirection(e.target.value)}
                >
                  {['In', 'Out', ...(action === 'manual' ? ['Unknown'] : [])].map((value) => (
                    <option key={value}>{value}</option>
                  ))}
                </select>
              )}
            </FormField>
          </>
        )}
        {action === 'adjudications' && (
          <>
            <FormField
              id="attendance-event"
              label="Recorded evidence"
              required
              error={errors.eventId}
            >
              {(props) => (
                <select
                  {...props}
                  className="ui-control min-h-11 w-full px-3 py-2"
                  value={eventId}
                  disabled={update.isPending}
                  onChange={(e) => setEventId(e.target.value)}
                >
                  <option value="">Select evidence</option>
                  {review.rawCalculation.events.map((event) => (
                    <option key={event.attendanceEventId} value={event.attendanceEventId}>
                      {event.occurredAtUtc} · {event.direction} · {event.source}
                    </option>
                  ))}
                </select>
              )}
            </FormField>
            <FormField id="attendance-included" label="Decision" required error={errors.included}>
              {(props) => (
                <select
                  {...props}
                  className="ui-control min-h-11 w-full px-3 py-2"
                  value={included}
                  disabled={update.isPending}
                  onChange={(e) => setIncluded(e.target.value)}
                >
                  <option value="">Select decision</option>
                  <option value="true">Include in calculation</option>
                  <option value="false">Exclude from calculation</option>
                </select>
              )}
            </FormField>
          </>
        )}
        <FormField id="attendance-reason" label="Reason" required error={errors.reason}>
          {(props) => (
            <textarea
              {...props}
              className="ui-control min-h-24 w-full px-3 py-2"
              maxLength={2000}
              value={reason}
              disabled={update.isPending}
              onChange={(e) => setReason(e.target.value)}
            />
          )}
        </FormField>
        {errors.version && (
          <p role="alert" className="text-sm text-destructive">
            {errors.version}
          </p>
        )}
        {update.isError && (
          <div role="alert" className="space-y-2 text-sm text-destructive">
            <p>
              {conflict
                ? 'Attendance sources or workflow state changed, or prerequisites were not met. Close this confirmation and refresh the review before a new decision.'
                : update.error.message}
            </p>
            {update.error instanceof ApiError &&
              Object.values(update.error.fieldErrors)
                .flat()
                .map((message, i) => <p key={i}>{message}</p>)}
            <p>No successful change is implied.</p>
          </div>
        )}
      </div>
    </AlertDialog>
  );
}
