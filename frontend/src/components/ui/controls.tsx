import type { ComponentProps } from 'react';
import {
  Checkbox as C,
  RadioGroup as R,
  Switch as S,
  Label as L,
  Select as SelectPrimitive,
} from 'radix-ui';
import { Check, ChevronDown } from 'lucide-react';
import { cn } from '../../lib/utils/cn';
const fieldStyle =
  'ui-control min-h-11 w-full px-3 py-2 text-base sm:text-sm placeholder:text-muted-foreground disabled:cursor-not-allowed disabled:bg-muted disabled:opacity-60';
export function Input({ className, ...props }: ComponentProps<'input'>) {
  return <input className={cn(fieldStyle, 'read-only:bg-muted', className)} {...props} />;
}
export function Textarea({ className, ...props }: ComponentProps<'textarea'>) {
  return (
    <textarea className={cn(fieldStyle, 'min-h-28 read-only:bg-muted', className)} {...props} />
  );
}
export function Label({ className, ...props }: ComponentProps<typeof L.Root>) {
  return (
    <L.Root
      className={cn('ui-label block text-sm leading-5 font-semibold', className)}
      {...props}
    />
  );
}
export function Checkbox({ className, ...props }: ComponentProps<typeof C.Root>) {
  return (
    <C.Root
      className={cn(
        'ui-choice group flex size-11 items-center justify-center rounded-md disabled:opacity-50',
        className,
      )}
      {...props}
    >
      <span className="flex size-5 items-center justify-center rounded-sm border border-input bg-surface group-data-[state=checked]:border-primary group-data-[state=checked]:bg-primary group-data-[state=checked]:text-primary-foreground">
        <C.Indicator>
          <Check className="size-4" />
        </C.Indicator>
      </span>
    </C.Root>
  );
}
export function Switch({ className, ...props }: ComponentProps<typeof S.Root>) {
  return (
    <S.Root
      className={cn(
        'ui-choice group inline-flex h-11 w-12 items-center rounded-md disabled:opacity-50',
        className,
      )}
      {...props}
    >
      <span className="flex h-7 w-12 items-center rounded-full border border-input bg-muted p-0.5 group-data-[state=checked]:bg-primary">
        <S.Thumb className="ui-switch-thumb size-5 rounded-full border border-input bg-surface data-[state=checked]:translate-x-5" />
      </span>
    </S.Root>
  );
}
export function RadioGroup({
  options,
  className,
  ...props
}: ComponentProps<typeof R.Root> & { options: { value: string; label: string }[] }) {
  return (
    <R.Root className={cn('flex flex-wrap gap-4', className)} {...props}>
      {options.map((option) => (
        <label key={option.value} className="flex items-center gap-2 text-sm">
          <R.Item
            value={option.value}
            className="ui-choice flex size-11 items-center justify-center rounded-md disabled:opacity-50"
          >
            <span className="flex size-5 items-center justify-center rounded-full border border-input">
              <R.Indicator className="size-2.5 rounded-full bg-primary" />
            </span>
          </R.Item>
          {option.label}
        </label>
      ))}
    </R.Root>
  );
}
export function Select({
  options,
  placeholder = 'Select an option',
  label,
  id,
  'aria-describedby': describedBy,
  'aria-invalid': invalid,
  'aria-required': required,
  className,
  ...props
}: ComponentProps<typeof SelectPrimitive.Root> & {
  options: { value: string; label: string }[];
  placeholder?: string;
  label: string;
  id?: string;
  'aria-describedby'?: string;
  'aria-invalid'?: ComponentProps<'button'>['aria-invalid'];
  'aria-required'?: boolean;
  className?: string;
}) {
  return (
    <SelectPrimitive.Root {...props}>
      <SelectPrimitive.Trigger
        id={id}
        aria-label={label}
        aria-describedby={describedBy}
        aria-invalid={invalid}
        aria-required={required}
        className={cn(fieldStyle, 'ui-select-trigger flex items-center', className)}
      >
        <span className="ui-select-value">
          <SelectPrimitive.Value placeholder={placeholder} />
        </span>
        <SelectPrimitive.Icon className="ui-select-icon">
          <ChevronDown aria-hidden="true" />
        </SelectPrimitive.Icon>
      </SelectPrimitive.Trigger>
      <SelectPrimitive.Portal>
        <SelectPrimitive.Content
          position="popper"
          className="ui-floating z-50 max-h-80 min-w-[var(--radix-select-trigger-width)] overflow-auto rounded-md border border-border bg-surface p-1 shadow-[var(--shadow-overlay)]"
        >
          <SelectPrimitive.Viewport>
            {options.map((option) => (
              <SelectPrimitive.Item
                key={option.value}
                value={option.value}
                className="ui-menu-item flex min-h-11 cursor-pointer items-center gap-2 rounded-md px-3 text-sm data-[highlighted]:bg-muted"
              >
                <SelectPrimitive.ItemText>{option.label}</SelectPrimitive.ItemText>
                <SelectPrimitive.ItemIndicator>
                  <Check className="size-4" />
                </SelectPrimitive.ItemIndicator>
              </SelectPrimitive.Item>
            ))}
          </SelectPrimitive.Viewport>
        </SelectPrimitive.Content>
      </SelectPrimitive.Portal>
    </SelectPrimitive.Root>
  );
}
