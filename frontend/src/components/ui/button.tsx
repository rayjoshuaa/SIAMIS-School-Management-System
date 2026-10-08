import type { ComponentProps } from 'react';
import { cva } from 'class-variance-authority';
import { cn } from '../../lib/utils/cn';
import { LoaderCircle } from 'lucide-react';
const styles = cva(
  'ui-button inline-flex items-center justify-center gap-2 px-4 text-sm font-semibold disabled:cursor-not-allowed disabled:opacity-50',
  {
    variants: {
      variant: {
        primary: 'ui-button-primary',
        secondary: 'bg-secondary text-secondary-foreground enabled:hover:bg-muted',
        outline: 'border border-input bg-surface enabled:hover:bg-muted',
        ghost: 'enabled:hover:bg-muted',
        destructive: 'bg-destructive text-primary-foreground enabled:hover:opacity-90',
      },
      icon: { true: 'min-w-11 px-2', false: '' },
      density: { comfortable: 'min-h-11', compact: 'min-h-9 max-sm:min-h-11' },
    },
    defaultVariants: { variant: 'primary', icon: false },
  },
);
export type ButtonProps = ComponentProps<'button'> & {
  variant?: 'primary' | 'secondary' | 'outline' | 'ghost' | 'destructive';
  icon?: boolean;
  loading?: boolean;
  density?: 'comfortable' | 'compact';
};
export function Button({
  variant,
  icon,
  loading,
  density = 'comfortable',
  disabled,
  className,
  children,
  type = 'button',
  ...props
}: ButtonProps) {
  return (
    <button
      type={type}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      data-variant={variant ?? 'primary'}
      data-density={density}
      className={cn(styles({ variant, icon, density }), className)}
      {...props}
    >
      {loading && <LoaderCircle aria-hidden="true" className="size-4 animate-spin" />}
      {children}
    </button>
  );
}
