import { useNavigate } from 'react-router-dom';
import { useI18n } from '@/shared/i18n';
import { Button } from '@/shared/ui';
import { useActAsCustomer } from '../api';
import { MANAGED_ORDERS_PATH } from '../lib/session';

/**
 * Nhân viên Việt An đang làm cho 1 khách → "Về trang quản trị": bỏ khách đang chọn (POST /auth/vietan/act-as `customerId: 0`),
 * về phiên chưa gắn khách — chỉ còn Đơn khách phụ trách, Hỗ trợ & góp ý, Chọn khách; không có tạo đơn / book bill (đỡ nhầm khách).
 */
export function BackToAdminButton() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const actAs = useActAsCustomer();
  return (
    <Button size="sm" disabled={actAs.isPending} onClick={() => actAs.mutate(0, { onSuccess: () => void navigate(MANAGED_ORDERS_PATH, { replace: true }) })}>
      {t('Về trang quản trị')}
    </Button>
  );
}
