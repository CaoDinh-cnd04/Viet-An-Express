import { useEffect, useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { getErrorMessage, isNotImplemented } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { useCopyToClipboard } from '@/shared/lib/useCopyToClipboard';
import { Button, Icon, Notice, StatusPill } from '@/shared/ui';
import { useTracking } from '../api';
import { FloatingContact } from '../components/FloatingContact';
import { SiteFooter } from '../components/SiteFooter';
import { SiteHeader } from '../components/SiteHeader';
import { TrackingDetail } from '../components/TrackingDetail';
import { COMPANY, CONTACTS, TRACK_STATUS } from '../constants';
import { MAX_TRACK_BILLS, parseBills } from '../lib/tracking';
import { billsFromPath, trackingPath } from '../lib/tracking-detail';
import type { TrackResult } from '../types';
import styles from './TrackingPage.module.css';

/**
 * Trang kết quả tra cứu công khai: /tracking/MA1,MA2 (?awb=MA2 chọn mã đang xem).
 * Link dùng được để gửi cho người nhận — không cần đăng nhập.
 */
export default function TrackingPage() {
  const { t } = useI18n();
  const { bills: billsParam } = useParams();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const copy = useCopyToClipboard();

  const bills = billsFromPath(billsParam);
  const active = bills.includes(params.get('awb') ?? '') ? params.get('awb')! : bills[0];
  const tracking = useTracking(bills);
  const results = tracking.data;
  const current = results?.find(r => r.bill === active);

  useEffect(() => {
    const page = active ? t('Tra cứu {bill}', { bill: active }) : t('Tra cứu vận đơn');
    document.title = `${page} — ${COMPANY.name}`;
  }, [active, t]);

  const select = (bill: string) => void navigate(trackingPath(bills, bill), { replace: true });

  return (
    <div className={styles.page}>
      <SiteHeader onLogin={() => void navigate('/login')} />

      <section className={styles.hero}>
        <div className={styles.container}>
          <nav className={styles.crumbs} aria-label={t('Vị trí trang')}>
            <Link to="/">{t('Trang chủ')}</Link>
            <Icon name="chevronRight" size={14} />
            <span>{t('Tra cứu vận đơn')}</span>
          </nav>
          <h1 className={styles.title}>{t('Theo dõi vận đơn')}</h1>
          <p className={styles.lead}>{t('Nhập số vận đơn Việt An hoặc mã vận đơn của hãng để xem hành trình kiện hàng.')}</p>
          <SearchBar key={billsParam ?? ''} initial={bills} busy={tracking.isFetching}
            onSearch={next => void navigate(trackingPath(next))} />
        </div>
      </section>

      <main className={styles.container}>
        {bills.length === 0 ? (
          <div className={styles.placeholder}>
            <Icon name="search" size={28} />
            <p>{t('Nhập số vận đơn ở ô phía trên để bắt đầu tra cứu.')}</p>
          </div>
        ) : (
          <>
            <div className={styles.toolbar}>
              <p className={styles.summary}>
                {bills.length > 1 ? t('{n} vận đơn', { n: bills.length }) : t('Kết quả tra cứu')}
              </p>
              <div className={styles.tools}>
                <Button size="sm" onClick={() => void copy(window.location.href, t('Đã sao chép link tra cứu'))}>
                  {t('Chia sẻ link')}
                </Button>
                <Button size="sm" onClick={() => window.print()} disabled={!current?.found}>
                  {t('In')}
                </Button>
              </div>
            </div>

            <div className={cx(styles.layout, bills.length > 1 && styles.withList)}>
              {bills.length > 1 && (
                <BillList bills={bills} results={results} active={active} loading={tracking.isPending} onSelect={select} />
              )}

              <div className={styles.main}>
                {tracking.error ? (
                  <TrackingError error={tracking.error} />
                ) : tracking.isPending ? (
                  <div className={styles.skeleton} aria-busy="true" aria-label={t('Đang tra cứu')} />
                ) : current?.found ? (
                  <TrackingDetail key={current.bill} result={current} />
                ) : (
                  <NotFound bill={active ?? ''} />
                )}
                <HelpCard />
              </div>
            </div>
          </>
        )}
      </main>

      <SiteFooter />
      <FloatingContact />
    </div>
  );
}

function SearchBar({ initial, busy, onSearch }: { initial: string[]; busy: boolean; onSearch: (bills: string[]) => void }) {
  const { t } = useI18n();
  const [text, setText] = useState(() => initial.join(', '));
  const [error, setError] = useState<string>();

  const submit = (e: FormEvent) => {
    e.preventDefault();
    const { bills, invalid } = parseBills(text);
    if (bills.length === 0) {
      setError(invalid.length > 0 ? t('Số vận đơn không hợp lệ: {bills}', { bills: invalid.join(', ') }) : t('Nhập ít nhất 1 số vận đơn'));
      return;
    }
    setError(undefined);
    onSearch(bills);
  };

  return (
    <form className={styles.search} onSubmit={submit} noValidate role="search">
      <label htmlFor="tracking-input" className="visually-hidden">{t('Số vận đơn')}</label>
      <div className={cx(styles.searchBox, error && styles.searchInvalid)}>
        <Icon name="search" size={18} />
        <input
          id="tracking-input"
          value={text}
          onChange={e => setText(e.target.value)}
          placeholder={t('VD: 6165957 — nhiều số cách nhau bằng dấu phẩy')}
          autoComplete="off"
          spellCheck={false}
          aria-invalid={error ? true : undefined}
          aria-describedby="tracking-hint"
        />
        <Button variant="primary" type="submit" disabled={busy}>
          {t(busy ? 'Đang tra…' : 'Tra cứu')}
        </Button>
      </div>
      <p id="tracking-hint" className={cx(styles.hint, error && styles.hintError)}>
        {error ?? t('Tối đa {max} số mỗi lần', { max: MAX_TRACK_BILLS })}
      </p>
    </form>
  );
}

function BillList({ bills, results, active, loading, onSelect }: {
  bills: string[];
  results?: TrackResult[];
  active?: string;
  loading: boolean;
  onSelect: (bill: string) => void;
}) {
  const { t } = useI18n();
  return (
    <nav className={styles.list} aria-label={t('Danh sách vận đơn')}>
      {bills.map(bill => {
        const r = results?.find(x => x.bill === bill);
        const status = r?.found ? TRACK_STATUS[r.status] : undefined;
        return (
          <button key={bill} type="button" className={cx(styles.listItem, bill === active && styles.listActive)}
            aria-current={bill === active ? 'true' : undefined} onClick={() => onSelect(bill)}>
            <span className={styles.listBill}>{bill}</span>
            {loading ? (
              <span className={styles.listPending}>{t('Đang tra…')}</span>
            ) : status ? (
              <StatusPill tone={status.tone}>{status.label}</StatusPill>
            ) : (
              <StatusPill tone="neutral">{'Không tìm thấy'}</StatusPill>
            )}
            {r?.found && r.destination && <span className={styles.listMeta}><Icon name="globe" size={12} /> {r.destination}</span>}
          </button>
        );
      })}
    </nav>
  );
}

function NotFound({ bill }: { bill: string }) {
  const { t } = useI18n();
  return (
    <div className={styles.notFound}>
      <span className={styles.notFoundIcon}><Icon name="search" size={26} /></span>
      <h2>{t('Không tìm thấy vận đơn {bill}', { bill })}</h2>
      <p>
        {t('Vui lòng kiểm tra lại số vận đơn. Vận đơn mới tạo có thể cần vài giờ để hiện thông tin — hoặc gọi hotline')}{' '}
        <a href={CONTACTS.hotline.href}>{CONTACTS.hotline.label}</a> {t('để được hỗ trợ.')}
      </p>
    </div>
  );
}

function TrackingError({ error }: { error: unknown }) {
  const { t } = useI18n();
  return isNotImplemented(error) ? (
    <Notice title="Tra cứu vận đơn đang được kết nối máy chủ">
      {t('Vui lòng gọi')} <a href={CONTACTS.phone.href}>{CONTACTS.phone.label}</a> {t('hoặc hotline')}{' '}
      <a href={CONTACTS.hotline.href}>{CONTACTS.hotline.label}</a> {t('để được hỗ trợ.')}
    </Notice>
  ) : (
    <Notice tone="danger" title="Không tra cứu được">
      {t(getErrorMessage(error, 'Không kết nối được máy chủ')).replace(/[.!]$/, '')}. {t('Vui lòng thử lại sau hoặc gọi hotline')}{' '}
      <a href={CONTACTS.hotline.href}>{CONTACTS.hotline.label}</a>.
    </Notice>
  );
}

function HelpCard() {
  const { t } = useI18n();
  return (
    <aside className={styles.help}>
      <div>
        <p className={styles.helpTitle}>{t('Cần hỗ trợ về vận đơn?')}</p>
        <p className={styles.helpText}>{t('Đội chăm sóc khách hàng Việt An sẵn sàng hỗ trợ bạn.')}</p>
      </div>
      <div className={styles.helpActions}>
        <a className={styles.helpLink} href={CONTACTS.hotline.href}><Icon name="phone" size={16} /> {CONTACTS.hotline.label}</a>
        <a className={styles.helpLink} href={CONTACTS.zalo.href} target="_blank" rel="noreferrer"><Icon name="message" size={16} /> Chat Zalo</a>
      </div>
    </aside>
  );
}
