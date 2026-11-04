import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useI18n } from '@/shared/i18n';
import { Button, Card, EmptyState } from '@/shared/ui';
import { useSession } from '../api';
import { CHOOSE_CUSTOMER_PATH, loginPath, needsCustomer } from '../lib/session';
import styles from './RequireAuth.module.css';

/**
 * Chặn các trang portal khi chưa đăng nhập — chuyển về /login?next=<trang đang mở>.
 * Nhân viên Việt An chưa chọn khách → chuyển sang trang chọn khách (chọn xong quay lại trang đang mở).
 */
export function RequireAuth({ children }: { children: ReactNode }) {
  const session = useSession();
  const { t } = useI18n();
  const { pathname, search } = useLocation();

  if (session.isPending) {
    return <p className={styles.center}>{t('Đang kiểm tra đăng nhập…')}</p>;
  }
  if (session.isError) {
    return (
      <div className={styles.center}>
        <Card>
          <EmptyState
            title="Không kết nối được máy chủ"
            description="Vui lòng kiểm tra mạng rồi thử lại."
            action={<Button variant="primary" onClick={() => void session.refetch()}>{t('Thử lại')}</Button>}
          />
        </Card>
      </div>
    );
  }
  if (session.data.status === 'anonymous') {
    return <Navigate to={loginPath(pathname + search)} replace />;
  }
  if (session.data.status === 'authenticated' && needsCustomer(session.data.user, pathname)) {
    return <Navigate to={`${CHOOSE_CUSTOMER_PATH}?next=${encodeURIComponent(pathname + search)}`} replace />;
  }
  return children;
}
