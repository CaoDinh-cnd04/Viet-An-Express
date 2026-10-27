/** API công khai của feature — feature khác chỉ import từ đây. */
export { ImportantNoticeModal } from './components/ImportantNoticeModal';
export { NotificationBell } from './components/NotificationBell';
export { NoticeContent, RichBody } from './components/NoticeContent';
export { FilterBar, type FilterOption } from './components/FilterBar';
export { useMarkRead, useNotifications, notificationImageUrl, type NotificationItem } from './api';
export { NOTIFICATION_LINK_MAX, safeLink } from './lib/link';
export { editorValue, FONT_SIZES, plainToHtml, sanitizeHtml, sizeLabel, stepFontSize, TEXT_COLORS, toPlainText } from './lib/richText';
