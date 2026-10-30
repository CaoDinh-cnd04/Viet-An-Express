import { ImportantNoticeModal } from './ImportantNoticeModal';
import { PopupNoticeModal, useLegacyPopup } from './PopupNoticeModal';

/**
 * Popup sau khi đăng nhập hiện lần lượt, không đè nhau: thông báo của hệ thống cũ (CRM, TConfig) trước,
 * đóng xong mới tới tin quan trọng của portal (TThongBao).
 */
export function LoginNotices() {
  const popup = useLegacyPopup();
  return (
    <>
      <PopupNoticeModal popup={popup} />
      <ImportantNoticeModal paused={popup.waiting || popup.open} />
    </>
  );
}
