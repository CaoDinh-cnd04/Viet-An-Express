import { useState, type FormEvent } from 'react';
import { getErrorMessage } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { useTheme } from '@/shared/lib/useTheme';
import { Button, Card, Icon, Notice, Tabs, TextField } from '@/shared/ui';
import { adminLogout, getAdminToken, useAdminTroubles, useAdminLogin } from '../api';
import { AdminNotifications } from '../components/AdminNotifications';
import { AdminTroubles } from '../components/AdminTroubles';
import { AdminUsage } from '../components/AdminUsage';
import styles from './AdminPage.module.css';

type Section = 'troubles' | 'notifications' | 'analytics';

/**
 * Trang quản trị Việt An (/admin) — chỉ tài khoản quản trị cố định trong code đăng nhập được.
 * Tab Hỗ trợ & sự cố: sự cố vận đơn, yêu cầu hỗ trợ, góp ý khách gửi — trả lời / đổi trạng thái (khách nhận thông báo riêng).
 * Tab Thông báo: soạn thông báo gửi mọi khách (tùy chọn gửi email), xem kết quả, xóa.
 */
export default function AdminPage() {
  const { t } = useI18n();
  const [signedIn, setSignedIn] = useState(() => !!getAdminToken());
  const [section, setSection] = useState<Section>('troubles');
  const troubles = useAdminTroubles(signedIn);
  // Token hết hạn (401) → về màn đăng nhập.
  const expired = signedIn && troubles.isError && !getAdminToken();

  if (!signedIn || expired) return <AdminLogin onSignedIn={() => setSignedIn(true)} expired={expired} />;

  return (
    <main className={styles.page}>
      <div className={styles.wrap}>
        <header className={styles.head}>
          <div>
            <h1 className={styles.title}>{t('Quản trị Việt An Express')}</h1>
            <p className={styles.muted}>{t('Hỗ trợ & sự cố của khách và thông báo gửi khách')}</p>
          </div>
          <div className={styles.headActions}>
            <ThemeToggle />
            <Button size="sm" variant="ghost" onClick={() => { adminLogout(); setSignedIn(false); }}>
              <Icon name="arrowRight" size={14} /> {t('Đăng xuất')}
            </Button>
          </div>
        </header>
        <Tabs
          ariaLabel="Chức năng quản trị"
          items={[
            { key: 'troubles', label: 'Hỗ trợ & sự cố', count: troubles.data?.filter(x => x.ticket.status === 'new').length || undefined },
            { key: 'notifications', label: 'Thông báo' },
            { key: 'analytics', label: 'Thống kê sử dụng' }
          ]}
          value={section}
          onChange={setSection}
        />
        {section === 'troubles' ? (
          <AdminTroubles />
        ) : section === 'analytics' ? (
          <AdminUsage />
        ) : (
          <AdminNotifications />
        )}
      </div>
    </main>
  );
}

/** Nút chuyển giao diện sáng / tối — dùng chung lựa chọn với portal (mặc định theo hệ điều hành). */
function ThemeToggle() {
  const { t } = useI18n();
  const { theme, toggle } = useTheme();
  const label = t(theme === 'dark' ? 'Chuyển giao diện sáng' : 'Chuyển giao diện tối');
  return (
    <Button size="sm" variant="ghost" iconOnly onClick={toggle} aria-label={label} title={label}>
      <Icon name={theme === 'dark' ? 'sun' : 'moon'} size={16} />
    </Button>
  );
}

function AdminLogin({ onSignedIn, expired }: { onSignedIn: () => void; expired: boolean }) {
  const { t } = useI18n();
  const login = useAdminLogin();
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const submit = (e: FormEvent) => {
    e.preventDefault();
    login.mutate({ userName, password }, { onSuccess: onSignedIn });
  };
  return (
    <main className={styles.page}>
      <form className={styles.login} onSubmit={submit}>
        <div className={styles.loginTheme}>
          <ThemeToggle />
        </div>
        <Card title="Quản trị Việt An Express">
          <div className={styles.loginBody}>
            {expired && <Notice tone="warning">{t('Phiên quản trị đã hết hạn, vui lòng đăng nhập lại.')}</Notice>}
            {login.isError && <Notice tone="danger">{getErrorMessage(login.error)}</Notice>}
            <TextField label="Tên đăng nhập" required autoComplete="username" value={userName} onChange={e => setUserName(e.target.value)} autoFocus />
            <TextField label="Mật khẩu" required type="password" autoComplete="current-password" value={password} onChange={e => setPassword(e.target.value)} />
            <Button variant="primary" type="submit" disabled={login.isPending || !userName || !password}>
              {t(login.isPending ? 'Đang đăng nhập…' : 'Đăng nhập')}
            </Button>
          </div>
        </Card>
      </form>
    </main>
  );
}
