import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getErrorMessage, ApiError } from '@/shared/api/http';
import { Button, Card, EmptyState, Notice } from '@/shared/ui';
import { useI18n } from '@/shared/i18n';
import { adminFetch } from '../api';
import styles from './AdminUsage.module.css';

interface Page { path: string; views: number; visitors: number }
interface Mode { mode: string; views: number; visitors: number; starts: number; completed: number; switches: number }
interface Report { views: number; visitors: number; pages: Page[]; modes: Mode[]; days: { date: string; views: number; visitors: number }[] }
const labels: Record<string, string> = {
  '/': 'Trang chủ', '/login': 'Đăng nhập', '/tracking': 'Tra cứu vận đơn', '/t': 'MyTracking công khai',
  '/orders/new': 'Tạo đơn từng bước', '/orders/new/quick': 'Tạo đơn 1 trang', '/orders/import': 'Tạo đơn từ Excel', '/orders': 'Đơn hàng của tôi', '/drafts': 'Đơn nháp & chưa in',
  '/pricing': 'Giá & gợi ý dịch vụ', '/ecommerce': 'E-commerce', '/ecommerce/orders': 'Đơn hàng E-com', '/troubles': 'Quản lý sự cố', '/notifications': 'Thông báo', '/help': 'Trợ giúp & Góp ý',
  '/account/api-tracking': 'API Tracking', '/account/mytracking': 'MyTracking cá nhân', '/account/staff': 'Tài khoản nhân viên', '/account/password': 'Đổi mật khẩu', '/account/feedback': 'Góp ý'
};
function today() { return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date()); }
function daysAgo(date: string, days: number) { const d = new Date(`${date}T00:00:00Z`); d.setUTCDate(d.getUTCDate() - days); return d.toISOString().slice(0, 10); }

export function AdminUsage() {
  const { t } = useI18n();
  const [range, setRange] = useState(() => ({ from: daysAgo(today(), 29), to: today() }));
  const [audience, setAudience] = useState('all');
  const valid = !!range.from && !!range.to && range.from <= range.to && (Date.parse(range.to) - Date.parse(range.from)) / 86400000 < 366;
  const query = useQuery({
    queryKey: ['admin', 'analytics', range.from, range.to, audience],
    queryFn: async () => ((await (await adminFetch(`/admin/analytics?${new URLSearchParams({ ...range, audience })}`)).json()) as { data: Report }).data,
    enabled: valid,
    retry: (count, e) => !(e instanceof ApiError && (e.status === 401 || e.status === 403)) && count < 1
  });
  const d = query.data;
  const n = (value: number) => value.toLocaleString('vi-VN');
  const maxViews = Math.max(1, ...(d?.pages.map(p => p.views) ?? []));
  return <div className={styles.stack}>
    <Card title="Thống kê sử dụng">
      <div className={styles.filters}>
        <label>{t('Từ ngày')}<input type="date" value={range.from} max={range.to} onChange={e => setRange(r => ({ ...r, from: e.target.value }))} /></label>
        <label>{t('Đến ngày')}<input type="date" value={range.to} min={range.from} onChange={e => setRange(r => ({ ...r, to: e.target.value }))} /></label>
        <label>{t('Loại tài khoản')}<select value={audience} onChange={e => setAudience(e.target.value)}>
          <option value="all">{t('Tất cả')}</option><option value="customer">{t('Khách (tài khoản chính)')}</option><option value="staff">{t('Nhân viên của khách')}</option><option value="anonymous">{t('Chưa đăng nhập')}</option>
        </select></label>
        {[1, 7, 30].map(days => <Button key={days} size="sm" onClick={() => setRange({ from: daysAgo(today(), days - 1), to: today() })}>{t(days === 1 ? 'Hôm nay' : '{n} ngày', { n: days })}</Button>)}
        <Button size="sm" disabled={!valid || query.isFetching} onClick={() => void query.refetch()}>{t(query.isFetching ? 'Đang tải…' : 'Làm mới')}</Button>
      </div>
      <p className={styles.muted}>{t('Ngày thống kê theo giờ Việt Nam. Dữ liệu được ghi nhận từ khi bật tính năng; không bao gồm trang quản trị.')}</p>
    </Card>
    {!valid ? <Notice tone="warning">{t('Chọn khoảng ngày hợp lệ, tối đa 366 ngày.')}</Notice> : query.isError ? <Notice tone="danger">{getErrorMessage(query.error, 'Không tải được thống kê')}{query.error instanceof ApiError && query.error.status === 403 && <p>{t('Vui lòng đăng xuất và đăng nhập lại admin để nhận quyền thống kê mới.')}</p>}</Notice> : query.isPending ? <p>{t('Đang tải…')}</p> : d && <>
      <div className={styles.summary}>
        <Card title="Lượt xem trang"><strong className={styles.number}>{n(d.views)}</strong></Card>
        <Card title="Người truy cập riêng biệt"><strong className={styles.number}>{n(d.visitors)}</strong></Card>
        <Card title="Lượt tạo đơn thành công"><strong className={styles.number}>{n(d.modes.reduce((sum, m) => sum + m.completed, 0))}</strong></Card>
      </div>
      {!d.views && <EmptyState title="Chưa có dữ liệu truy cập" description="Lượt truy cập trong khoảng ngày và loại tài khoản đã chọn sẽ hiện ở đây khi khách sử dụng website." />}
      <Card title="So sánh hai cách tạo đơn">
        <div className={styles.scroll}><table className={styles.table}>
          <thead><tr>{['Kiểu tạo đơn', 'Lượt xem', 'Người dùng', 'Bắt đầu', 'Thành công', 'Tỷ lệ hoàn thành', 'Chuyển sang kiểu này'].map(s => <th key={s}>{t(s)}</th>)}</tr></thead>
          <tbody>{d.modes.map(m => <tr key={m.mode}><th scope="row">{t(m.mode === 'wizard' ? 'Từng bước' : '1 trang')}</th><td>{n(m.views)}</td><td>{n(m.visitors)}</td><td>{n(m.starts)}</td><td>{n(m.completed)}</td><td>{m.starts ? `${(100 * m.completed / m.starts).toFixed(1)}%` : '—'}</td><td>{n(m.switches)}</td></tr>)}</tbody>
        </table></div>
        <p className={styles.muted}>{t('Bắt đầu: lần điền form mới; tải lại hoặc tiếp tục cùng form không tính thêm. Thành công: lưu tạo đơn thành công, chưa phải in và cấp bill. Không tính sửa đơn nháp đã có. Một form dùng cả hai kiểu được tính ở cả hai; thành công thuộc kiểu dùng khi lưu.')}</p>
        <p className={styles.muted}>{t('Tỷ lệ tính trên các lần bắt đầu hoặc hoàn thành trong khoảng đã chọn; lần bắt đầu trước khoảng ngày nhưng hoàn thành trong khoảng vẫn được tính vào mẫu số.')}</p>
      </Card>
      <Card title="Trang được truy cập nhiều nhất">
        <div className={styles.scroll}><table className={styles.table}>
          <thead><tr><th>{t('Trang')}</th><th>{t('Lượt xem')}</th><th>{t('Người truy cập')}</th><th>{t('So sánh lượt xem')}</th></tr></thead>
          <tbody>{d.pages.map(p => <tr key={p.path}><th scope="row">{t(labels[p.path] ?? p.path)}<small>{p.path}</small></th><td>{n(p.views)}</td><td>{n(p.visitors)}</td><td><div className={styles.bar}><span style={{ width: `${p.views / maxViews * 100}%` }} /></div></td></tr>)}</tbody>
        </table></div>
        <p className={styles.muted}>{t('Khách đăng nhập được đếm theo tài khoản; người chưa đăng nhập được đếm theo trình duyệt. Một người dùng nhiều tài khoản hoặc trình duyệt có thể được tính nhiều lần.')}</p>
      </Card>
      <Card title="Truy cập theo ngày">
        <div className={styles.scroll}><table className={styles.table}><thead><tr><th>{t('Ngày')}</th><th>{t('Lượt xem')}</th><th>{t('Người truy cập')}</th></tr></thead><tbody>
          {d.days.map(day => <tr key={day.date}><th scope="row">{day.date.split('-').reverse().join('/')}</th><td>{n(day.views)}</td><td>{n(day.visitors)}</td></tr>)}
        </tbody></table></div>
      </Card>
    </>}
  </div>;
}
