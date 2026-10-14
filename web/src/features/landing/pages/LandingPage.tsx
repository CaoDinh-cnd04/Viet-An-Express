import { useEffect } from 'react';
import { Navigate, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { useSession } from '@/features/auth';
import { useI18n } from '@/shared/i18n';
import { Icon } from '@/shared/ui';
import { ContactSection } from '../components/ContactSection';
import { FloatingContact } from '../components/FloatingContact';
import { HeroPanel, TrackBar } from '../components/HeroPanel';
import { AboutSection, CtaBand, FaqSection, LanesSection, PortalSection, ProcessSection, ServicesSection } from '../components/Sections';
import { SiteFooter } from '../components/SiteFooter';
import { SiteHeader } from '../components/SiteHeader';
import { BRANCHES, COMPANY, CONTACTS, LANES } from '../constants';
import { yearsSince } from '../lib/company';
import { billsFromQuery } from '../lib/tracking';
import { trackingPath } from '../lib/tracking-detail';
import styles from './LandingPage.module.css';

/**
 * Trang giới thiệu (không cần đăng nhập) — bố cục "ảnh lớn": ảnh trụ sở phủ navy, tiêu đề giữa, ô tra cứu vận đơn ngang,
 * dải số liệu nổi; tiếp theo dịch vụ, quy trình, tuyến chuyên, vì sao chọn Việt An, portal, kêu gọi báo giá, hỏi đáp, liên hệ.
 * `/login` hiện khung đăng nhập thay ô tra cứu. Tra cứu chuyển sang /tracking/MA1,MA2; link cũ `?track=` được chuyển hướng tới đó.
 */
export default function LandingPage() {
  const { t } = useI18n();
  const [params] = useSearchParams();
  const { pathname, search } = useLocation();
  const navigate = useNavigate();
  const session = useSession();
  const loggedIn = session.data?.status === 'authenticated';
  const loginMode = pathname === '/login';

  const legacyTrack = billsFromQuery(params.get('track'));

  useEffect(() => {
    document.title = `${COMPANY.name} — ${t('Chuyển phát nhanh quốc tế')}`;
    // Vào từ trang khác với /#muc → cuộn tới mục sau khi trang dựng xong.
    const id = window.location.hash.slice(1);
    if (id) requestAnimationFrame(() => document.getElementById(id)?.scrollIntoView());
  }, [t]);

  const track = (bills: string[]) => void navigate(trackingPath(bills));

  const openLogin = () => {
    void navigate({ pathname: '/login', search }, { replace: true });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const stats = [
    { value: `${yearsSince(COMPANY.foundedYear)}+`, label: 'năm kinh nghiệm' },
    { value: String(BRANCHES.length), label: 'chi nhánh' },
    { value: String(LANES.length), label: 'tuyến chuyên' },
    { value: '24/7', label: 'theo dõi hành trình' }
  ];

  if (legacyTrack.length > 0) return <Navigate to={trackingPath(legacyTrack)} replace />;

  return (
    <div className={styles.page}>
      <SiteHeader onLogin={openLogin} />

      <main>
        <section className={styles.hero}>
          <div className={styles.heroInner}>
            <span className={styles.badge}>
              <Icon name="globe" size={14} /> {t('Chuyển phát nhanh quốc tế từ {year}', { year: COMPANY.foundedYear })}
            </span>
            <h1 className={styles.heroTitle}>
              {t('Gửi hàng đi nước ngoài')}
              <br />
              <span className={styles.accent}>{t('nhanh, an toàn, tiết kiệm')}</span>
            </h1>
            <p className={styles.heroLead}>
              {t('Đại lý gom hàng của DHL, FedEx, UPS, TNT — lấy hàng tận nơi, đóng gói miễn phí, cập nhật hành trình liên tục.')}
            </p>
            {loginMode ? (
              <div className={styles.loginBox}>
                <HeroPanel initialBills={[]} onTrack={track} tracking={false} />
              </div>
            ) : (
              <>
                <TrackBar onTrack={track} />
                <p className={styles.heroAlt}>
                  {t('hoặc')} <a href="#lien-he">{t('nhận báo giá miễn phí')}</a> · {t('gọi')}{' '}
                  <a href={CONTACTS.hotline.href}>{CONTACTS.hotline.label}</a>
                </p>
              </>
            )}
          </div>
        </section>

        {/* Dải số liệu nổi chồng lên mép dưới hero */}
        <div className={styles.statWrap}>
          <dl className={styles.stats}>
            {stats.map(s => (
              <div key={s.label}>
                <dd>{s.value}</dd>
                <dt>{t(s.label)}</dt>
              </div>
            ))}
          </dl>
        </div>

        <ServicesSection />
        <ProcessSection />
        <LanesSection />
        <AboutSection />
        <PortalSection loggedIn={loggedIn} onLogin={openLogin} />
        <CtaBand />
        <FaqSection />
        <ContactSection />
      </main>

      <SiteFooter />
      <FloatingContact />
    </div>
  );
}
