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
  'min-h-11 w-full rounded-md border border-input bg-surface px-3 py-2 text-sm placeholder:text-muted-foreground disabled:cursor-not-allowed disabled:bg-muted disabled:opacity-60 read-only:bg-muted';
export function Input({ className, ...props }: ComponentProps<'input'>) {
  return <input className={cn(fieldStyle, className)} {...props} />;
}
export function Textarea({ className, ...props }: ComponentProps<'textarea'>) {
  return <textarea className={cn(fieldStyle, 'min-h-28', className)} {...props} />;
}
export function Label(props: ComponentProps<typeof L.Root>) {
  return <L.Root className="text-sm font-semibold" {...props} />;
}
export function Checkbox(props: ComponentProps<typeof C.Root>) {
  return (
    <C.Root
      className="group flex size-11 items-center justify-center rounded-md disabled:opacity-50"
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
export function Switch(props: ComponentProps<typeof S.Root>) {
  return (
    <S.Root
      className="group inline-flex h-11 w-12 items-center rounded-md disabled:opacity-50"
      {...props}
    >
      <span className="flex h-7 w-12 items-center rounded-full border border-input bg-muted p-0.5 group-data-[state=checked]:bg-primary">
        <S.Thumb className="size-5 rounded-full border border-input bg-surface transition-transform data-[state=checked]:translate-x-5" />
      </span>
    </S.Root>
  );
}
export function RadioGroup({
  options,
  ...props
}: ComponentProps<typeof R.Root> & { options: { value: string; label: string }[] }) {
  return (
    <R.Root className="flex flex-wrap gap-4" {...props}>
      {options.map((option) => (
        <label key={option.value} className="flex items-center gap-2 text-sm">
          <R.Item
            value={option.value}
            className="flex size-11 items-center justify-center rounded-md"
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
  ...props
}: ComponentProps<typeof SelectPrimitive.Root> & {
  options: { value: string; label: string }[];
  placeholder?: string;
  label: string;
  id?: string;
}) {
  return (
    <SelectPrimitive.Root {...props}>
      <SelectPrimitive.Trigger
        id={id}
        aria-label={label}
        className={cn(fieldStyle, 'flex items-center justify-between gap-3')}
      >
        <SelectPrimitive.Value placeholder={placeholder} />
        <SelectPrimitive.Icon>
          <ChevronDown className="size-4" />
        </SelectPrimitive.Icon>
      </SelectPrimitive.Trigger>
      <SelectPrimitive.Portal>
        <SelectPrimitive.Content
          position="popper"
          className="z-50 max-h-80 min-w-[var(--radix-select-trigger-width)] overflow-auto rounded-md border border-border bg-surface p-1 shadow-[var(--shadow-overlay)]"
        >
          <SelectPrimitive.Viewport>
            {options.map((option) => (
              <SelectPrimitive.Item
                key={option.value}
                value={option.value}
                className="flex min-h-11 cursor-pointer items-center gap-2 rounded-md px-3 text-sm data-[highlighted]:bg-muted"
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
