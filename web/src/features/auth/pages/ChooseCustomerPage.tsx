import { useDeferredValue, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useI18n } from '@/shared/i18n';
import { Button, Card, DataTable, Notice, StatusPill, TextField, type Column } from '@/shared/ui';
import { useActAsCustomer, useActableCustomers, useSession } from '../api';
import { safeNextPath } from '../lib/session';
import type { ActableCustomer } from '../types';
import styles from './ChooseCustomerPage.module.css';

/** Backend trả tối đa bấy nhiêu khách mỗi lần tìm. */
const LIST_LIMIT = 50;

/**
 * Nhân viên Việt An chọn khách để làm thay: nhân viên thường thấy khách mình phụ trách (TCustomer.Staff_ID),
 * IS_ADMIN / VIEW_CUSTOMER_ALL thấy mọi khách. Bảng gọn mỗi khách 1 dòng, cuộn trong khung; khách đang làm đứng đầu,
 * bấm "Tiếp tục" quay lại không cần chọn lại. Chọn xong dùng chính các trang của khách (về `?next=`, mặc định Tạo đơn).
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
  const current = session.data?.status === 'authenticated' ? session.data.user.customerCode : '';
  const next = safeNextPath(params.get('next'));
  // Khách đang làm lên đầu danh sách.
  const rows = useMemo(() => [...(list.data ?? [])].sort((a, b) => Number(b.code === current) - Number(a.code === current)), [list.data, current]);

  const go = () => void navigate(next, { replace: true });
  const choose = (c: ActableCustomer) => (c.code === current ? go() : actAs.mutate(c.id, { onSuccess: go }));

  const columns: ReadonlyArray<Column<ActableCustomer>> = [
    {
      key: 'code',
      header: 'Mã khách',
      width: 150,
      render: c => <strong className={styles.clip}>{c.code}</strong>
    },
    { key: 'name', header: 'Tên công ty', render: c => <span className={styles.clip} title={c.address ?? undefined}>{c.companyName}</span> },
    { key: 'login', header: 'Tên đăng nhập', width: 160, render: c => <span className={styles.clip}>{c.loginUserName ?? '—'}</span> },
    { key: 'phone', header: 'Điện thoại', width: 130, render: c => <span className={styles.clip}>{c.phone ?? '—'}</span> },
    {
      key: 'act',
      header: '',
      width: 190,
      align: 'right',
      render: c =>
        c.code === current
          ? <span className={styles.act}><StatusPill tone="brand">{t('Đang làm')}</StatusPill><Button size="sm" onClick={go}>{t('Tiếp tục')}</Button></span>
          : <Button size="sm" variant="primary" disabled={actAs.isPending} onClick={() => choose(c)}>{t('Chọn')}</Button>
    }
  ];

  return (
    <div className={styles.wrap}>
      <Card title={t('Chọn khách để tạo đơn giúp')} flush>
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
              onRowClick={choose}
              highlight={c => c.code === current}
              minWidth={640}
              caption="Chọn khách"
            />
          </div>
        )}
        {rows.length >= LIST_LIMIT && <p className={styles.more}>{t('Hiện {n} khách đầu — gõ mã, tên hoặc tên đăng nhập để tìm khách khác', { n: LIST_LIMIT })}</p>}
      </Card>
    </div>
  );
}
