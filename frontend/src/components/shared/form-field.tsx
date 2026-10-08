import type { ReactNode } from 'react';
import { Label } from '../ui/controls';
export function FormField({
  id,
  label,
  required,
  hint,
  error,
  children,
}: {
  id: string;
  label: string;
  required?: boolean;
  hint?: string;
  error?: string;
  children: (props: {
    id: string;
    'aria-describedby'?: string;
    'aria-invalid': boolean;
    'aria-required'?: boolean;
  }) => ReactNode;
}) {
  const describedBy =
    [hint ? `${id}-hint` : null, error ? `${id}-error` : null].filter(Boolean).join(' ') ||
    undefined;
  return (
    <div className="ui-form-field flex flex-col">
      <Label htmlFor={id}>
        {label}
        {required && (
          <span aria-hidden="true" className="ml-1 text-destructive">
            *
          </span>
        )}
      </Label>
      <div className="mt-2">
        {children({
          id,
          'aria-describedby': describedBy,
          'aria-invalid': Boolean(error),
          'aria-required': required || undefined,
        })}
      </div>
      {hint && (
        <p id={`${id}-hint`} className="ui-field-hint mt-1.5 text-muted-foreground">
          {hint}
        </p>
      )}
      {error && (
        <p id={`${id}-error`} role="alert" className="mt-1.5 text-sm text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}
