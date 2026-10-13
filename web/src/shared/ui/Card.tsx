import type { ReactNode } from 'react';
import { useTranslateNode } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import styles from './Card.module.css';

interface CardProps {
  title?: ReactNode;
  /** Chú thích phụ cạnh tiêu đề, VD "(Shipper)". */
  subtitle?: ReactNode;
  actions?: ReactNode;
  className?: string;
  /** Bỏ padding thân — dùng cho bảng tràn viền. */
  flush?: boolean;
  /** Tiêu đề nổi bật (chữ màu) — cho các khung của form dài, dễ nhận ra từng mục. */
  accent?: boolean;
  children: ReactNode;
}

export function Card({ title, subtitle, actions, className, flush, accent, children }: CardProps) {
  const tr = useTranslateNode();
  return (
    <section className={cx(styles.card, accent && styles.accent, className)}>
      {(title || actions) && (
        <header className={styles.header}>
          {title && (
            <h3 className={styles.title}>
              {tr(title)} {subtitle && <span className={styles.subtitle}>{tr(subtitle)}</span>}
            </h3>
          )}
          {actions && <div className={styles.actions}>{actions}</div>}
        </header>
      )}
      <div className={cx(!flush && styles.body)}>{children}</div>
    </section>
  );
}
