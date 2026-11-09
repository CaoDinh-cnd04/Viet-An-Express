import { useRef, useState, type ReactNode } from 'react';
import { useTranslateNode } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Icon, type IconName } from './Icon';
import styles from './Feedback.module.css';

type NoticeTone = 'info' | 'warning' | 'danger' | 'success';

/** Hộp thông báo trong trang (lưu ý, cảnh báo, lỗi). */
export function Notice({ tone = 'info', title, children }: { tone?: NoticeTone; title?: ReactNode; children?: ReactNode }) {
  const tr = useTranslateNode();
  return (
    <div className={cx(styles.notice, styles[tone])} role={tone === 'danger' ? 'alert' : 'note'}>
      {title && <strong className={styles.noticeTitle}>{tr(title)}</strong>}
      {children && <div>{tr(children)}</div>}
    </div>
  );
}

/** Thẻ số liệu (dashboard, tổng quan). */
export function StatCard({ label, value, hint, tone, icon }: { label: string; value: ReactNode; hint?: ReactNode; tone?: 'brand' | 'warning' | 'danger'; icon?: IconName }) {
  const tr = useTranslateNode();
  return (
    <div className={cx(styles.stat, tone && styles[`stat_${tone}`])}>
      <div className={styles.statTop}>
        <span className={styles.statLabel}>{tr(label)}</span>
        {icon && <Icon name={icon} size={18} className={styles.statIcon} />}
      </div>
      <div className={styles.statValue}>{value}</div>
      {hint && <div className={styles.statHint}>{tr(hint)}</div>}
    </div>
  );
}

export function StatGrid({ children }: { children: ReactNode }) {
  return <div className={styles.statGrid}>{children}</div>;
}

interface FileDropProps {
  accept: string;
  /** 1 tệp (tệp đầu tiên). Bỏ qua khi có onFiles. */
  onFile?: (file: File) => void;
  /** Nhiều tệp cùng lúc (chọn / kéo thả nhiều). */
  onFiles?: (files: File[]) => void;
  title?: string;
  hint?: ReactNode;
  disabled?: boolean;
}

/** Vùng kéo-thả / chọn tệp. */
export function FileDrop({ accept, onFile, onFiles, title = 'Kéo & thả tệp vào đây hoặc bấm để chọn', hint, disabled }: FileDropProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [over, setOver] = useState(false);
  const tr = useTranslateNode();
  const take = (files: FileList | null) => {
    const list = files ? [...files] : [];
    if (!list.length) return;
    if (onFiles) onFiles(list);
    else onFile?.(list[0]!);
  };

  return (
    <div
      className={cx(styles.drop, over && styles.dropOver, disabled && styles.dropDisabled)}
      role="button"
      tabIndex={disabled ? -1 : 0}
      onClick={() => !disabled && inputRef.current?.click()}
      onKeyDown={e => (e.key === 'Enter' || e.key === ' ') && inputRef.current?.click()}
      onDragOver={e => {
        e.preventDefault();
        setOver(true);
      }}
      onDragLeave={() => setOver(false)}
      onDrop={e => {
        e.preventDefault();
        setOver(false);
        if (!disabled) take(e.dataTransfer.files);
      }}
    >
      <Icon name="upload" size={26} />
      <strong>{tr(title)}</strong>
      {hint && <span className={styles.dropHint}>{tr(hint)}</span>}
      <input
        ref={inputRef}
        type="file"
        accept={accept}
        multiple={!!onFiles}
        className="visually-hidden"
        tabIndex={-1}
        onChange={e => {
          take(e.target.files);
          e.target.value = '';
        }}
      />
    </div>
  );
}
