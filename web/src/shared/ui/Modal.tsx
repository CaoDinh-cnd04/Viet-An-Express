import { useEffect, useRef, type ReactNode } from 'react';
import { useI18n, useTranslateNode } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Icon } from './Icon';
import styles from './Modal.module.css';

interface ModalProps {
  open: boolean;
  title: string;
  onClose: () => void;
  footer?: ReactNode;
  /** Ghi chú nhỏ bên trái thanh nút. */
  footerNote?: ReactNode;
  /** md 560px (mặc định) · lg 860px · xl 1120px · full gần hết màn hình (rộng ≤ 1320px, cao 94vh) */
  size?: 'md' | 'lg' | 'xl' | 'full';
  children: ReactNode;
}

/** Hộp thoại dựa trên <dialog> gốc: tự giữ focus, Esc để đóng. */
export function Modal({ open, title, onClose, footer, footerNote, size = 'md', children }: ModalProps) {
  const ref = useRef<HTMLDialogElement>(null);
  const { t } = useI18n();
  const tr = useTranslateNode();

  useEffect(() => {
    const dlg = ref.current;
    if (!dlg) return;
    if (open && !dlg.open) dlg.showModal();
    if (!open && dlg.open) dlg.close();
  }, [open]);

  return (
    <dialog
      ref={ref}
      className={cx(styles.dialog, size !== 'md' && styles[size])}
      onClose={onClose}
      onClick={e => e.target === ref.current && onClose()}
      aria-labelledby="modal-title"
    >
      <div className={styles.box}>
        <header className={styles.header}>
          <h2 id="modal-title" className={styles.title}>{t(title)}</h2>
          <button type="button" className={styles.close} onClick={onClose} aria-label={t('Đóng')}>
            <Icon name="close" size={16} />
          </button>
        </header>
        <div className={styles.body}>{children}</div>
        {footer && (
          <footer className={styles.footer}>
            {footerNote && <span className={styles.footerNote}>{tr(footerNote)}</span>}
            {footer}
          </footer>
        )}
      </div>
    </dialog>
  );
}
