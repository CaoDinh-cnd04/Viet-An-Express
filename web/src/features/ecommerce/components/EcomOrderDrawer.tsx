import { useEffect, useState, type ReactNode } from 'react';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { formatMoney, formatNumber } from '@/shared/lib/format';
import { useCopyToClipboard } from '@/shared/lib/useCopyToClipboard';
import { Button, Icon, Notice, Sheet, SheetBox, SheetFields, SheetLines, SheetName, SheetTable, SheetTotals, StatusPill } from '@/shared/ui';
import { ECOM_SOURCES } from '../constants';
import { displayStatus, receiverLines } from '../lib/order-view';
import type { EcomOrder } from '../types';
import { EcomOrderEditForm } from './EcomOrderEditForm';
import { PrintMenu } from './PrintMenu';
import styles from './EcomOrderDrawer.module.css';

interface DrawerProps {
  order: EcomOrder | null;
  onClose: () => void;
  onDelete: (o: EcomOrder) => void;
  /** yes = xác nhận gửi (sang Đơn hàng E-com), no = trả về tab Đơn hàng */
  onConfirm: (o: EcomOrder, yes: boolean) => void;
  /** Có quyền tạo & xử lý đơn (xác nhận, sửa, xóa) — tài khoản con chỉ xem thì ẩn các nút này. */
  canProcess: boolean;
  confirming: boolean;
}

/** 3 bước của đơn E-commerce — hiện ở đầu ngăn để biết đơn đang ở đâu. */
const STEPS = ['Mới về', 'Đã xác nhận gửi', 'Đã có bill'] as const;
const stepOf = (o: EcomOrder) => (o.bill ? 2 : o.confirmed ? 1 : 0);

/**
 * Ngăn chi tiết 1 đơn E-commerce: bước hiện tại, tóm tắt (người nhận, sản phẩm, giá trị, cân), thao tác (xác nhận / trả về, in, sửa, xóa),
 * rồi phiếu chi tiết. Đơn chưa có bill thì sửa được ngay trong ngăn.
 */
export function EcomOrderDrawer({ order, onClose, onDelete, onConfirm, confirming, canProcess }: DrawerProps) {
  const { t } = useI18n();
  const copy = useCopyToClipboard();
  const [editing, setEditing] = useState(false);

  // Mở đơn khác → về chế độ xem.
  useEffect(() => setEditing(false), [order?.id]);

  useEffect(() => {
    if (!order) return;
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose();
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [order, onClose]);

  if (!order) return null;
  const o = order;
  const r = o.receiver;
  const status = displayStatus(o);
  const step = stepOf(o);
  const products = o.products ?? [];
  const hasHs = products.some(p => p.hsCode);
  const money = (n: number | null | undefined) => formatMoney(n, o.currency);
  const itemsTotal = products.reduce((sum, p) => sum + p.sellingPrice * p.qty, 0);
  const issues = o.issues ?? [];
  const extra = [
    ['VA Bill', o.bill ? <span className="mono">{o.bill}</span> : ''],
    ['Xác nhận gửi lúc', o.confirmedAt ?? ''],
    ['Dịch vụ', [o.service, o.hub].filter(Boolean).join(' · ')],
    ['Chi nhánh gửi', o.branch ?? '']
  ].filter(([, v]) => v !== '') as Array<[string, ReactNode]>;

  return (
    <>
      <div className={styles.scrim} onClick={onClose} />
      <aside className={styles.drawer} role="dialog" aria-modal="true" aria-labelledby="ecom-drawer-title">
        <header className={styles.header}>
          <div className={styles.headMain}>
            <p className={styles.kicker}>{t('Đơn {source}', { source: t(ECOM_SOURCES[o.src]?.label ?? o.src) })} · {o.createdAt}</p>
            <h2 id="ecom-drawer-title" className={styles.title}>
              {o.ref}
              <button type="button" className={styles.iconBtn} onClick={() => void copy(o.ref, t('Đã sao chép mã đơn'))} aria-label={t('Sao chép mã đơn')} title={t('Sao chép mã đơn')}>
                <Icon name="copy" size={14} />
              </button>
              <StatusPill tone={status.tone}>{t(status.label)}</StatusPill>
            </h2>
          </div>
          <button type="button" className={styles.close} onClick={onClose} aria-label={t('Đóng')}>
            <Icon name="close" size={16} />
          </button>
        </header>

        <ol className={styles.steps} aria-label={t('Tiến trình đơn')}>
          {STEPS.map((s, i) => (
            <li key={s} className={cx(i < step && styles.stepDone, i === step && styles.stepCurrent)} aria-current={i === step ? 'step' : undefined}>
              <span className={styles.stepDot}>{i < step ? <Icon name="check" size={12} /> : i + 1}</span>
              {t(s)}
            </li>
          ))}
        </ol>

        <dl className={styles.summary}>
          <div>
            <dt>{t('Người nhận')}</dt>
            <dd>{r?.name || o.cnee || '—'}<span>{r?.country ?? o.ct}</span></dd>
          </div>
          <div>
            <dt>{t('Sản phẩm')}</dt>
            <dd>{t('{n} SP', { n: o.items })}</dd>
          </div>
          <div>
            <dt>{t('Giá trị đơn')}</dt>
            <dd>{money(o.value) || '—'}</dd>
          </div>
          <div>
            <dt>{t('Cân nặng')}</dt>
            <dd>{o.kg ? `${formatNumber(o.kg)} kg` : <span className={styles.mutedValue}>{t('Việt An cân')}</span>}</dd>
          </div>
        </dl>

        {!editing && (
          <div className={styles.toolbar}>
            {o.editable && canProcess && (o.confirmed ? (
              <Button size="sm" disabled={confirming} onClick={() => onConfirm(o, false)}>{t('Trả về Đơn hàng')}</Button>
            ) : (
              <Button size="sm" variant="primary" disabled={confirming || issues.length > 0} onClick={() => onConfirm(o, true)}>
                <Icon name="send" size={15} /> {t('Xác nhận gửi')}
              </Button>
            ))}
            <PrintMenu orders={[o]} variant={o.confirmed ? 'primary' : undefined} />
            {o.editable && canProcess && (
              <>
                <Button size="sm" onClick={() => setEditing(true)}>
                  <Icon name="edit" size={15} /> {t('Sửa đơn')}
                </Button>
                {o.deletable && (
                  <Button size="sm" variant="ghost" onClick={() => onDelete(o)}>
                    <Icon name="trash" size={15} /> {t('Xóa đơn')}
                  </Button>
                )}
              </>
            )}
          </div>
        )}

        <div className={styles.body}>
          {editing ? (
            <EcomOrderEditForm order={o} onDone={() => setEditing(false)} />
          ) : (
            <>
              {issues.length > 0 && (
                <div className={styles.issues}>
                  <Notice tone="warning" title={t('Cần bổ sung trước khi xác nhận gửi')}>
                    <ul className={styles.issueList}>{issues.map(i => <li key={i}>{t(i)}</li>)}</ul>
                  </Notice>
                </div>
              )}
              <Sheet>
                <SheetBox no={1} title="Người nhận">
                  <SheetName>{r?.name || o.cnee || '—'}</SheetName>
                  {r?.company && <SheetLines lines={[r.company]} />}
                  <SheetLines lines={receiverLines(r, o.ct)} />
                  <SheetFields columns={2} items={[['Điện thoại', r?.phone ?? ''], ['Email', r?.email ?? '']]} />
                </SheetBox>

                <SheetBox no={2} title="Sản phẩm" aside={t('{n} SP', { n: o.items })}>
                  {products.length ? (
                    <>
                      <SheetTable
                        head={hasHs ? ['Tên hàng', 'SKU', 'Mã HS', 'SL', 'Đơn giá', 'Thành tiền'] : ['Tên hàng', 'SKU', 'SL', 'Đơn giá', 'Thành tiền']}
                        numeric={hasHs ? [3, 4, 5] : [2, 3, 4]}
                        rows={products.map(p => [
                          p.name,
                          p.sku || '—',
                          ...(hasHs ? [p.hsCode || '—'] : []),
                          formatNumber(p.qty),
                          money(p.sellingPrice),
                          money(p.sellingPrice * p.qty)
                        ])}
                      />
                      <SheetTotals items={[['Tổng tiền hàng', money(itemsTotal)]]} />
                    </>
                  ) : (
                    <p className={styles.muted}>{t('Sàn chưa gửi danh sách sản phẩm.')}</p>
                  )}
                </SheetBox>

                {(extra.length > 0 || o.note) && (
                  <SheetBox no={3} title="Thông tin khác">
                    {extra.length > 0 && <SheetFields columns={2} items={extra} />}
                    {o.note && <SheetFields items={[['Ghi chú của shop', o.note]]} />}
                  </SheetBox>
                )}
              </Sheet>
            </>
          )}
        </div>
      </aside>
    </>
  );
}
