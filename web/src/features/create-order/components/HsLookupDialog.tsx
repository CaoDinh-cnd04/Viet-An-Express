import { useMemo, useState } from 'react';
import { isNotImplemented } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { useCopyToClipboard } from '@/shared/lib/useCopyToClipboard';
import { Button, Icon, Modal } from '@/shared/ui';
import { useCategories, useHsLookup, useProductLibrary, useRecentInvoices, type HsLookupItem } from '../api';
import { formatHs, HS_COUNTRIES, hs6Of, type HsCountry } from '../lib/hs-lookup';
import { searchHs } from '../lib/quick-lookup';
import styles from './HsLookupDialog.module.css';

type Mode = 'verified' | 'catalog';

/**
 * Popup tra mã HS theo nước nhập khẩu (mở từ khung Hỗ trợ khách hàng):
 * - "Mặt hàng đã xác minh": mặt hàng Việt An đã gửi thực tế, kèm HS6 + mã đầy đủ của nước nhập khẩu và mô tả dòng mã.
 * - "Tra danh mục mã": tra biểu thuế của nước đó theo tên / số.
 * Máy chủ chưa có dữ liệu theo nước → tạm hiện HS6 từ nhóm hàng, thư viện mặt hàng, invoice gần đây (dữ liệu thật đang có).
 */
export function HsLookupDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { t } = useI18n();
  const copy = useCopyToClipboard();
  const [mode, setMode] = useState<Mode>('verified');
  const [country, setCountry] = useState<HsCountry>('US');
  const [input, setInput] = useState('');
  const [query, setQuery] = useState('');
  const lookup = useHsLookup(country, query, mode);
  const fallback = lookup.isError && isNotImplemented(lookup.error);

  // Dữ liệu tạm khi chưa có endpoint: HS6 từ dữ liệu sẵn có của khách
  const categories = useCategories();
  const products = useProductLibrary(open && fallback);
  const invoices = useRecentInvoices(open && fallback);
  const fallbackItems = useMemo<HsLookupItem[]>(
    () =>
      !fallback
        ? []
        : searchHs(query, [
            ...(categories.data ?? []).map(c => ({ source: c.name, items: c.suggestions })),
            { source: 'products', items: (products.data ?? []).map(p => ({ hs: p.hs, en: p.descEn, vi: p.descVi })) },
            { source: 'invoices', items: (invoices.data ?? []).flatMap(i => i.items.map(p => ({ hs: p.hs, en: p.descEn, vi: p.descVi }))) }
          ], 20).map(h => ({ nameVi: h.vi, nameEn: h.en, hs6: hs6Of(h.hs) || formatHs(h.hs) })),
    [fallback, query, categories.data, products.data, invoices.data]
  );

  const items = fallback ? fallbackItems : lookup.data ?? [];
  const meta = HS_COUNTRIES.find(c => c.code === country)!;
  const search = () => setQuery(input.trim());

  return (
    <Modal open={open} title="Tra cứu mã HS" size="full" onClose={onClose}>
      <div className={styles.wrap}>
        <div className={styles.intro}>
          <h3>{t('Tra mã HS đã xác minh theo nước nhập khẩu')}</h3>
          <p>{t('Mã 10 số cho Hoa Kỳ (HTSUS), Anh (UK Tariff) và 8/10 số cho Úc (ABF), kèm mô tả dòng mã. Dữ liệu do Việt An Express rà soát từ các mặt hàng đã gửi thực tế.')}</p>
        </div>

        <div className={styles.modes} role="tablist">
          {(['verified', 'catalog'] as const).map(m => (
            <button key={m} type="button" role="tab" aria-selected={mode === m} className={cx(styles.mode, mode === m && styles.modeOn)} onClick={() => setMode(m)}>
              {t(m === 'verified' ? 'Mặt hàng đã xác minh' : 'Tra danh mục mã')}
            </button>
          ))}
        </div>

        <form className={styles.form} onSubmit={e => { e.preventDefault(); search(); }}>
          <label className={styles.field}>
            <span>{t('Nước nhập khẩu')}</span>
            <select value={country} onChange={e => setCountry(e.target.value as HsCountry)}>
              {HS_COUNTRIES.map(c => <option key={c.code} value={c.code}>{t(c.label)}</option>)}
            </select>
          </label>
          <label className={cx(styles.field, styles.grow)}>
            <span>{t(mode === 'verified' ? 'Tên hàng (tiếng Việt/Anh) hoặc mã số' : 'Mã số hoặc mô tả trong biểu thuế')}</span>
            <input autoFocus value={input} onChange={e => setInput(e.target.value)} placeholder={t('VD: xoài sấy, dried mango, 0804')} />
          </label>
          <Button type="submit" variant="primary" disabled={input.trim().length < 2}>
            <Icon name="search" size={15} /> {t('Tra cứu')}
          </Button>
        </form>

        <div className={styles.notice}>
          <p><b>{t('Lưu ý:')}</b> {t('Mã HS dưới đây chỉ để tham khảo, lấy từ các lô Việt An Express đã gửi. Cùng một tên hàng nhưng khác chất liệu, công dụng hay cách đóng gói có thể ra mã khác. Bạn cần tự đối chiếu với hàng thực tế và chịu trách nhiệm về mã khai trên tờ khai. Chưa chắc chắn, hãy hỏi nhân viên trước khi khai.')}</p>
          <p><b>{t('Thuế nhập khẩu')}</b> {t('không hiển thị ở đây vì còn phụ thuộc xuất xứ, giá trị lô và quy định từng thời điểm. Hãy hỏi nhân viên để được tư vấn trước khi gửi.')}</p>
        </div>

        {fallback && query && (
          <p className={styles.pending}>{t('Dữ liệu mã {tariff} theo nước đang được cập nhật — tạm hiện mã HS6 từ nhóm hàng và các mặt hàng bạn đã khai.', { tariff: meta.tariff })}</p>
        )}

        <div className={styles.results}>
          {lookup.isFetching && <p className={styles.muted}>{t('Đang tra cứu…')}</p>}
          {lookup.isError && !fallback && <p className={styles.muted}>{t('Không tra cứu được, vui lòng thử lại.')}</p>}
          {query && !lookup.isFetching && (fallback || lookup.isSuccess) && !items.length && (
            <p className={styles.muted}>{t('Không tìm thấy mặt hàng phù hợp. Hãy thử tên khác hoặc hỏi nhân viên Việt An.')}</p>
          )}
          {items.map((it, i) => (
            <article key={`${it.hs6}|${it.nameEn}|${i}`} className={styles.card}>
              <header>
                <h4>{it.nameVi || it.nameEn}</h4>
                {it.nameVi && it.nameEn && <p>{it.nameEn}</p>}
              </header>
              <dl className={styles.codes}>
                <dt>HS6</dt>
                <dd>
                  <code>{formatHs(it.hs6)}</code>
                  <button type="button" onClick={() => void copy(formatHs(it.hs6), t('Đã sao chép HS {hs}', { hs: formatHs(it.hs6) }))}><Icon name="copy" size={13} /> {t('Chép')}</button>
                </dd>
                <dt>{meta.tariff}</dt>
                <dd>
                  {it.nationalCode ? (
                    <>
                      <code>{formatHs(it.nationalCode)}</code>
                      <button type="button" onClick={() => void copy(formatHs(it.nationalCode), t('Đã sao chép HS {hs}', { hs: formatHs(it.nationalCode) }))}><Icon name="copy" size={13} /> {t('Chép')}</button>
                      {it.nationalDescription && <small>{it.nationalDescription}</small>}
                    </>
                  ) : (
                    <span className={styles.muted}>{t('Chưa có mã {digits} của {country}', { digits: t(meta.digits), country: t(meta.label) })}</span>
                  )}
                </dd>
              </dl>
              {it.note && <p className={styles.note}>{it.note}</p>}
            </article>
          ))}
        </div>
      </div>
    </Modal>
  );
}
