import { useI18n } from '@/shared/i18n';
import { useCopyToClipboard } from '@/shared/lib/useCopyToClipboard';
import { Button, Icon } from '@/shared/ui';
import type { Publication } from '../api';
import { customerTrackingUrl, MYTRACKING_URL } from '../lib/link';
import styles from '../pages/MyTrackingPage.module.css';

/** Link công khai theo mã khách: {MyTracking}/tracking/{mã khách}/{số bill}?ref={mã REF} — hiện ngay sau khi Lưu. */
export function PublishPanel({ publication }: { publication: Publication }) {
  const { t } = useI18n();
  const copy = useCopyToClipboard();
  const code = publication.customerCode ?? '';
  const url = code ? customerTrackingUrl(MYTRACKING_URL, code) : '';
  const sample = code ? customerTrackingUrl(MYTRACKING_URL, code, '6174086', 'PO-123') : '';

  return <section className={`${styles.card} ${styles.publish}`}>
    <div className={styles.publishHead}>
      <h2>{t('Trang tra cứu công khai')}</h2>
    </div>
    <p className={styles.hint}>
      {t('Gửi link này cho người nhận hàng để họ tra cứu vận đơn trên trang mang thương hiệu của bạn. Thay đổi chỉ hiện trên link sau khi bấm Lưu.')}
    </p>
    <div className={styles.linkBox}>
      <code>{url || t('Đang tải mã khách…')}</code>
      <Button size="sm" disabled={!url} onClick={() => void copy(url, t('Đã sao chép link tra cứu'))}>
        <Icon name="copy" size={14} /> {t('Sao chép')}
      </Button>
      {url && <a className={styles.openLink} href={url} target="_blank" rel="noreferrer">{t('Mở trang')}</a>}
    </div>
    {sample && (
      <p className={styles.hint}>
        {t('Thêm số bill Việt An sau dấu "/" cuối, mã REF sau "?ref=". Ví dụ:')} <code>{sample}</code>
      </p>
    )}
  </section>;
}
