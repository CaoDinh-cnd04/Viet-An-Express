import { useMemo, useState } from 'react';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Button, Modal } from '@/shared/ui';
import { usePopupNotice } from '../api';
import { popupSignature } from '../lib/popup';
import { sanitizeHtml } from '../lib/richText';
import contentStyles from './NoticeContent.module.css';
import styles from './notifications.module.css';

/** Dấu nội dung popup đã đóng trong phiên làm việc này — không hiện lại tới khi đóng tab; CRM sửa nội dung thì hiện lại. */
const CLOSED_KEY = 'va.popupNotice.closed';
const readClosed = (): string | null => {
  try {
    return window.sessionStorage.getItem(CLOSED_KEY);
  } catch {
    return null;
  }
};

/**
 * Thông báo popup của hệ thống cũ: nhân viên soạn ở CRM (Nội dung › Thông báo popup — TConfig ThongBao_NoiDung, bật bằng
 * ThongBao_Show). Hiện 1 lần mỗi phiên sau khi đăng nhập; HTML được lọc (giữ định dạng, liên kết, ảnh an toàn).
 */
export function PopupNoticeModal() {
  const { t } = useI18n();
  const { data } = usePopupNotice();
  const [closed, setClosed] = useState(readClosed);

  const html = data?.show ? (data.html ?? '') : '';
  const clean = useMemo(() => (html ? sanitizeHtml(html, { media: true }) : ''), [html]);
  const signature = useMemo(() => (html ? popupSignature(html) : ''), [html]);

  const close = () => {
    try {
      window.sessionStorage.setItem(CLOSED_KEY, signature);
    } catch {
      /* storage bị chặn: chỉ nhớ tới khi tải lại trang */
    }
    setClosed(signature);
  };

  return (
    <Modal
      open={!!clean.trim() && closed !== signature}
      title={t('Thông báo')}
      size="lg"
      onClose={close}
      footer={<Button variant="primary" onClick={close}>{t('Đóng')}</Button>}
    >
      <div className={cx(contentStyles.body, contentStyles.rich, styles.popupHtml)} dangerouslySetInnerHTML={{ __html: clean }} />
    </Modal>
  );
}
