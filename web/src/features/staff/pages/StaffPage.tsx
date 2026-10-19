import { useMemo, useState } from 'react';
import { getErrorMessage, isNotImplemented } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Button, Modal, PageHeader } from '@/shared/ui';
import ws from '@/shared/ui/Workspace.module.css';
import { useAssignablePermissions, useDeleteStaff, useStaffList, useUpdateStaff } from '../api';
import { ResetPasswordModal } from '../components/ResetPasswordModal';
import { StaffFormModal } from '../components/StaffFormModal';
import { filterStaff, grantedGroups, profileOf, staffStats, type StaffFilter } from '../lib/staff';
import type { StaffAccount } from '../types';
import styles from './StaffPage.module.css';

type Dialog =
  | { kind: 'create' }
  | { kind: 'edit' | 'password' | 'delete'; staff: StaffAccount };

/** Tối đa tài khoản con / khách — khớp backend (StaffHandlers.MaxPerCustomer). */
const MAX_STAFF = 100;

const FILTERS: ReadonlyArray<{ key: StaffFilter; label: string }> = [
  { key: 'all', label: 'Tất cả' },
  { key: 'active', label: 'Đang hoạt động' },
  { key: 'locked', label: 'Đã khóa' }
];

/**
 * Tài khoản nhân viên — tài khoản chính (admin) của khách tạo tài khoản con và chọn quyền cho từng người.
 * Cùng bố cục nghiệp vụ với Hỗ trợ & sự cố: hàng trên có tab lọc + ô tìm + nút thêm, khung bảng nhân viên, lưu ý ở footer.
 */
export default function StaffPage() {
  const { t } = useI18n();
  const list = useStaffList();
  const permissions = useAssignablePermissions();
  const update = useUpdateStaff();
  const [dialog, setDialog] = useState<Dialog>();
  const [filter, setFilter] = useState<StaffFilter>('all');
  const [query, setQuery] = useState('');
  const close = () => setDialog(undefined);

  const descriptions = useMemo(() => new Map(permissions.data?.map(p => [p.code, p.description])), [permissions.data]);
  const describe = (code: string) => t(descriptions.get(code) ?? code);
  const all = list.data ?? [];
  const stats = staffStats(all);
  const counts: Record<StaffFilter, number> = { all: stats.total, active: stats.active, locked: stats.locked };
  const rows = filterStaff(all, filter, query);
  const full = stats.total >= MAX_STAFF;

  const setActive = (s: StaffAccount, active: boolean) =>
    update.mutate({ id: s.id, body: { ...profileOf(s), active } });
  const create = () => setDialog({ kind: 'create' });

  return (
    <>
      <PageHeader title="Tài khoản nhân viên" description="Tạo tài khoản đăng nhập riêng cho nhân viên và chọn chức năng từng người được dùng." />

      {list.isError ? (
        <p className={cx(ws.box, ws.note)}>
          {isNotImplemented(list.error) ? t('Chức năng đang được kết nối máy chủ. Vui lòng thử lại sau.') : t(getErrorMessage(list.error))}
        </p>
      ) : (
        <>
          {/* Chức năng (tab lọc) bên trái · ô tìm + nút thao tác bên phải */}
          <div className={ws.toolbar}>
            <div className={ws.tabs} role="tablist" aria-label={t('Tài khoản nhân viên')}>
              {FILTERS.map(f => (
                <button
                  key={f.key}
                  type="button"
                  role="tab"
                  aria-selected={filter === f.key}
                  className={cx(ws.tab, filter === f.key && ws.tabOn)}
                  onClick={() => setFilter(f.key)}
                >
                  {t(f.label)} <span className={ws.count}>{counts[f.key]}</span>
                </button>
              ))}
            </div>
            <div className={ws.tools}>
              <input className={ws.search} value={query} onChange={e => setQuery(e.target.value)} placeholder={t('Tìm tên, tên đăng nhập, email…')} aria-label={t('Tìm nhân viên')} />
              <button type="button" className={ws.btn} onClick={create} disabled={full} title={full ? t('Đã đủ {max} tài khoản', { max: MAX_STAFF }) : undefined}>
                + {t('Thêm nhân viên')}
              </button>
            </div>
          </div>

          {/* Hiển thị: bảng nhân viên */}
          <section className={ws.box}>
            {list.isPending ? (
              <p className={ws.note}>{t('Đang tải…')}</p>
            ) : !all.length ? (
              <div className={ws.note}>
                <p className={styles.emptyText}>{t('Chưa có tài khoản nhân viên. Thêm nhân viên để họ đăng nhập bằng tài khoản riêng, không cần dùng chung mật khẩu công ty.')}</p>
                <button type="button" className={ws.btn} onClick={create}>+ {t('Thêm nhân viên')}</button>
              </div>
            ) : !rows.length ? (
              <p className={ws.note}>{t('Không có nhân viên khớp bộ lọc.')}</p>
            ) : (
              <div className={ws.tableWrap}>
                <table className={ws.table}>
                  <thead>
                    <tr>
                      <th>{t('Họ tên')}</th>
                      <th style={{ width: 140 }}>{t('Tên đăng nhập')}</th>
                      <th style={{ width: 180 }}>{t('SĐT / email')}</th>
                      <th>{t('Quyền')}</th>
                      <th style={{ width: 120 }}>{t('Trạng thái')}</th>
                      <th style={{ width: 210 }}>{t('Thao tác')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map(s => (
                      <tr key={s.id} className={styles.row}>
                        <td><b>{s.fullName}</b></td>
                        <td className="mono">{s.userName}</td>
                        <td>
                          {[s.phone, s.phone2, s.email, s.email2].some(Boolean)
                            ? [[s.phone, s.phone2], [s.email, s.email2]].map(pair => pair.filter(Boolean).join(' · ')).filter(Boolean).map(v => <div key={v}>{v}</div>)
                            : '—'}
                        </td>
                        <td>
                          {s.permissions.length
                            ? grantedGroups(s.permissions, describe).map(g => (
                                <div key={g.label}><span className={styles.group}>{t(g.label)}:</span> {g.items.join(', ')}</div>
                              ))
                            : <span className={styles.muted}>{t('Chưa cấp quyền')}</span>}
                        </td>
                        <td className={s.active ? styles.active : styles.locked}>{t(s.active ? 'Đang hoạt động' : 'Đã khóa')}</td>
                        <td>
                          <span className={styles.actions}>
                            <button type="button" className={ws.linkBtn} onClick={() => setDialog({ kind: 'edit', staff: s })}>{t('Sửa')}</button>
                            <button type="button" className={ws.linkBtn} onClick={() => setDialog({ kind: 'password', staff: s })}>{t('Đặt lại mật khẩu')}</button>
                            <button type="button" className={ws.linkBtn} onClick={() => setActive(s, !s.active)}>{t(s.active ? 'Khóa' : 'Mở khóa')}</button>
                            <button type="button" className={cx(ws.linkBtn, ws.linkDanger)} onClick={() => setDialog({ kind: 'delete', staff: s })}>{t('Xóa')}</button>
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          {/* Lưu ý */}
          <footer className={ws.footer}>
            <span>{t('Đã dùng')}: <b>{stats.total}/{MAX_STAFF}</b> {t('tài khoản')}</span>
            <span>{t('Nhân viên chỉ thấy đơn do mình tạo, trừ khi được cấp "Xem toàn bộ đơn".')}</span>
            <span>{t('Mọi nhân viên luôn dùng được Hỗ trợ & sự cố và Thông báo.')}</span>
            <span>{t('Nhân viên không tự đổi mật khẩu — bạn đặt lại giúp khi cần.')}</span>
          </footer>
        </>
      )}

      {dialog?.kind === 'create' && <StaffFormModal onClose={close} />}
      {dialog?.kind === 'edit' && <StaffFormModal key={dialog.staff.id} staff={dialog.staff} onClose={close} />}
      {dialog?.kind === 'password' && <ResetPasswordModal staff={dialog.staff} onClose={close} />}
      {dialog?.kind === 'delete' && <DeleteStaffModal staff={dialog.staff} onClose={close} />}
    </>
  );
}

function DeleteStaffModal({ staff, onClose }: { staff: StaffAccount; onClose: () => void }) {
  const { t } = useI18n();
  const remove = useDeleteStaff();
  return (
    <Modal
      open
      title={t('Xóa tài khoản {name}?', { name: staff.userName })}
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose}>{t('Hủy')}</Button>
          <Button variant="danger" disabled={remove.isPending} onClick={() => remove.mutate(staff.id, { onSuccess: onClose })}>
            {t(remove.isPending ? 'Đang xóa…' : 'Xóa tài khoản')}
          </Button>
        </>
      }
    >
      <p>{t('Nhân viên {name} sẽ không đăng nhập được nữa. Đơn hàng đã tạo vẫn giữ nguyên trong tài khoản công ty.', { name: staff.fullName })}</p>
    </Modal>
  );
}
