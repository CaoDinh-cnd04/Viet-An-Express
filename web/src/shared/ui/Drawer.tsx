import { useEffect, useRef, type ReactNode } from 'react';
import { useI18n } from '@/shared/i18n';
import { Icon } from './Icon';
import styles from './Drawer.module.css';

interface DrawerProps {
  open: boolean;
  /** Dòng nhỏ đầu khung (vd "Thông báo · Đang hiện"). */
  title: string;
  onClose: () => void;
  footer?: ReactNode;
  children: ReactNode;
}

/** Khung trượt từ cạnh phải (xem chi tiết mà vẫn thấy danh sách phía sau) — dựa trên <dialog> gốc: giữ focus, Esc để đóng. */
export function Drawer({ open, title, onClose, footer, children }: DrawerProps) {
  const ref = useRef<HTMLDialogElement>(null);
  const { t } = useI18n();

  useEffect(() => {
    const dlg = ref.current;
    if (!dlg) return;
    if (open && !dlg.open) dlg.showModal();
    if (!open && dlg.open) dlg.close();
  }, [open]);

  return (
    <dialog ref={ref} className={styles.drawer} onClose={onClose} onClick={e => e.target === ref.current && onClose()} aria-label={t(title)}>
      <div className={styles.box}>
        <header className={styles.header}>
          <span className={styles.title}>{t(title)}</span>
          <button type="button" className={styles.close} onClick={onClose} aria-label={t('Đóng')}>
            <Icon name="close" size={16} />
          </button>
        </header>
        <div className={styles.body}>{open && children}</div>
        {footer && <footer className={styles.footer}>{footer}</footer>}
      </div>
    </dialog>
  );
}
