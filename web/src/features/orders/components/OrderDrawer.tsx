import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useI18n } from '@/shared/i18n';
import { formatNumber } from '@/shared/lib/format';
import { useCopyToClipboard } from '@/shared/lib/useCopyToClipboard';
import { Button, DropdownMenu, Icon, Sheet, SheetBox, SheetFields, SheetLines, SheetName, SheetRow, SheetTable, SheetTotals, StatusPill } from '@/shared/ui';
import { TROUBLE_STATUS, TroubleDetailDialog, troublesOfBill, useTroubles } from '@/features/troubles';
import { useOrder, useOrderEvents } from '../api';
import { ORDER_STATUS, PRINT_DOCUMENTS } from '../constants';
import { invoiceTotal, lineVolume, receiverAddress, summarizePackages } from '../lib/order-detail';
import { estimatePodDate } from '../lib/order-utils';
import { usePrintDocuments } from '../mutations';
import type { Order, OrderActions, OrderDetail } from '../types';
import { TrackingLinks } from './TrackingLinks';
import styles from './OrderDrawer.module.css';

interface OrderDrawerProps {
  order: Order | null;
  onClose: () => void;
  actions: Pick<OrderActions, 'onPhotos' | 'onTrouble'>;
}

/** Hình thức xuất khẩu (MaVanDon.Ly_Do_Xuat_Hang). */
const EXPORT_TYPES: Record<string, string> = { GIFT: 'gift (no commercial value)', SAMPLE: 'sample', OTHER: 'khác' };

/**
 * Ngăn chi tiết đơn trượt từ phải — nội dung bố cục như phiếu vận đơn (chữ đen, kẻ ô mảnh, không màu trang trí).
 * Hiện ngay dữ liệu dòng trong bảng, rồi tải thêm chi tiết (người gửi / nhận, kiện, invoice) từ GET /orders/:bill.
 */
export function OrderDrawer({ order, onClose, actions }: OrderDrawerProps) {
  const { t } = useI18n();
  const navigate = useNavigate();
  const copy = useCopyToClipboard();
  const print = usePrintDocuments();
  const events = useOrderEvents(order?.bill ?? null);
  const detail = useOrder(order?.bill ?? null);
  const troubleList = useTroubles();
  const [openTroubleId, setOpenTroubleId] = useState<string | null>(null);

  useEffect(() => {
    if (!order) return;
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose();
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [order, onClose]);

  if (!order) return null;
  const o: OrderDetail = { ...order, ...(detail.data ?? {}) };
  const status = ORDER_STATUS[o.st];
  const estimate = o.podEstimate || estimatePodDate(o);
  const s = o.shipper;
  const r = o.receiver;
  const packages = o.packages ?? [];
  const sum = packages.length ? summarizePackages(packages, o.route) : null;
  const items = o.invoice?.items ?? [];
  const cur = o.invoice?.currency || '';
  const money = (n: number) => `${formatNumber(n)}${cur ? ` ${cur}` : ''}`;
  const troubles = troublesOfBill(troubleList.data ?? [], o.bill);
  const openTrouble = troubles.find(x => x.id === openTroubleId) ?? null;
  let no = 0;

  return (
    <>
      <div className={styles.scrim} onClick={onClose} />
      <aside className={styles.drawer} role="dialog" aria-modal="true" aria-labelledby="order-drawer-title">
        <header className={styles.header}>
          <div className={styles.headMain}>
            <p className={styles.kicker}>{t('Chi tiết đơn hàng')}</p>
            <h2 id="order-drawer-title" className={styles.title}>
              VA Bill {o.bill}
              <button type="button" className={styles.iconBtn} onClick={() => void copy(o.bill, t('Đã sao chép mã bill'))} aria-label={t('Sao chép mã bill')} title={t('Sao chép mã bill')}>
                <Icon name="copy" size={14} />
              </button>
            </h2>
            <p className={styles.headMeta}>
              {t('Trạng thái')}: <strong>{t(status.label)}</strong>
              {o.ref && <> · Ref: {o.ref}</>}
            </p>
          </div>
          <button type="button" className={styles.close} onClick={onClose} aria-label={t('Đóng')}>
            <Icon name="close" size={16} />
          </button>
        </header>

        <div className={styles.toolbar}>
          <DropdownMenu
            items={PRINT_DOCUMENTS.map(d => ({ label: d.label, onSelect: () => void print([o.bill], d.key) }))}
            trigger={({ open, toggle }) => (
              <Button size="sm" variant="primary" onClick={toggle} aria-expanded={open} aria-haspopup="menu">
                <Icon name="printer" size={15} /> {t('In')} <Icon name="chevronDown" size={14} />
              </Button>
            )}
          />
          <Button size="sm" onClick={() => navigate(`/orders/new?from=${encodeURIComponent(o.bill)}`)}>{t('Copy đơn')}</Button>
          <Button size="sm" onClick={() => actions.onPhotos(o)}>{o.photos ? t('Ảnh kiện ({n})', { n: o.photos }) : t('Ảnh kiện')}</Button>
          <Button size="sm" onClick={() => actions.onTrouble(o)}>{t('Báo sự cố')}</Button>
        </div>

        <div className={styles.body}>
          <Sheet>
            <SheetBox no={++no} title="Vận đơn">
              <SheetFields
                columns={3}
                items={[
                  ['Dịch vụ', t(o.route)],
                  ['Chi nhánh', o.branch ? t(o.branch) : ''],
                  ['Nước đến', o.ct],
                  ['VSVX', o.remoteArea ?? t('Không')],
                  ['Loại hàng', o.type],
                  ['Kiện / cân', t(o.pcs)],
                  ['Ngày tạo', o.created],
                  ['Ngày gửi', o.sent || t('Chưa gửi')],
                  ['Mã tracking hãng', o.connect ?? ''],
                  ['Giao hàng (POD)', o.pod ? `${o.pod.date} ${o.pod.time}`.trim() : ''],
                  [o.pod ? 'Người ký' : 'Dự kiến giao', o.pod ? o.pod.signer : estimate]
                ]}
              />
            </SheetBox>

            {troubles.length > 0 && (
              <SheetBox no={++no} title="Sự cố của đơn" aside={t('{n} sự cố', { n: troubles.length })}>
                <ul className={styles.troubles}>
                  {troubles.map(x => (
                    <li key={x.id}>
                      <button type="button" className={styles.trouble} onClick={() => setOpenTroubleId(x.id)}>
                        <span className={styles.troubleHead}>
                          <strong className="mono">{x.id}</strong> · {t(x.type)}
                          <StatusPill tone={TROUBLE_STATUS[x.status].tone}>{t(TROUBLE_STATUS[x.status].label)}</StatusPill>
                        </span>
                        <span className={styles.troubleSub}>
                          {x.updatedAt ?? x.date} · {x.desc}
                        </span>
                      </button>
                    </li>
                  ))}
                </ul>
              </SheetBox>
            )}

            {detail.isLoading ? (
              <SheetBox title="Người gửi & người nhận"><p className={styles.muted}>{t('Đang tải chi tiết đơn…')}</p></SheetBox>
            ) : (
              <SheetRow>
                <SheetBox no={++no} title="Người gửi (Shipper)">
                  <SheetName>{s?.company || '—'}</SheetName>
                  <SheetLines lines={[s?.address ?? '']} />
                  <SheetFields items={[['Người liên hệ', s?.contact ?? ''], ['Điện thoại', s?.tel ?? ''], ['MST / CCCD', s?.taxId ?? ''], ['Email', s?.email ?? '']]} />
                </SheetBox>
                <SheetBox no={++no} title="Người nhận (Consignee)">
                  <SheetName>{r?.company || o.cnee}</SheetName>
                  <SheetLines lines={r ? receiverAddress(r) : [o.ct]} />
                  <SheetFields items={[['Người liên hệ', r?.contact ?? ''], ['Điện thoại', r?.tel ?? ''], ['Tax ID', r?.taxId ?? ''], ['IOSS No', r?.iossNo ?? ''], ['EORI No', r?.eoriNo ?? ''], ['Email', r?.email ?? '']]} />
                </SheetBox>
              </SheetRow>
            )}

            {packages.length > 0 && sum && (
              <SheetBox no={++no} title="Chi tiết kiện" aside={t('{n} kiện', { n: sum.pieces })}>
                <SheetTable
                  head={['SL', 'Bao bì', 'D × R × C (cm)', 'Cân / kiện', 'Quy đổi']}
                  numeric={[0, 3, 4]}
                  rows={packages.map(p => [
                    p.qty,
                    p.packType || '—',
                    p.length && p.width && p.height ? `${p.length} × ${p.width} × ${p.height}` : '—',
                    `${formatNumber(p.weightKg)} kg`,
                    `${formatNumber(lineVolume(p, o.route))} kg`
                  ])}
                />
                <SheetTotals
                  items={[
                    ['Cân thực', `${formatNumber(sum.gross)} kg`],
                    ['Cân quy đổi', `${formatNumber(sum.volume)} kg`],
                    ['Cân tính cước', `${formatNumber(o.chargeableWeight ?? sum.chargeable)} kg`]
                  ]}
                />
              </SheetBox>
            )}

            {items.length > 0 ? (
              <SheetBox
                no={++no}
                title="Invoice"
                aside={[o.invoice?.exportType ? t(EXPORT_TYPES[o.invoice.exportType] ?? o.invoice.exportType) : '', cur].filter(Boolean).join(' · ')}
              >
                <SheetTable
                  head={['#', 'Mô tả hàng hóa', 'Mã HS', 'Xuất xứ', 'SL / ĐVT', 'Đơn giá', 'Thành tiền']}
                  numeric={[4, 5, 6]}
                  rows={items.map((it, i) => [
                    i + 1,
                    <>
                      <div>{it.descEn || it.descVi || '—'}</div>
                      {it.descEn && it.descVi && <div className={styles.sub}>{it.descVi}</div>}
                    </>,
                    it.hs || '—',
                    it.origin || '—',
                    `${formatNumber(it.qty)} ${it.unit}`,
                    money(it.price),
                    money(it.amount || it.qty * it.price)
                  ])}
                />
                <SheetTotals
                  items={[
                    ...(o.invoice?.shippingFee ? [['Shipping fee', money(o.invoice.shippingFee)] as const] : []),
                    ['Tổng giá trị hàng', money(invoiceTotal(items))]
                  ]}
                />
              </SheetBox>
            ) : (
              <SheetBox no={++no} title="Hàng hóa">
                <div>{o.content || '—'}</div>
              </SheetBox>
            )}

            <SheetBox no={++no} title="Hành trình" aside={<TrackingLinks bill={o.bill} />}>
              {events.data?.length ? (
                <SheetTable
                  head={['Thời gian', 'Cập nhật', 'Nơi']}
                  rows={events.data.map(ev => [<span key="t" className="tabular">{ev.time}</span>, t(ev.title), ev.location || '—'])}
                />
              ) : (
                <p className={styles.muted}>{t(events.isLoading ? 'Đang tải…' : 'Chưa có cập nhật hành trình — xem chi tiết qua VA Track.')}</p>
              )}
            </SheetBox>
          </Sheet>
        </div>
      </aside>
      <TroubleDetailDialog ticket={openTrouble} onClose={() => setOpenTroubleId(null)} />
    </>
  );
}
