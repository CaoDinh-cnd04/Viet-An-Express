import { useEffect, useRef } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { LoginForm, useSession } from '@/features/auth';
import { ApiError, getErrorMessage } from '@/shared/api/http';
import { CONTACTS } from '@/shared/config/company';
import { useI18n } from '@/shared/i18n';
import { Button, Card, EmptyState, Icon } from '@/shared/ui';
import { useClaimShopifyInstall } from '../api';
import styles from './ShopifyConnectPage.module.css';

const CONNECT_TAB = '/ecommerce?tab=connect';
const SELF = '/shopify';
const LANG_KEY = 'va.lang';

/**
 * Trang mở app từ Shopify (application_url → backend OAuth → đây). Công khai: chưa đăng nhập thì giải thích + form đăng nhập
 * ngay tại chỗ (mặc định tiếng Anh cho người dùng Shopify, đổi được VI/EN), không chuyển về trang chủ, không sinh lỗi 401.
 * Đã đăng nhập → gắn shop vừa cài vào tài khoản rồi về tab Kết nối; không có shop chờ (mở lại app) → về tab Kết nối luôn.
 */
export default function ShopifyConnectPage() {
  const { t, lang, setLang } = useI18n();
  const session = useSession();
  const navigate = useNavigate();
  const claim = useClaimShopifyInstall();
  const started = useRef(false);
  const signedIn = session.data?.status === 'authenticated' || session.data?.status === 'open';

  // Lần đầu (chưa từng chọn ngôn ngữ): tiếng Anh cho người dùng Shopify.
  useEffect(() => {
    try {
      if (window.localStorage.getItem(LANG_KEY) === null) setLang('en');
    } catch {
      /* storage bị chặn: giữ mặc định */
    }
  }, [setLang]);

  useEffect(() => {
    if (!signedIn || started.current) return;
    started.current = true;
    claim.mutate(undefined, {
      onSuccess: store => void navigate(store ? `${CONNECT_TAB}&connected=shopify` : CONNECT_TAB, { replace: true }),
      onError: e => {
        if (e instanceof ApiError && e.code === 'ECOM_INSTALL_NOT_FOUND') void navigate(CONNECT_TAB, { replace: true });
      }
    });
  }, [signedIn, claim, navigate]);

  return (
    <main className={styles.page}>
      <div className={styles.wrap}>
        <header className={styles.head}>
          <Link to="/" className={styles.brand}>
            <img src="/logo.webp" alt="" className={styles.logo} />
            <span>Việt An Express</span>
          </Link>
          <div className={styles.lang} role="group" aria-label={t('Ngôn ngữ')}>
            <Button size="sm" variant={lang === 'en' ? 'primary' : 'ghost'} onClick={() => setLang('en')}>EN</Button>
            <Button size="sm" variant={lang === 'vi' ? 'primary' : 'ghost'} onClick={() => setLang('vi')}>VI</Button>
          </div>
        </header>

        <Card>
          {session.isPending ? (
            <p className={styles.muted}>{t('Đang kiểm tra đăng nhập…')}</p>
          ) : signedIn ? (
            claim.isError && !(claim.error instanceof ApiError && claim.error.code === 'ECOM_INSTALL_NOT_FOUND') ? (
              <EmptyState title="Chưa kết nối được cửa hàng Shopify"
                description={claim.error instanceof ApiError && claim.error.status === 403
                  ? t('Tài khoản này chưa được cấp quyền kết nối cửa hàng. Đăng nhập bằng tài khoản chính của công ty hoặc nhờ quản trị viên cấp quyền "Kết nối / ngắt kết nối cửa hàng", rồi mở lại ứng dụng từ Shopify admin.')
                  : getErrorMessage(claim.error)}
                action={<Link to={CONNECT_TAB}>{t('Mở trang Kết nối')}</Link>} />
            ) : (
              <EmptyState title="Đang kết nối cửa hàng Shopify…" description="Đang gắn cửa hàng vừa cài vào tài khoản của bạn." />
            )
          ) : (
            <div className={styles.body}>
              <div>
                <h1 className={styles.title}>{t('Kết nối cửa hàng Shopify với Việt An Express')}</h1>
                <p className={styles.lead}>
                  {t('Đăng nhập tài khoản Việt An Express để hoàn tất kết nối. Sau đó đơn hàng Shopify tự về mục E-commerce để tạo vận đơn quốc tế.')}
                </p>
                <ol className={styles.steps}>
                  <li>{t('Đăng nhập tài khoản Việt An Express')}</li>
                  <li>{t('Cửa hàng vừa cài tự gắn vào tài khoản')}</li>
                  <li>{t('Đơn hàng Shopify mới tự đồng bộ về E-commerce → Đơn hàng')}</li>
                </ol>
              </div>
              <LoginForm autoFocus next={SELF} />
              <div className={styles.account}>
                <strong>{t('Chưa có tài khoản Việt An Express?')}</strong>
                <span>{t('Gửi yêu cầu, Việt An tạo tài khoản và gửi thông tin đăng nhập cho bạn trong ngày làm việc.')}</span>
                <div className={styles.contacts}>
                  <a href={`${CONTACTS.email.href}?subject=${encodeURIComponent('Shopify - request a Viet An Express account')}`}>
                    <Icon name="mail" size={14} /> {CONTACTS.email.label}
                  </a>
                  <a href={CONTACTS.zalo.href} target="_blank" rel="noreferrer"><Icon name="message" size={14} /> Zalo</a>
                  <a href={CONTACTS.hotline.href}><Icon name="phone" size={14} /> {CONTACTS.hotline.label}</a>
                </div>
              </div>
            </div>
          )}
        </Card>
      </div>
    </main>
  );
}
