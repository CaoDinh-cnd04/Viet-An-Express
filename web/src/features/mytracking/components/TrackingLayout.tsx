import type { CSSProperties, ReactNode } from 'react';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Icon } from '@/shared/ui';
import { CHANNELS, channelUrl, isHexColor, type Channel } from '../lib/channels';
import { addressSize } from '../lib/layout';
import { isWebUrl, type AdImage, type MyTrackingConfig } from '../schema';
import { ConfigImage } from './ConfigImage';
import styles from '../pages/MyTrackingPage.module.css';

/** Mục đã chọn còn dựng được link (giá trị sai thì ẩn nút). */
const links = (list: readonly Channel[]) =>
  list.map(c => ({ ...c, href: channelUrl(c.type, c.value), info: CHANNELS[c.type] })).filter(c => c.href);

interface Props {
  config: MyTrackingConfig;
  /** Khung tra cứu (ô nhập + kết quả) — preview dùng vùng chờ, trang công khai tra cứu thật. */
  children: ReactNode;
  /** Bố cục điện thoại (preview). */
  mobile?: boolean;
  /** Trang công khai /tracking/{mã khách}/...: toàn màn hình, chữ to hơn preview. */
  standalone?: boolean;
}

/**
 * Trang tra cứu mang thương hiệu của khách: logo (mở website trang home), thông tin công ty, ảnh quảng cáo,
 * mạng xã hội cuối trang, nút cổng liên lạc nổi bên trái; màu thương hiệu (nút, thanh tiêu đề) / màu phụ (nền dòng) thay màu mặc định.
 */
export function TrackingLayout({ config, children, mobile, standalone }: Props) {
  const { t } = useI18n();
  const { brand } = config;
  const contacts = links(brand.contacts);
  const socials = links(brand.socials);
  // Màu thương hiệu: mọi nút, thanh TRACKING DETAIL, tiêu đề bảng / quảng cáo. Màu phụ: nền các dòng trong nội dung tracking.
  const colors = {
    ...(isHexColor(brand.primaryColor) && { '--color-tracking-bar': brand.primaryColor, '--color-tracking-ad-button': brand.primaryColor }),
    ...(isHexColor(brand.secondaryColor) && { '--color-tracking-row': brand.secondaryColor })
  } as CSSProperties;
  const website = isWebUrl(brand.website) ? brand.website : '';
  // Không có ảnh quảng cáo và mô tả: ẩn hẳn cột quảng cáo, khung tra cứu chiếm toàn bộ chiều ngang.
  const hasAds = config.images.length > 0;
  const showAside = hasAds || config.description.trim() !== '';
  const logo = brand.logo
    ? <ConfigImage src={brand.logo} alt="Logo" className={styles.logo} />
    : <span className={styles.neutralLogo}>Logo</span>;

  return <div className={cx(styles.previewPage, mobile && styles.mobile, standalone && styles.standalone)} style={colors}>
    {config.background && <div className={styles.backdrop} aria-hidden="true"><ConfigImage src={config.background} alt="" className={styles.backgroundImage} /></div>}
    <div className={styles.trackingShell}>
      <header className={styles.brandHeader}>
        {website
          ? <a className={styles.logoLink} href={website} target="_blank" rel="noreferrer" aria-label={t('Website {name}', { name: brand.companyName })}>{logo}</a>
          : logo}
        <div className={styles.brandInfo}>
          <strong className={styles.twoLines} title={brand.companyName}>{brand.companyName || t('Tên công ty của bạn')}</strong>
          <p className={cx(styles.twoLines, styles[`address_${addressSize(brand.address)}`])} title={brand.address}>
            <b>Add:</b> {brand.address || t('Địa chỉ công ty')}
          </p>
          <p><b>Tel:</b> {brand.phone || t('Số điện thoại liên hệ')}</p>
          {website && <p><b>Web:</b> <a href={website} target="_blank" rel="noreferrer">{website.replace(/^https?:\/\//, '').replace(/\/$/, '')}</a></p>}
        </div>
      </header>
      <div className={cx(styles.trackingColumns, !showAside && styles.singleColumn)}>
        <div className={styles.trackingMain}>{children}</div>
        {showAside && <aside className={styles.advertisingPanel}>
          <h3 className={styles.promotionTitle}>{config.title}</h3>
          {config.description && <p className={styles.description}>{config.description}</p>}
          {config.images.map((image, index) => <Advertisement key={image.id} image={image} index={index} />)}
        </aside>}
      </div>
      {socials.length > 0 && <footer className={styles.connect}>
        <strong>Connect with us:</strong>
        <div className={styles.socialIcons}>{socials.map(c => <ChannelButton key={c.type} channel={c} size={20} />)}</div>
      </footer>}
    </div>
    {contacts.length > 0 && <nav className={styles.floatingContacts} aria-label={t('Liên hệ')}>
      {contacts.map(c => <ChannelButton key={c.type} channel={c} size={22} />)}
    </nav>}
  </div>;
}

/**
 * Nút Home cạnh nút Track: mở website trang home của khách (ô "Website trang home").
 * Chưa nhập link: trang công khai ẩn nút; bản xem trước (`preview`) hiện nút mờ để khách biết chỗ nhập.
 */
export function HomeButton({ website, preview }: { website: string; preview?: boolean }) {
  const { t } = useI18n();
  if (!isWebUrl(website)) {
    return preview
      ? <button type="button" className={styles.homeButton} disabled title={t('Nhập "Website trang home" ở mục Thông tin thương hiệu để bật nút này')}>
          <Icon name="home" size={14} /> Home
        </button>
      : null;
  }
  return <a className={styles.homeButton} href={website} target="_blank" rel="noreferrer" title={website}>
    <Icon name="home" size={14} /> Home
  </a>;
}

/** Nút 1 cổng liên lạc / mạng xã hội: biểu tượng trên nền màu của ứng dụng; link web mở tab mới. */
function ChannelButton({ channel, size }: { channel: ReturnType<typeof links>[number]; size: number }) {
  const { info, href, value } = channel;
  const web = href.startsWith('http');
  if (info.logo) {
    return <a href={href} target={web ? '_blank' : undefined} rel={web ? 'noreferrer' : undefined}
      aria-label={info.label} title={`${info.label}: ${value}`} className={styles.logoButton}>
      <img src={info.logo} alt="" aria-hidden="true" />
    </a>;
  }
  return <a href={href} target={web ? '_blank' : undefined} rel={web ? 'noreferrer' : undefined}
    aria-label={info.label} title={`${info.label}: ${value}`} style={{ background: info.color }}>
    {info.glyph
      ? <img src={info.glyph} alt="" aria-hidden="true" width={size} height={size} />
      : <Icon name={info.icon ?? 'link'} size={size - 4} />}
  </a>;
}

function Advertisement({ image, index }: { image: AdImage; index: number }) {
  const { t } = useI18n();
  const linked = isWebUrl(image.linkUrl);
  const content = <>
    <ConfigImage src={image.src} alt={image.title || t('Ảnh quảng cáo {n}', { n: index + 1 })} className={styles.adImage} />
    {(image.title || image.buttonText) && <div className={styles.adCaption}>
      {image.title && <strong>{image.title}</strong>}
      {image.buttonText && (linked
        ? <span className={styles.adButton}>{image.buttonText}</span>
        : <button type="button" className={styles.adButton} disabled>{image.buttonText}</button>)}
    </div>}
  </>;
  return linked
    ? <a className={styles.advertisement} href={image.linkUrl} target="_blank" rel="noreferrer">{content}</a>
    : <div className={styles.advertisement}>{content}</div>;
}
