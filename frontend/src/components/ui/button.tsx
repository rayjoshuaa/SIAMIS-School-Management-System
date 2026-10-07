import type { ButtonHTMLAttributes } from 'react';
import { cva } from 'class-variance-authority';
import { cn } from '../../lib/utils/cn';
import { LoaderCircle } from 'lucide-react';
const styles = cva(
  'ui-button inline-flex min-h-11 items-center justify-center gap-2 px-4 text-sm font-semibold disabled:cursor-not-allowed disabled:opacity-50',
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
    },
    defaultVariants: { variant: 'primary', icon: false },
  },
);
export type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: 'primary' | 'secondary' | 'outline' | 'ghost' | 'destructive';
  icon?: boolean;
  loading?: boolean;
};
export function Button({
  variant,
  icon,
  loading,
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
      className={cn(styles({ variant, icon }), className)}
      {...props}
    >
      {loading && <LoaderCircle aria-hidden="true" className="size-4 animate-spin" />}
      {children}
    </button>
  );
}
