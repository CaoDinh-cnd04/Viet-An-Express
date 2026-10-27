import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { parseBills } from '@/features/landing';
import { getErrorMessage } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { Icon, Notice } from '@/shared/ui';
import { useCustomerTracking, usePublicMyTracking } from '../api';
import { customerTrackingQuery } from '../lib/link';
import { ClassicTrackingDetail } from '../components/ClassicTrackingDetail';
import { HomeButton, TrackingLayout } from '../components/TrackingLayout';
import layout from './MyTrackingPage.module.css';
import styles from './PublicMyTrackingPage.module.css';

/**
 * Trang tra cứu MyTracking công khai của khách, không cần đăng nhập — khách gửi link cho người nhận hàng:
 * /tracking/{mã khách}/{số bill}?ref={mã REF}: chỉ tra đơn của khách đó; không có số bill thì tra theo mã REF.
 * Trên tên miền MyTracking, /tracking/{mã khách} (chưa có số bill) cũng vào đây — `customerCode` truyền từ route.
 */
export default function PublicMyTrackingPage({ customerCode }: { customerCode?: string }) {
  const { t } = useI18n();
  const { code = customerCode ?? '', bill } = useParams();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const page = usePublicMyTracking(code);
  const bills = customerTrackingQuery(bill, params.get('ref'));
  const tracking = useCustomerTracking(code, bills);
  const config = page.data?.config;

  useEffect(() => {
    if (config) document.title = config.brand.companyName || config.title;
  }, [config]);

  if (page.isPending) return <p className={styles.center}>{t('Đang tải…')}</p>;
  if (!config) {
    return (
      <div className={styles.center}>
        <div className={styles.missing}>
          <Icon name="search" size={28} />
          <h1>{t('Không tìm thấy trang tra cứu')}</h1>
          <p>{t('Trang tra cứu không tồn tại. Vui lòng kiểm tra lại đường dẫn.')}</p>
        </div>
      </div>
    );
  }

  const search = (next: string[]) =>
    navigate(`/tracking/${encodeURIComponent(page.data?.customerCode ?? code)}/${next.map(encodeURIComponent).join(',')}`);

  return (
    <div className={styles.page}>
      <TrackingLayout config={config} standalone>
        <SearchForm key={bills.join(',')} initial={bills} busy={tracking.isFetching} onSearch={search} website={config.brand.website} />
        <h2 className={layout.blackBar}>TRACKING DETAIL</h2>
        {bills.length === 0 ? (
          <div className={layout.trackingPlaceholder}>
            <Icon name="box" size={26} />
            <p>{t('Nhập số vận đơn để xem hành trình kiện hàng.')}</p>
          </div>
        ) : tracking.error ? (
          <Notice tone="danger" title="Không tra cứu được">{t(getErrorMessage(tracking.error, 'Không kết nối được máy chủ'))}</Notice>
        ) : tracking.isPending ? (
          <div className={layout.trackingPlaceholder} aria-busy="true"><p>{t('Đang tra cứu…')}</p></div>
        ) : (
          <div className={styles.results}>
            {bills.map(bill => {
              const result = tracking.data?.find(r => r.bill === bill);
              return result?.found
                ? <ClassicTrackingDetail key={bill} result={result} />
                : <p key={bill} className={styles.notFound}>{t('Không tìm thấy vận đơn {bill}', { bill })}</p>;
            })}
          </div>
        )}
      </TrackingLayout>
      <p className={styles.powered}>{t('Vận chuyển bởi')} <a href="/" target="_blank" rel="noreferrer">Việt An Express</a></p>
    </div>
  );
}

function SearchForm({ initial, busy, onSearch, website }: {
  initial: string[];
  busy: boolean;
  onSearch: (bills: string[]) => void;
  website: string;
}) {
  const { t } = useI18n();
  const [text, setText] = useState(() => initial.join(', '));
  const [error, setError] = useState<string>();

  const submit = (e: FormEvent) => {
    e.preventDefault();
    const { bills, invalid } = parseBills(text);
    if (bills.length === 0) {
      setError(invalid.length ? t('Số vận đơn không hợp lệ: {bills}', { bills: invalid.join(', ') }) : t('Nhập ít nhất 1 số vận đơn'));
      return;
    }
    setError(undefined);
    onSearch(bills);
  };

  return (
    <form role="search" onSubmit={submit} noValidate>
      <div className={layout.trackingSearch}>
        <input value={text} onChange={e => setText(e.target.value)} aria-label="Tracking number" placeholder="Tracking number"
          autoComplete="off" spellCheck={false} aria-invalid={error ? true : undefined} />
        <button type="submit" disabled={busy}>Track</button>
        <HomeButton website={website} />
      </div>
      {error && <p className={styles.error} role="alert">{error}</p>}
    </form>
  );
}
