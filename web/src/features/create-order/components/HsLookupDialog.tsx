import { useMemo, useState } from 'react';
import { isNotImplemented } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { useCopyToClipboard } from '@/shared/lib/useCopyToClipboard';
import { Button, Icon, Modal } from '@/shared/ui';
import { useCategories, useHsLookup, useProductLibrary, useRecentInvoices, type HsLookupItem } from '../api';
import { formatHs, groupByHs6, HS_COUNTRIES, hs6Of, type HsCountry } from '../lib/hs-lookup';
import { searchHs } from '../lib/quick-lookup';
import styles from './HsLookupDialog.module.css';

type Mode = 'verified' | 'catalog';

/**
 * Popup tra mã HS theo nước nhập khẩu (mở từ khung Hỗ trợ khách hàng), dữ liệu dbo.Catalog_HsCode:
 * - "Tra danh mục mã" (mặc định): tra toàn bộ biểu thuế của nước đó theo tên / số.
 * - "Mặt hàng đã xác minh": chỉ mã Việt An đã rà soát từ hàng gửi thực tế.
 * Kết quả gom theo HS6; đường dẫn biểu thuế chung của nhóm ở tiêu đề, phần riêng ở từng dòng mã.
 * Máy chủ chưa có dữ liệu theo nước → tạm hiện HS6 từ nhóm hàng, thư viện mặt hàng, invoice gần đây (dữ liệu thật đang có).
 */
export function HsLookupDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { t } = useI18n();
  const copy = useCopyToClipboard();
  const [mode, setMode] = useState<Mode>('catalog');
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
  const groups = useMemo(() => groupByHs6(items), [items]);
  const meta = HS_COUNTRIES.find(c => c.code === country)!;
  const search = () => setQuery(input.trim());

  return (
    <Modal open={open} title="Tra cứu mã HS" size="full" onClose={onClose}>
      <div className={styles.wrap}>
        <div className={styles.intro}>
          <h3>{t('Tra mã HS theo nước nhập khẩu')}</h3>
          <p>{t('Mã 10 số cho Hoa Kỳ (HTSUS), Anh (UK Tariff) và 8/10 số cho Úc (ABF), kèm vị trí dòng mã trong biểu thuế. Gõ tên hàng tiếng Anh (có thể không dấu) hoặc vài số đầu của mã.')}</p>
        </div>

        <div className={styles.modes} role="tablist">
          {(['catalog', 'verified'] as const).map(m => (
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
            <input autoFocus value={input} onChange={e => setInput(e.target.value)} placeholder={t('VD: dried mango, t-shirt, 0804, 6109.10')} />
          </label>
          <Button type="submit" variant="primary" disabled={input.trim().length < 2}>
            <Icon name="search" size={15} /> {t('Tra cứu')}
          </Button>
        </form>

        <div className={styles.notice}>
          <p><b>{t('Lưu ý:')}</b> {t('Mã HS dưới đây lấy từ biểu thuế của nước nhập khẩu, chỉ để tham khảo. Cùng một tên hàng nhưng khác chất liệu, công dụng hay cách đóng gói có thể ra mã khác. Bạn cần tự đối chiếu với hàng thực tế và chịu trách nhiệm về mã khai trên tờ khai. Chưa chắc chắn, hãy hỏi nhân viên trước khi khai.')}</p>
          <p><b>{t('Thuế nhập khẩu')}</b> {t('không hiển thị ở đây vì còn phụ thuộc xuất xứ, giá trị lô và quy định từng thời điểm. Hãy hỏi nhân viên để được tư vấn trước khi gửi.')}</p>
        </div>

        {fallback && query && (
          <p className={styles.pending}>{t('Dữ liệu mã {tariff} theo nước đang được cập nhật — tạm hiện mã HS6 từ nhóm hàng và các mặt hàng bạn đã khai.', { tariff: meta.tariff })}</p>
        )}

        <div className={styles.results}>
          {lookup.isFetching && <p className={styles.muted}>{t('Đang tra cứu…')}</p>}
          {lookup.isError && !fallback && <p className={styles.muted}>{t('Không tra cứu được, vui lòng thử lại.')}</p>}
          {query && !lookup.isFetching && (fallback || lookup.isSuccess) && !items.length && (
            mode === 'verified' && !fallback ? (
              <p className={styles.muted}>
                {t('Chưa có mặt hàng đã xác minh phù hợp.')}{' '}
                <button type="button" className={styles.linkButton} onClick={() => setMode('catalog')}>{t('Tra trong toàn bộ biểu thuế')}</button>
              </p>
            ) : (
              <p className={styles.muted}>{t('Không tìm thấy mặt hàng phù hợp. Hãy thử tên khác hoặc hỏi nhân viên Việt An.')}</p>
            )
          )}
          {items.length > 0 && <p className={styles.count}>{t('{n} mã trong {g} nhóm HS6', { n: items.length, g: groups.length })}</p>}
          {groups.map(g => (
            <section key={g.hs6} className={styles.group}>
              <header className={styles.groupHead}>
                <span className={styles.label}>HS6</span>
                <code className={styles.code}>{g.hs6}</code>
                <button type="button" className={styles.copy} onClick={() => void copy(g.hs6, t('Đã sao chép HS {hs}', { hs: g.hs6 }))}><Icon name="copy" size={13} /> {t('Chép')}</button>
                {g.path.length > 0 && <p className={styles.path}>{g.path.join(' › ')}</p>}
              </header>
              <ul className={styles.lines}>
                {g.lines.map(({ item: it, rest }, i) => (
                  <li key={`${it.nationalCode ?? ''}|${it.nameEn}|${i}`} className={styles.line}>
                    <div className={styles.lineText}>
                      {rest.length > 0 && <small className={styles.path}>{rest.join(' › ')}</small>}
                      <strong>{it.nameVi || it.nameEn}</strong>
                      {it.nameVi && it.nameEn && <span className={styles.sub}>{it.nameEn}</span>}
                      {it.isVerified && <span className={styles.verified}><Icon name="check" size={12} /> {t('Việt An đã xác minh')}</span>}
                      {it.note && <p className={styles.note}>{it.note}</p>}
                    </div>
                    <div className={styles.lineCode}>
                      <span className={styles.label}>{meta.tariff}</span>
                      {it.nationalCode ? (
                        <>
                          <code className={styles.code}>{formatHs(it.nationalCode)}</code>
                          <button type="button" className={styles.copy} onClick={() => void copy(formatHs(it.nationalCode), t('Đã sao chép HS {hs}', { hs: formatHs(it.nationalCode) }))}><Icon name="copy" size={13} /> {t('Chép')}</button>
                        </>
                      ) : (
                        <span className={styles.muted}>{t('Chưa có mã {digits} của {country}', { digits: t(meta.digits), country: t(meta.label) })}</span>
                      )}
                      {it.unit && <small className={styles.unit}>{t('Đơn vị tính: {unit}', { unit: it.unit })}</small>}
                    </div>
                  </li>
                ))}
              </ul>
            </section>
          ))}
        </div>
      </div>
    </Modal>
  );
}
