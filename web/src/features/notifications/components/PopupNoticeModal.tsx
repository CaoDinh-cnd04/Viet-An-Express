import { useMemo, useState } from 'react';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { loginMark } from '@/shared/lib/login-mark';
import { Button, Modal } from '@/shared/ui';
import { usePopupNotice } from '../api';
import { LEGACY_SITE_URL } from '../constants';
import { popupSignature } from '../lib/popup';
import { sanitizeHtml } from '../lib/richText';
import contentStyles from './NoticeContent.module.css';
import styles from './notifications.module.css';

/**
 * Popup đã đóng: "{mốc đăng nhập}|{dấu nội dung}" trong sessionStorage — tải lại trang (F5) không hiện lại;
 * đăng nhập lần sau (mốc mới), tắt tab / trình duyệt rồi vào lại (sessionStorage bị xóa) hoặc CRM sửa nội dung thì hiện.
 */
const CLOSED_KEY = 'va.popupNotice.closed';
const readClosed = (): string | null => {
  try {
    return window.sessionStorage.getItem(CLOSED_KEY);
  } catch {
    return null;
  }
};

/**
 * Trạng thái thông báo popup của hệ thống cũ: nhân viên soạn ở CRM (Nội dung › Thông báo popup — TConfig ThongBao_NoiDung, bật bằng
 * ThongBao_Show). Hiện khi vừa đăng nhập hoặc mở lại trình duyệt / tab, F5 không hiện (người dùng chốt 30/10/2026); HTML được lọc (giữ định dạng, liên kết, ảnh an toàn).
 * `waiting` = đang tải — popup khác chờ để không bị popup này đè lên sau.
 */
export function useLegacyPopup() {
  const { data, isPending } = usePopupNotice();
  const [closed, setClosed] = useState(readClosed);

  const html = data?.show ? (data.html ?? '') : '';
  const clean = useMemo(() => (html ? sanitizeHtml(html, { media: true, base: LEGACY_SITE_URL }) : ''), [html]);
  const stamp = useMemo(() => (html ? `${loginMark()}|${popupSignature(html)}` : ''), [html]);

  const close = () => {
    try {
      window.sessionStorage.setItem(CLOSED_KEY, stamp);
    } catch {
      /* storage bị chặn: chỉ nhớ tới khi tải lại trang */
    }
    setClosed(stamp);
  };

  return { open: !!clean.trim() && closed !== stamp, clean, close, waiting: isPending };
}

export type LegacyPopup = ReturnType<typeof useLegacyPopup>;

export function PopupNoticeModal({ popup: { open, clean, close } }: { popup: LegacyPopup }) {
  const { t } = useI18n();
  return (
    <Modal
      open={open}
      title={t('Thông báo')}
      size="lg"
      onClose={close}
      footer={<Button variant="primary" onClick={close}>{t('Đóng')}</Button>}
    >
      <div className={cx(contentStyles.body, contentStyles.rich, styles.popupHtml)} dangerouslySetInnerHTML={{ __html: clean }} />
    </Modal>
  );
}
