import { useState, type ReactNode } from 'react';
import { PORTAL_HOME } from '@/features/auth';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Button, Icon, LinkButton, Modal } from '@/shared/ui';
import { CARRIERS, COMPANY, CONTACTS, FAQ, GALLERY, LANES, PORTAL_FEATURES, PROCESS, SERVICES, VALUES } from '../constants';
import { yearsSince } from '../lib/company';
import styles from './Sections.module.css';

interface SectionProps {
  id: string;
  eyebrow: string;
  title: string;
  lead?: string;
  /** plain: nền trắng · muted: nền xám rất nhạt · dark: nền navy chữ trắng */
  tone?: 'plain' | 'muted' | 'dark';
  /** Tiêu đề canh trái (mặc định giữa). */
  align?: 'center' | 'left';
  /** Nội dung phụ bên phải tiêu đề (khi canh trái). */
  aside?: ReactNode;
  children: ReactNode;
}

/** Khung 1 phần của trang ngoài: nhãn nhỏ + tiêu đề + mô tả + nội dung. */
export function Section({ id, eyebrow, title, lead, tone = 'plain', align = 'center', aside, children }: SectionProps) {
  const { t } = useI18n();
  return (
    <section id={id} className={cx(styles.section, tone === 'muted' && styles.muted, tone === 'dark' && styles.dark)} aria-labelledby={`${id}-title`}>
      <div className={styles.container}>
        <header className={cx(styles.sectionHead, align === 'left' && styles.headLeft)}>
          <div>
            <span className={styles.eyebrow}>{t(eyebrow)}</span>
            <h2 id={`${id}-title`} className={styles.title}>
              {t(title)}
            </h2>
            {lead && <p className={styles.lead}>{t(lead)}</p>}
          </div>
          {aside}
        </header>
        {children}
      </div>
    </section>
  );
}

/** Dịch vụ: 6 ô trong 1 khung kẻ ô, kèm dòng các hãng Việt An làm đại lý. */
export function ServicesSection() {
  const { t } = useI18n();
  return (
    <Section
      id="dich-vu"
      eyebrow="Dịch vụ"
      title="Một đầu mối cho mọi lô hàng"
      align="left"
      aside={
        <p className={styles.carriers}>
          {t('Đại lý')}: <strong>{CARRIERS.join(' · ')}</strong>
        </p>
      }
    >
      <div className={styles.serviceGrid}>
        {SERVICES.map(s => (
          <article key={s.title} className={styles.service}>
            <Icon name={s.icon} size={26} className={styles.serviceIcon} />
            <h3 className={styles.cardTitle}>{t(s.title)}</h3>
            <p className={styles.cardText}>{t(s.desc)}</p>
          </article>
        ))}
      </div>
    </Section>
  );
}

/** Quy trình gửi hàng 4 bước trên nền navy. */
export function ProcessSection() {
  const { t } = useI18n();
  return (
    <Section id="quy-trinh" tone="dark" eyebrow="Quy trình" title="Gửi hàng chỉ với 4 bước" align="left">
      <ol className={styles.process}>
        {PROCESS.map((p, i) => (
          <li key={p.title}>
            <span className={styles.processNo}>{String(i + 1).padStart(2, '0')}</span>
            <strong>{t(p.title)}</strong>
            <span>{t(p.desc)}</span>
          </li>
        ))}
      </ol>
    </Section>
  );
}

/** Tuyến chuyên: lưới ảnh lớn có cờ (7 tuyến + 1 ô mời hỏi giá nước khác = 2 hàng × 4). */
export function LanesSection() {
  const { t } = useI18n();
  return (
    <Section
      id="tuyen"
      eyebrow="Tuyến chủ lực"
      title={t('Giá tốt trên {n} tuyến chuyên', { n: LANES.length })}
      lead="Ngoài các tuyến dưới đây, Việt An nhận gửi đi hầu hết quốc gia qua mạng lưới hãng quốc tế."
      align="left"
    >
      <div className={styles.laneGrid}>
        {LANES.map(l => (
          <article key={l.code} className={styles.laneTile}>
            <img src={`/landing/lanes/${l.code.toLowerCase()}.jpg`} alt={t(l.landmark)} loading="lazy" width={960} height={549} />
            <span className={styles.laneLabel}>
              <img className={styles.laneFlag} src={`/flags/${l.code.toLowerCase()}.svg`} alt="" loading="lazy" />
              {t(l.country)}
            </span>
          </article>
        ))}
        <a href="#lien-he" className={cx(styles.laneTile, styles.laneMore)}>
          <Icon name="globe" size={28} />
          <strong>{t('Nước khác?')}</strong>
          <span>{t('Hỏi giá ngay')} →</span>
        </a>
      </div>
    </Section>
  );
}

/** Vì sao chọn Việt An: 4 giá trị + ảnh hoạt động dạng khảm (bấm để xem lớn). */
export function AboutSection() {
  const { t } = useI18n();
  const [photo, setPhoto] = useState<number | null>(null);
  const current = photo === null ? undefined : GALLERY[photo];
  const step = (delta: number) => setPhoto(p => (p === null ? p : (p + delta + GALLERY.length) % GALLERY.length));
  const shown = GALLERY.slice(0, 5);

  return (
    <Section
      id="ve-chung-toi"
      tone="muted"
      eyebrow="Vì sao chọn Việt An"
      title={t('{n} năm đồng hành cùng hàng Việt ra thế giới', { n: yearsSince(COMPANY.foundedYear) })}
      lead={t('Trụ sở tại TP. Hồ Chí Minh cùng chi nhánh Hà Nội, Cần Thơ và Bảo Lộc giúp bạn gửi hàng nhanh và thuận tiện hơn.')}
      align="left"
    >
      <ul className={styles.valueGrid}>
        {VALUES.map(v => (
          <li key={v.title}>
            <Icon name={v.icon} size={26} className={styles.serviceIcon} />
            <strong>{t(v.title)}</strong>
            <span className={styles.cardText}>{t(v.desc)}</span>
          </li>
        ))}
      </ul>

      <div className={styles.mosaic}>
        {shown.map((g, i) => (
          <button key={g.src} type="button" onClick={() => setPhoto(i)} aria-label={t('Xem ảnh: {name}', { name: t(g.alt) })}>
            <img src={g.src} alt="" loading="lazy" />
          </button>
        ))}
      </div>

      {current && (
        <Modal
          open
          size="lg"
          title={current.alt}
          onClose={() => setPhoto(null)}
          footer={
            <>
              <Button onClick={() => step(-1)}>{t('Ảnh trước')}</Button>
              <Button onClick={() => step(1)}>{t('Ảnh sau')}</Button>
            </>
          }
        >
          <img className={styles.lightbox} src={current.src} alt={t(current.alt)} />
        </Modal>
      )}
    </Section>
  );
}

/** Portal doanh nghiệp: danh sách tính năng 2 cột + ảnh kho, nút đăng nhập / vào portal. */
export function PortalSection({ loggedIn, onLogin }: { loggedIn: boolean; onLogin: () => void }) {
  const { t } = useI18n();
  return (
    <section id="portal" className={styles.section} aria-labelledby="portal-title">
      <div className={cx(styles.container, styles.portal)}>
        <div>
          <span className={styles.eyebrow}>{t('Portal doanh nghiệp')}</span>
          <h2 id="portal-title" className={styles.title}>{t('Tự tạo đơn, in bill, theo dõi 24/7')}</h2>
          <p className={styles.lead}>{t('Dành cho doanh nghiệp và người bán hàng online gửi thường xuyên.')}</p>
          <ul className={styles.featureGrid}>
            {PORTAL_FEATURES.map(f => (
              <li key={f.title}>
                <Icon name="check" size={18} />
                {t(f.title)}
              </li>
            ))}
          </ul>
          <div className={styles.portalActions}>
            {loggedIn ? (
              <LinkButton to={PORTAL_HOME} variant="primary" className={styles.bigButton}>
                {t('Vào portal')} <Icon name="arrowRight" size={16} />
              </LinkButton>
            ) : (
              <Button variant="primary" className={styles.bigButton} onClick={onLogin}>
                {t('Đăng nhập portal')} <Icon name="arrowRight" size={16} />
              </Button>
            )}
            <span className={styles.cardText}>
              {t('Chưa có tài khoản? Gọi')} <a href={CONTACTS.hotline.href}>{CONTACTS.hotline.label}</a> {t('để được cấp.')}
            </span>
          </div>
        </div>
        <img className={styles.portalPhoto} src="/landing/kiem-va-dong.jpg" alt={t('Kiểm và đóng gói hàng tại kho')} loading="lazy" />
      </div>
    </section>
  );
}

/** Dải kêu gọi báo giá màu xanh lá. */
export function CtaBand() {
  const { t } = useI18n();
  return (
    <div className={styles.ctaWrap}>
      <div className={styles.cta}>
        <div>
          <h2 className={styles.ctaTitle}>{t('Cần báo giá cho lô hàng?')}</h2>
          <p>{t('Gửi nước đến và cân nặng — CS Việt An phản hồi trong giờ làm việc.')}</p>
        </div>
        <div className={styles.ctaActions}>
          <a href="#lien-he" className={styles.ctaPrimary}>{t('Nhận báo giá')}</a>
          <a href={CONTACTS.zalo.href} target="_blank" rel="noreferrer" className={styles.ctaGhost}>{t('Chat Zalo')}</a>
        </div>
      </div>
    </div>
  );
}

/** Câu hỏi thường gặp — bấm để mở từng câu. */
export function FaqSection() {
  const { t } = useI18n();
  return (
    <Section id="hoi-dap" tone="muted" eyebrow="Hỏi đáp" title="Câu hỏi thường gặp">
      <div className={styles.faq}>
        {FAQ.map((f, i) => (
          <details key={f.q} open={i === 0}>
            <summary>
              {t(f.q)}
              <Icon name="chevronDown" size={18} />
            </summary>
            <p>{t(f.a)}</p>
          </details>
        ))}
      </div>
    </Section>
  );
}
