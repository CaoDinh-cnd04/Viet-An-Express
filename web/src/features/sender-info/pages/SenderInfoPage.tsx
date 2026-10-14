import { useEffect, useState, type FormEvent } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { ApiError, getErrorMessage } from '@/shared/api/http';
import { Button, Card, EmptyState, FormGrid, KeyValueList, Notice, PageHeader, StatusPill, TextAreaField, TextField } from '@/shared/ui';
import { useI18n } from '@/shared/i18n';
import { useSession } from '@/features/auth';
import { sessionKey } from '@/features/auth/api';
import { useCancelSenderInfo, useRequestSenderInfo, useSenderInfo } from '../api';
import { SENDER_FIELDS, SENDER_STATUS, type SenderInfo } from '../types';
import { SenderComparison } from '../SenderComparison';
import styles from '../SenderInfo.module.css';

export default function SenderInfoPage() {
  const { t } = useI18n();
  const info = useSenderInfo();
  const cancel = useCancelSenderInfo();
  const session = useSession();
  const qc = useQueryClient();
  const current = info.data?.current;
  useEffect(() => {
    if (!current || session.data?.status !== 'authenticated') return;
    const user = session.data.user;
    const changed = SENDER_FIELDS.some(f => (user[f.key] ?? '') !== (current[f.key] ?? ''));
    if (changed) qc.setQueryData(sessionKey, { ...session.data, user: { ...user, ...current, fullName: current.contactName || user.fullName } });
  }, [current, session.data, qc]);
  const pending = info.data?.pending;
  return <div className={styles.stack}>
    <PageHeader title="Thông tin người gửi" description="Thay đổi hồ sơ cần được Việt An xét duyệt trước khi áp dụng." />
    <div className={styles.tools}><Button size="sm" disabled={info.isFetching} onClick={() => void info.refetch()}>{t(info.isFetching ? 'Đang tải…' : 'Làm mới')}</Button></div>
    {info.isError ? <Notice tone="danger">{getErrorMessage(info.error)}{info.error instanceof ApiError && info.error.status === 403 && <p>{t('Vui lòng đăng nhập lại tài khoản chính để sử dụng chức năng này.')}</p>}</Notice> : info.isPending ? <p role="status">{t('Đang tải…')}</p> : current && <>
      <Card title="Thông tin đang sử dụng"><KeyValueList items={SENDER_FIELDS.map(f => [f.label, current[f.key] || '—'])} /></Card>
      {pending ? <Card title="Yêu cầu đang chờ duyệt" actions={<StatusPill tone="warning">{t('Chờ duyệt')}</StatusPill>}>
        <p className={styles.muted}>{t('Thông tin đang sử dụng chưa thay đổi. Việt An sẽ kiểm tra yêu cầu và thông báo kết quả.')}</p>
        <p>{t('Gửi lúc')}: {pending.createdAt}</p><SenderComparison request={pending} />
        {pending.note && <p>{t('Ghi chú')}: {pending.note}</p>}
        {cancel.isError && <Notice tone="danger">{getErrorMessage(cancel.error)}</Notice>}
        <Button variant="ghost" disabled={cancel.isPending} onClick={() => cancel.mutate(pending.id)}>{t(cancel.isPending ? 'Đang xử lý…' : 'Hủy yêu cầu')}</Button>
      </Card> : <SenderForm key={JSON.stringify(current)} current={current} />}
      <Card title="Lịch sử yêu cầu">
        {!info.data!.history.length ? <EmptyState title="Chưa có lịch sử yêu cầu" description="Các yêu cầu đã duyệt, từ chối hoặc hủy sẽ hiện ở đây." /> : info.data!.history.map(r => <details key={r.id} className={styles.history}>
          <summary><StatusPill tone={SENDER_STATUS[r.status].tone}>{SENDER_STATUS[r.status].label}</StatusPill><span>{r.createdAt}</span><span>{t('Xem thay đổi')}</span></summary>
          {r.reviewedAt && <p className={styles.muted}>{t('Xử lý lúc')}: {r.reviewedAt}</p>}
          {r.reason && <Notice tone="danger">{t('Lý do từ chối')}: {r.reason}</Notice>}
          {r.note && <p>{t('Ghi chú')}: {r.note}</p>}<SenderComparison request={r} />
        </details>)}
      </Card>
    </>}
  </div>;
}

function SenderForm({ current }: { current: SenderInfo }) {
  const { t } = useI18n();
  const [values, setValues] = useState(current);
  const [note, setNote] = useState('');
  const send = useRequestSenderInfo();
  const changed = SENDER_FIELDS.some(f => (values[f.key] ?? '').trim() !== (current[f.key] ?? '').trim());
  const submit = (e: FormEvent) => { e.preventDefault(); if (changed) send.mutate({ ...values, note: note.trim() || null }); };
  return <Card title="Đề nghị thay đổi thông tin">
    <form className={styles.form} onSubmit={submit}>
      <Notice>{t('Chỉ sau khi admin duyệt, thông tin mới được cập nhật vào hồ sơ. Đơn đã tạo giữ thông tin người gửi đã lưu.')}</Notice>
      <FormGrid columns={2}>{SENDER_FIELDS.map(f => f.key === 'address'
        ? <TextAreaField key={f.key} label={f.label} required={f.required} maxLength={f.max} rows={3} wide value={values[f.key] ?? ''} onChange={e => setValues(v => ({ ...v, [f.key]: e.target.value }))} />
        : <TextField key={f.key} label={f.label} required={f.required} maxLength={f.max} type={f.key === 'phone' ? 'tel' : 'text'} value={values[f.key] ?? ''} onChange={e => setValues(v => ({ ...v, [f.key]: e.target.value }))} />)}</FormGrid>
      <TextAreaField label="Lý do / ghi chú thay đổi" rows={3} maxLength={500} value={note} onChange={e => setNote(e.target.value)} />
      {send.isError && <Notice tone="danger">{getErrorMessage(send.error)}</Notice>}
      <div className={styles.tools}><Button variant="primary" type="submit" disabled={!changed || send.isPending}>{t(send.isPending ? 'Đang gửi…' : 'Gửi yêu cầu xét duyệt')}</Button></div>
    </form>
  </Card>;
}
