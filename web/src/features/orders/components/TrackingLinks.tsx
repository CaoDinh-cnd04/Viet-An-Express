import { useSession } from '@/features/auth';
import { customerTrackingUrl, MYTRACKING_URL } from '@/features/mytracking';
import { vaTrackingLink } from '@/shared/config/domain';
import { useI18n } from '@/shared/i18n';
import { useCopyToClipboard } from '@/shared/lib/useCopyToClipboard';
import { Icon } from '@/shared/ui';
import styles from './TrackingLinks.module.css';

/**
 * VA Track (thương hiệu Việt An) & Your Track (trang MyTracking của khách:
 * {MyTracking}/tracking/{mã khách}/{số bill}?ref={mã REF}) — mở link hoặc sao chép.
 */
export function TrackingLinks({ bill, refNo }: { bill: string; refNo?: string | null }) {
  const { t } = useI18n();
  const copy = useCopyToClipboard();
  const session = useSession();
  const customerCode = session.data?.status === 'authenticated' ? session.data.user.customerCode : '';
  const links = [
    { label: 'VA Track', url: vaTrackingLink(bill) },
    ...(customerCode ? [{ label: 'Your Track', url: customerTrackingUrl(MYTRACKING_URL, customerCode, bill, refNo ?? '') }] : [])
  ];

  return (
    <div className={styles.list}>
      {links.map(l => (
        <div key={l.label} className={styles.item}>
          <a href={l.url} target="_blank" rel="noreferrer" className={styles.link}>{l.label}</a>
          <button type="button" className={styles.copy} onClick={() => copy(l.url, t('Đã sao chép link {name}', { name: l.label }))} aria-label={t('Sao chép link {name}', { name: l.label })} title={t('Sao chép link')}>
            <Icon name="copy" size={13} />
          </button>
        </div>
      ))}
    </div>
  );
}
