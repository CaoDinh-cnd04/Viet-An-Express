import { useState } from 'react';
import { useI18n } from '@/shared/i18n';
import { Button, Modal } from '@/shared/ui';
import source from '../content/dieu-khoan-dich-vu.txt?raw';
import { parseTerms } from '../lib/parse-terms';
import styles from './TermsDialog.module.css';

/** Văn bản điều khoản (đọc 1 lần khi tải module). Sửa nội dung: sửa file content/dieu-khoan-dich-vu.txt. */
export const TERMS = parseTerms(source);

interface TermsDialogProps {
  open: boolean;
  onClose: () => void;
  /** Có thì hộp thoại ở chế độ xác nhận: khách phải tick đồng ý mới bấm được nút. */
  onAccept?: () => void;
  /** Nhãn nút xác nhận (mặc định "Đồng ý & in bill"). */
  acceptLabel?: string;
}

/** Điều khoản dịch vụ dạng văn bản — chỉ xem, hoặc bắt khách đồng ý trước khi In & cấp bill. */
export function TermsDialog({ open, onClose, onAccept, acceptLabel = 'Đồng ý & in bill' }: TermsDialogProps) {
  const { t } = useI18n();
  const [agreed, setAgreed] = useState(false);
  // Chỉ gọi onClose khi khách tự đóng (Hủy / X / Esc). Cha tắt hộp thoại sau khi đồng ý cũng làm <dialog> phát sự kiện close
  // — lúc đó open đã là false nên bỏ qua, tránh vừa đồng ý vừa bị coi như từ chối.
  const close = () => {
    if (!open) return;
    setAgreed(false);
    onClose();
  };

  return (
    <Modal
      open={open}
      size="lg"
      title="Điều khoản dịch vụ"
      onClose={close}
      footer={
        onAccept ? (
          <div className={styles.accept}>
            <label className={styles.agree}>
              <input type="checkbox" checked={agreed} onChange={e => setAgreed(e.target.checked)} />
              <span>{t('Tôi đã đọc và đồng ý với Điều khoản dịch vụ của Việt An Express')}</span>
            </label>
            <div className={styles.buttons}>
              <Button size="sm" onClick={close}>{t('Hủy')}</Button>
              <Button
                size="sm"
                variant="primary"
                disabled={!agreed}
                onClick={() => {
                  setAgreed(false);
                  onAccept();
                }}
              >
                {t(acceptLabel)}
              </Button>
            </div>
          </div>
        ) : (
          <Button size="sm" onClick={close}>{t('Đóng')}</Button>
        )
      }
    >
      <article className={styles.doc}>
        <h3 className={styles.title}>{TERMS.title}</h3>
        {TERMS.intro.map(line => <p key={line} className={styles.intro}>{line}</p>)}
        {TERMS.sections.map(s => (
          <section key={s.heading}>
            <h4 className={styles.heading}>{s.heading}</h4>
            {s.clauses.map((c, i) => (
              <p key={`${c.no}-${i}`} className={styles.clause}>
                {c.no && <b>{c.no} </b>}
                {c.text}
              </p>
            ))}
          </section>
        ))}
        {TERMS.footer && <p className={styles.footer}>{TERMS.footer}</p>}
      </article>
    </Modal>
  );
}

/** Nút mở Điều khoản dịch vụ (chỉ xem). */
export function TermsButton({ size = 'sm' }: { size?: 'sm' | 'md' }) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  return (
    <>
      <Button size={size} onClick={() => setOpen(true)}>{t('Điều khoản dịch vụ')}</Button>
      <TermsDialog open={open} onClose={() => setOpen(false)} />
    </>
  );
}
