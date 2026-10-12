import type { ComponentPropsWithRef } from 'react';
import { Link, type LinkProps } from 'react-router-dom';
import { cx } from '@/shared/lib/cx';
import styles from './Button.module.css';

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger';
type Size = 'md' | 'sm';

interface StyleProps {
  variant?: Variant;
  size?: Size;
  /** Nút vuông chỉ chứa icon — bắt buộc truyền aria-label. */
  iconOnly?: boolean;
}

const buttonClass = ({ variant = 'secondary', size = 'md', iconOnly }: StyleProps, className?: string) =>
  cx(styles.button, styles[variant], styles[size], iconOnly && styles.iconOnly, className);

export function Button({ variant, size, iconOnly, className, type = 'button', ...rest }: StyleProps & ComponentPropsWithRef<'button'>) {
  return <button type={type} className={buttonClass({ variant, size, iconOnly }, className)} {...rest} />;
}

/** Liên kết điều hướng mang giao diện nút (tránh lồng <button> trong <a>). */
export function LinkButton({ variant, size, iconOnly, className, ...rest }: StyleProps & LinkProps) {
  return <Link className={buttonClass({ variant, size, iconOnly }, className)} {...rest} />;
}

/** Liên kết ra trang ngoài mang giao diện nút — luôn mở tab mới, không lộ trang portal cho trang đích. */
export function ExternalLinkButton({ variant, size, iconOnly, className, ...rest }: StyleProps & Omit<ComponentPropsWithRef<'a'>, 'target' | 'rel'>) {
  return <a className={buttonClass({ variant, size, iconOnly }, className)} target="_blank" rel="noopener noreferrer" {...rest} />;
}
