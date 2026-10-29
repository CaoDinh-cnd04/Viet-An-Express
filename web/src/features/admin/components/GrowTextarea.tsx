import { useLayoutEffect, useRef, type TextareaHTMLAttributes } from 'react';
import { cx } from '@/shared/lib/cx';
import styles from './NotificationEditor.module.css';

/** Ô chữ không viền tự cao theo nội dung. */
export function GrowTextarea({ className, value, ...rest }: TextareaHTMLAttributes<HTMLTextAreaElement> & { value: string }) {
  const ref = useRef<HTMLTextAreaElement>(null);
  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) return;
    el.style.height = 'auto';
    el.style.height = `${el.scrollHeight}px`;
  }, [value]);
  return <textarea ref={ref} rows={1} className={cx(styles.bare, className)} value={value} {...rest} />;
}
