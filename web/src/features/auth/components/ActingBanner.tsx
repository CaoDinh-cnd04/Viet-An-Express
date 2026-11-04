import { useLocation } from 'react-router-dom';
import { useI18n } from '@/shared/i18n';
import { LinkButton } from '@/shared/ui';
import { useSession } from '../api';
import { CHOOSE_CUSTOMER_PATH, isVietAnAgent } from '../lib/session';
import styles from './ActingBanner.module.css';

/**
 * Thanh luôn hiện khi nhân viên Việt An làm thay khách: ai đang làm cho khách nào + nút đổi khách.
 * Tài khoản khách và trang chọn khách: không hiện.
 */
export function ActingBanner() {
  const { t } = useI18n();
  const session = useSession();
  const { pathname } = useLocation();
  const user = session.data?.status === 'authenticated' ? session.data.user : undefined;
  if (!user || !isVietAnAgent(user) || pathname === CHOOSE_CUSTOMER_PATH) return null;

  return (
    <div className={styles.bar} role="status">
      <span>
        {user.customerCode
          ? t('Nhân viên {agent} đang làm cho khách {code} — {name}', { agent: user.agentName ?? user.agentUserName ?? '', code: user.customerCode, name: user.companyName })
          : t('Nhân viên {agent} — chưa chọn khách', { agent: user.agentName ?? user.agentUserName ?? '' })}
      </span>
      <LinkButton to={CHOOSE_CUSTOMER_PATH} size="sm">{t(user.customerCode ? 'Đổi khách' : 'Chọn khách')}</LinkButton>
    </div>
  );
}
