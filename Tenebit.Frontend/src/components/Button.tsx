import type { ButtonHTMLAttributes, ReactNode } from 'react';

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger';
  icon?: ReactNode;
  iconOnly?: boolean;
};

export function Button({ variant = 'primary', icon, iconOnly, children, className = '', ...props }: ButtonProps) {
  return (
    <button className={`button button--${variant}${iconOnly ? ' button--icon' : ''} ${className}`} {...props}>
      {icon ? <span className="button__icon">{icon}</span> : null}
      {iconOnly ? null : <span>{children}</span>}
    </button>
  );
}
