import { useI18n } from '@/shared/i18n';
import { Button, LinkButton, Modal, Sheet, SheetBox, SheetFields, SheetLines, SheetName, SheetRow, SheetTable, SheetTotals } from '@/shared/ui';
import type { Draft } from '../api';
import { draftDetail } from '../lib/draft-detail';
import styles from './DraftDetailModal.module.css';

interface DraftDetailModalProps {
  draft: Draft | null;
  onClose: () => void;
  onPrint: (draft: Draft) => void;
  printing: boolean;
}

const fmt = (n: number) => n.toLocaleString('en-US', { maximumFractionDigits: 2 });

/** Xem đơn nháp / chưa in theo bố cục bản in (phiếu vận đơn) trước khi sửa hoặc In & cấp bill. */
export function DraftDetailModal({ draft, onClose, onPrint, printing }: DraftDetailModalProps) {
  const { t } = useI18n();
  if (!draft) return null;
  const d = draftDetail(draft.payload);
  const ready = draft.stt === 'ready';
  let no = 0;

  return (
    <Modal
      open
      size="lg"
      title={t('Chi tiết đơn — {name}', { name: draft.cnee || t('chưa có người nhận') })}
      onClose={onClose}
      footerNote={<span className={styles.muted}>{t('Tạo lúc {date}', { date: draft.date })}</span>}
      footer={
        <>
          <LinkButton size="sm" to={`/orders/new/quick?draft=${encodeURIComponent(draft.id)}`}>
            {t(ready ? 'Sửa' : 'Tiếp tục')}
          </LinkButton>
          <Button size="sm" variant="primary" disabled={!ready || printing} onClick={() => onPrint(draft)}>
            {t(printing ? 'Đang in…' : 'In & cấp bill')}
          </Button>
        </>
      }
    >
      <p className={styles.state}>
        {t(ready ? 'Đơn chưa in — mã VA Bill được cấp khi bấm "In & cấp bill".' : 'Đơn nháp — còn thiếu thông tin, bấm "Tiếp tục" để khai đủ.')}
      </p>

      <Sheet>
        <SheetRow>
          <SheetBox no={++no} title="Người gửi (Shipper)">
            <SheetName>{d.shipper.name || '—'}</SheetName>
            <SheetLines lines={d.shipper.lines} />
            <SheetFields items={d.shipper.fields} />
          </SheetBox>
          <SheetBox no={++no} title="Người nhận (Consignee)">
            <SheetName>{d.receiver.name || '—'}</SheetName>
            <SheetLines lines={d.receiver.lines} />
            <SheetFields items={d.receiver.fields} />
          </SheetBox>
        </SheetRow>

        <SheetBox no={++no} title="Dịch vụ & lô hàng">
          <SheetFields items={d.shipment} columns={4} />
        </SheetBox>

        {d.packages.length > 0 && (
          <SheetBox no={++no} title="Chi tiết kiện">
            <SheetTable
              head={['SL', 'Bao bì', 'Nhóm hàng', 'D × R × C (cm)', 'Cân / kiện']}
              numeric={[0, 4]}
              rows={d.packages.map(p => [p.qty, t(p.packaging), t(p.category) || '—', p.size || '—', p.weight || '—'])}
            />
            {d.packageTotals && (
              <SheetTotals
                items={[
                  ['Cân thực', `${fmt(d.packageTotals.grossWeight)} kg`],
                  ['Cân quy đổi', `${fmt(d.packageTotals.volumetricWeight)} kg`],
                  ['Cân tính cước', `${fmt(d.packageTotals.chargeableWeight)} kg`]
                ]}
              />
            )}
          </SheetBox>
        )}

        {d.items.length > 0 && (
          <SheetBox no={++no} title="Invoice" aside={d.invoice.map(([, v]) => t(v)).filter(Boolean).join(' · ')}>
            <SheetTable
              head={['#', 'Mô tả hàng hóa', 'Mã HS', 'Xuất xứ', 'SL / ĐVT', 'Đơn giá', 'Thành tiền']}
              numeric={[4, 5, 6]}
              rows={d.items.map((it, i) => [
                i + 1,
                <>
                  <div>{it.descEn}</div>
                  {it.descVi && <div className={styles.sub}>{it.descVi}</div>}
                  {it.manufacturer && <div className={styles.sub}>{it.manufacturer}</div>}
                </>,
                it.hs || '—',
                it.origin || '—',
                it.qty,
                it.price,
                fmt(it.amount)
              ])}
            />
            <SheetTotals
              items={[
                ...(d.shippingFee ? [['Shipping fee', `${fmt(d.shippingFee)} ${d.currency}`] as const] : []),
                ['Tổng giá trị invoice', `${fmt(d.invoiceTotal)} ${d.currency}`]
              ]}
            />
          </SheetBox>
        )}
      </Sheet>
    </Modal>
  );
}
