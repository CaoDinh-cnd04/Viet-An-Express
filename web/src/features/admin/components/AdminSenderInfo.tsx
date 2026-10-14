import { useState } from 'react';
import { ApiError, getErrorMessage } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { Button, Card, EmptyState, Modal, Notice, SelectField, StatusPill, TextAreaField } from '@/shared/ui';
import { SENDER_STATUS, type AdminSenderRequest, type SenderStatus } from '@/features/sender-info/types';
import { SenderComparison } from '@/features/sender-info/SenderComparison';
import { useAdminSenderInfo, useReviewSenderInfo } from '../sender-info-api';
import styles from '@/features/sender-info/SenderInfo.module.css';

export function AdminSenderInfo() {
  const { t } = useI18n();
  const [status, setStatus] = useState<SenderStatus | 'all'>('pending');
  const [openId, setOpenId] = useState<number | null>(null);
  const list = useAdminSenderInfo(true, status);
  const selected = list.data?.find(x => x.request.id === openId);
  return <Card title="Xét duyệt thông tin người gửi">
    <div className={styles.tools}><SelectField label="Trạng thái yêu cầu" value={status} onChange={e => { setStatus(e.target.value as SenderStatus | 'all'); setOpenId(null); }} options={[{ value: 'all', label: 'Tất cả' }, ...Object.entries(SENDER_STATUS).map(([value, s]) => ({ value, label: s.label }))]} />
      <Button size="sm" disabled={list.isFetching} onClick={() => void list.refetch()}>{t('Làm mới')}</Button></div>
    {list.isError ? <Notice tone="danger">{getErrorMessage(list.error)}{list.error instanceof ApiError && list.error.status === 403 && <p>{t('Đăng xuất và đăng nhập lại admin để nhận quyền xét duyệt mới.')}</p>}</Notice>
      : list.isPending ? <p>{t('Đang tải…')}</p> : !list.data?.length ? <EmptyState title="Không có yêu cầu" description="Các yêu cầu của khách phù hợp bộ lọc sẽ hiện ở đây." />
        : <ul className={styles.items}>{list.data.map(x => <li key={x.request.id}><button className={styles.item} onClick={() => setOpenId(x.request.id)}>
          <span><strong>{x.companyName} · {x.customerCode}</strong><small>{t('Người gửi')}: {x.userName} · {x.request.createdAt}</small><small>{x.request.changed.length} {t('trường thay đổi')}</small></span>
          <StatusPill tone={SENDER_STATUS[x.request.status].tone}>{SENDER_STATUS[x.request.status].label}</StatusPill>
        </button></li>)}</ul>}
    {selected && <ReviewDialog key={selected.request.id} item={selected} onClose={() => setOpenId(null)} />}
  </Card>;
}

function ReviewDialog({ item, onClose }: { item: AdminSenderRequest; onClose: () => void }) {
  const { t } = useI18n();
  const review = useReviewSenderInfo();
  const [reason, setReason] = useState('');
  const r = item.request;
  const pending = r.status === 'pending';
  return <Modal open title="Kiểm tra yêu cầu sửa người gửi" size="lg" onClose={() => { if (!review.isPending) onClose(); }} footer={pending && <>
    <Button disabled={review.isPending || !reason.trim()} onClick={() => review.mutate({ id: r.id, action: 'reject', reason: reason.trim() }, { onSuccess: onClose })}>{t('Từ chối yêu cầu')}</Button>
    <Button variant="primary" disabled={review.isPending || item.outdated || !item.live} onClick={() => review.mutate({ id: r.id, action: 'approve' }, { onSuccess: onClose })}>{t('Duyệt & cập nhật hồ sơ')}</Button>
  </>}>
    <div className={styles.stack}>
      <p><strong>{item.companyName}</strong> · {item.customerCode} · {item.userName}</p>
      <p>{t('Gửi lúc')}: {r.createdAt}</p>
      <StatusPill tone={SENDER_STATUS[r.status].tone}>{SENDER_STATUS[r.status].label}</StatusPill>
      {item.outdated && <Notice tone="warning">{t('Hồ sơ đã thay đổi trong lúc chờ duyệt. Hãy từ chối yêu cầu và đề nghị khách gửi lại theo hồ sơ mới.')}</Notice>}
      {!item.live && <Notice tone="danger">{t('Không tìm thấy hồ sơ khách hàng để cập nhật.')}</Notice>}
      <SenderComparison request={r} live={item.live} />
      {r.note && <p>{t('Ghi chú của khách')}: {r.note}</p>}
      {r.reviewedAt && <p>{t('Xử lý lúc')}: {r.reviewedAt} · {item.reviewedBy || '—'}</p>}
      {r.reason && <Notice tone="danger">{t('Lý do từ chối')}: {r.reason}</Notice>}
      {pending && <TextAreaField label="Lý do từ chối" hint="Bắt buộc khi từ chối yêu cầu" maxLength={500} rows={3} value={reason} onChange={e => setReason(e.target.value)} />}
      {review.isError && <Notice tone="danger">{getErrorMessage(review.error)}</Notice>}
    </div>
  </Modal>;
}
