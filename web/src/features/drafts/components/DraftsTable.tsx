import { useState } from 'react';
import { useI18n } from '@/shared/i18n';
import { Button, DataTable, Icon, LinkButton, StatusPill, type Column } from '@/shared/ui';
import { useDeleteDraft, useDrafts, type Draft } from '../api';
import { TermsDialog } from '@/features/terms';
import { useIssueAndPrint } from '../hooks/useIssueAndPrint';
import styles from '../pages/DraftsPage.module.css';
import { DraftDetailModal } from './DraftDetailModal';

/** Bảng đơn nháp & chưa in: bấm vào dòng (hoặc Xem) để xem chi tiết · Sửa · In & cấp bill (mở bản in A4) · Xóa. Dùng ở trang Đơn nháp và dưới form tạo đơn 1 trang. */
export function DraftsTable({ limit }: { limit?: number }) {
  const { data = [], isLoading, isError, refetch } = useDrafts();
  const print = useIssueAndPrint();
  const remove = useDeleteDraft();
  const rows = limit ? data.slice(0, limit) : data;
  const [viewing, setViewing] = useState<Draft | null>(null);
  // In & cấp bill: hiện Điều khoản dịch vụ, khách tick đồng ý mới cấp bill và in.
  const [terms, setTerms] = useState<{ id: string; onDone?: () => void } | null>(null);
  const { t } = useI18n();

  const columns: ReadonlyArray<Column<Draft>> = [
    { key: 'st', header: 'Trạng thái', width: 100, render: d => (d.stt === 'ready' ? <StatusPill tone="info">{t('Chưa in')}</StatusPill> : <StatusPill>{t('Nháp')}</StatusPill>) },
    {
      key: 'cnee',
      header: 'Người nhận',
      render: d => <span className={styles.strong}>{t(d.cnee || '—')}</span>
    },
    { key: 'ct', header: 'Nước đến', render: d => d.ct },
    { key: 'svc', header: 'Dịch vụ', render: d => <span className={styles.muted}>{t(d.service)}</span> },
    { key: 'pcs', header: 'Kiện / cân', render: d => t(d.pcs) },
    { key: 'content', header: 'Nội dung', render: d => t(d.content) },
    { key: 'date', header: 'Tạo lúc', render: d => <span className={`${styles.muted} tabular`}>{d.date}</span> },
    {
      key: 'act',
      header: 'Thao tác',
      width: 320,
      render: d => {
        const ready = d.stt === 'ready';
        const printing = print.pendingId === d.id;
        return (
          <div className={styles.actions}>
            <Button size="sm" onClick={() => setViewing(d)}><Icon name="eye" size={15} /> {t('Xem')}</Button>
            <LinkButton size="sm" to={`/orders/new/quick?draft=${encodeURIComponent(d.id)}`}><Icon name="edit" size={15} /> {t(ready ? 'Sửa' : 'Tiếp tục')}</LinkButton>
            <Button size="sm" variant="primary" onClick={() => setTerms({ id: d.id })} disabled={!ready || printing} title={t(ready ? 'Cấp mã bill và in vận đơn A4, invoice, nhãn A6' : 'Hoàn thiện đơn trước khi in')}>
              {t(printing ? 'Đang in…' : 'In & cấp bill')}
            </Button>
            <Button size="sm" iconOnly variant="danger" onClick={() => remove.mutate(d.id)} aria-label={t('Xóa đơn nháp {name}', { name: d.cnee })}>
              <Icon name="trash" size={15} />
            </Button>
          </div>
        );
      }
    }
  ];

  return (
    <>
      <DataTable
        caption="Đơn nháp và chưa in"
        columns={columns}
        rows={rows}
        rowKey={d => d.id}
        loading={isLoading}
        minWidth={960}
        onRowClick={setViewing}
        empty={
          isError
            ? { title: 'Không tải được đơn nháp', action: <Button onClick={() => void refetch()}>{t('Thử lại')}</Button> }
            : { title: 'Chưa có đơn nháp', description: 'Đơn đang làm dở hoặc chưa in sẽ hiện ở đây.' }
        }
      />
      <DraftDetailModal
        draft={viewing}
        onClose={() => setViewing(null)}
        printing={!!viewing && print.pendingId === viewing.id}
        onPrint={d => setTerms({ id: d.id, onDone: () => setViewing(null) })}
      />
      <TermsDialog
        open={!!terms}
        onClose={() => setTerms(null)}
        onAccept={() => {
          if (terms) print.run(terms.id, terms.onDone);
          setTerms(null);
        }}
      />
    </>
  );
}
