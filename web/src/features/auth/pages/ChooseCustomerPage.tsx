import { useDeferredValue, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useI18n } from '@/shared/i18n';
import { Button, Card, DataTable, Notice, StatusPill, TextField, type Column } from '@/shared/ui';
import { useActAsCustomer, useActableCustomers, useCreateNewCustomer, useSession } from '../api';
import { AssignManagerDialog } from '../components/AssignManagerDialog';
import { BackToAdminButton } from '../components/BackToAdminButton';
import { safeNextPath } from '../lib/session';
import type { ActableCustomer } from '../types';
import styles from './ChooseCustomerPage.module.css';

/** Backend trả tối đa bấy nhiêu khách mỗi lần tìm. */
const LIST_LIMIT = 50;

/**
 * Nhân viên Việt An chọn khách để làm thay: nhân viên thường thấy khách mình phụ trách (TCustomer.Staff_ID),
 * vai trò Administrator thấy mọi khách. Bảng gọn mỗi khách 1 dòng, cuộn trong khung; khách đang làm đứng đầu,
 * bấm "Tiếp tục" quay lại không cần chọn lại. Chỉ nút "Chọn" mới vào khách — bấm vào dòng không làm gì (06/11/2026, tránh vào nhầm khách). Chọn xong dùng chính các trang của khách (về `?next=`, mặc định Tạo đơn).
 * "Tạo khách mới": khách lần đầu gửi — backend tạo hồ sơ TCustomer tạm (đánh dấu khách mới cho CS hoàn thiện ở CMS) rồi mở
 * trang tạo đơn với người gửi trống. Quản trị / CS thấy nút "Gán nhân viên" (đã gán: "Đổi nhân viên") ở dòng khách mới.
 * Thứ tự do backend xếp: khách mới, rồi khách mới tạo đơn / tạo nhiều đơn lên đầu.
 */
export default function ChooseCustomerPage() {
  const { t } = useI18n();
  const session = useSession();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const [query, setQuery] = useState('');
  const search = useDeferredValue(query.trim());
  const list = useActableCustomers(search);
  const actAs = useActAsCustomer();
  const createNew = useCreateNewCustomer();
  const [assigning, setAssigning] = useState<ActableCustomer | null>(null);
  const user = session.data?.status === 'authenticated' ? session.data.user : null;
  const current = user?.customerCode ?? '';
  const canAssign = user?.canAssignManager === true;
  const next = safeNextPath(params.get('next'));
  // Khách đang làm lên đầu danh sách.
  const rows = useMemo(() => [...(list.data ?? [])].sort((a, b) => Number(b.code === current) - Number(a.code === current)), [list.data, current]);

  const go = () => void navigate(next, { replace: true });
  const choose = (c: ActableCustomer) => (c.code === current ? go() : actAs.mutate(c.id, { onSuccess: go }));
  const startNew = () => createNew.mutate(undefined, { onSuccess: () => void navigate('/orders/new', { replace: true }) });
  const busy = actAs.isPending || createNew.isPending;

  const columns: ReadonlyArray<Column<ActableCustomer>> = [
    {
      key: 'code',
      header: 'Mã khách',
      width: 150,
      // Khách mới chưa có mã (CustomerCode NULL — CS đặt ở CMS): hiện nhãn "Khách mới" thay cho số CustomerID.
      render: c => (c.isNew ? <StatusPill tone="warning">{t('Khách mới')}</StatusPill> : <strong className={styles.clip}>{c.code}</strong>)
    },
    {
      key: 'name',
      header: 'Tên công ty',
      render: c => (
        <span className={styles.clip} title={c.address ?? undefined}>{c.companyName || t('(chưa có tên)')}</span>
      )
    },
    { key: 'login', header: 'Tên đăng nhập', width: 160, render: c => <span className={styles.clip}>{c.loginUserName ?? '—'}</span> },
    { key: 'phone', header: 'Điện thoại', width: 130, render: c => <span className={styles.clip}>{c.phone ?? '—'}</span> },
    // Người phụ trách chỉ Administrator / CS thấy; sale không có cột này.
    ...(canAssign ? [{
      key: 'manager',
      header: 'Phụ trách',
      width: 150,
      render: (c: ActableCustomer) => <span className={styles.clip}>{c.managerName ?? (c.isNew ? t('Chưa gán') : '—')}</span>
    }] : []),
    {
      key: 'act',
      header: '',
      width: 220,
      align: 'right',
      render: c => (
        <span className={styles.act}>
          {c.isNew && canAssign && (
            <Button size="sm" onClick={e => { e.stopPropagation(); setAssigning(c); }}>{t(c.managerId ? 'Đổi nhân viên' : 'Gán nhân viên')}</Button>
          )}
          {c.code === current
            ? <><StatusPill tone="brand">{t('Đang làm')}</StatusPill><Button size="sm" onClick={go}>{t('Tiếp tục')}</Button></>
            : <Button size="sm" variant="primary" disabled={busy} onClick={e => { e.stopPropagation(); choose(c); }}>{t('Chọn')}</Button>}
        </span>
      )
    }
  ];

  return (
    <div className={styles.wrap}>
      <Card
        title={t('Chọn khách để tạo đơn giúp')}
        actions={
          <span className={styles.act}>
            {current && <BackToAdminButton />}
            <Button size="sm" variant="primary" disabled={busy} onClick={startNew} title={t('Khách lần đầu gửi, chưa có hồ sơ — mở trang đánh bill với người gửi trống')}>
              {t('Tạo khách mới')}
            </Button>
          </span>
        }
        flush
      >
        <div className={styles.search}>
          <TextField label="Tìm khách" placeholder="Mã khách, tên công ty, số điện thoại, tên đăng nhập" value={query} onChange={e => setQuery(e.target.value)} autoFocus />
        </div>
        {list.isError ? (
          <div className={styles.search}><Notice tone="danger">{t('Không tải được danh sách khách')}</Notice></div>
        ) : (
          <div className={styles.table}>
            <DataTable
              columns={columns}
              rows={rows}
              rowKey={c => String(c.id)}
              loading={list.isLoading}
              empty={{ title: 'Không có khách phù hợp', description: 'Bạn chỉ thấy khách mình phụ trách.' }}
              highlight={c => c.code === current}
              minWidth={820}
              caption="Chọn khách"
            />
          </div>
        )}
        {rows.length >= LIST_LIMIT && <p className={styles.more}>{t('Hiện {n} khách đầu — gõ mã, tên hoặc tên đăng nhập để tìm khách khác', { n: LIST_LIMIT })}</p>}
      </Card>
      <AssignManagerDialog customer={assigning} onClose={() => setAssigning(null)} />
    </div>
  );
}
