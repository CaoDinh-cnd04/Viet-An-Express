/**
 * Cổng liên lạc (nút nổi bên trái trang tracking) và mạng xã hội (cuối trang): khách chọn tối đa 5 mục mỗi loại.
 * Mỗi mục nhập link http(s):// hoặc giá trị ngắn (số điện thoại / tên tài khoản / email) — channelUrl dựng link mở được.
 */

export const MAX_CHANNELS = 5;
/** Ô CongTinNhan / CongTruyenThong, cột Email / Website của bảng cũ: nvarchar(250). */
export const MAX_CHANNEL_VALUE = 250;

export const CONTACT_TYPES = [
  'hotline', 'zalo', 'messenger', 'whatsapp', 'wechat', 'telegram', 'viber', 'line', 'snapchat', 'kakaotalk', 'instagram', 'email'
] as const;
export const SOCIAL_TYPES = ['facebook', 'instagram', 'youtube', 'tiktok', 'twitter', 'googlemaps', 'pinterest'] as const;

export type ContactType = (typeof CONTACT_TYPES)[number];
export type SocialType = (typeof SOCIAL_TYPES)[number];
export type ChannelType = ContactType | SocialType;

export interface ChannelInfo {
  label: string;
  /** Logo màu chính thức (public/social) — lấp kín nút, không nền màu; ưu tiên hơn glyph. */
  logo?: string;
  /** Glyph trong public/social (Simple Icons, CC0) đặt trên nền `color`; không có = dùng <Icon name>. */
  glyph?: string;
  icon?: 'phone' | 'mail';
  /** Biến màu nền nút trong tokens.css. */
  color: string;
  placeholder: string;
}

export const CHANNELS: Record<ChannelType, ChannelInfo> = {
  hotline: { label: 'Hotline', icon: 'phone', color: 'var(--color-tracking-bar)', placeholder: 'Số hotline, vd 0909 123 456' },
  zalo: { label: 'Zalo', logo: '/social/zalo-app.svg', glyph: '/social/zalo-glyph.svg', color: 'var(--color-social-zalo)', placeholder: 'Số Zalo hoặc link zalo.me' },
  messenger: { label: 'Messenger', glyph: '/social/messenger-glyph.svg', color: 'var(--color-social-messenger)', placeholder: 'Tên trang Facebook hoặc link m.me' },
  whatsapp: { label: 'WhatsApp', glyph: '/social/whatsapp-glyph.svg', color: 'var(--color-social-whatsapp)', placeholder: 'Số WhatsApp (kèm mã quốc gia)' },
  wechat: { label: 'WeChat', glyph: '/social/wechat-glyph.svg', color: 'var(--color-social-wechat)', placeholder: 'WeChat ID' },
  telegram: { label: 'Telegram', glyph: '/social/telegram-glyph.svg', color: 'var(--color-social-telegram)', placeholder: 'Tên Telegram (@ten) hoặc link t.me' },
  viber: { label: 'Viber', glyph: '/social/viber-glyph.svg', color: 'var(--color-social-viber)', placeholder: 'Số Viber (kèm mã quốc gia)' },
  line: { label: 'Line', glyph: '/social/line-glyph.svg', color: 'var(--color-social-line)', placeholder: 'LINE ID hoặc link line.me' },
  snapchat: { label: 'Snapchat', glyph: '/social/snapchat-glyph.svg', color: 'var(--color-social-snapchat)', placeholder: 'Tên Snapchat' },
  kakaotalk: { label: 'KakaoTalk', glyph: '/social/kakaotalk-glyph.svg', color: 'var(--color-social-kakaotalk)', placeholder: 'Link open.kakao.com' },
  instagram: { label: 'Instagram', logo: '/social/instagram-96.png', glyph: '/social/instagram-glyph.svg', color: 'var(--color-social-instagram)', placeholder: 'Tên Instagram hoặc link' },
  email: { label: 'Email', icon: 'mail', color: 'var(--color-social-email)', placeholder: 'Địa chỉ email' },
  facebook: { label: 'Facebook', logo: '/social/facebook-96.png', glyph: '/social/facebook-glyph.svg', color: 'var(--color-social-facebook)', placeholder: 'Link trang Facebook' },
  youtube: { label: 'Youtube', glyph: '/social/youtube-glyph.svg', color: 'var(--color-social-youtube)', placeholder: 'Link kênh hoặc @kenh' },
  tiktok: { label: 'Tiktok', glyph: '/social/tiktok-glyph.svg', color: 'var(--color-social-tiktok)', placeholder: 'Link hoặc @tentiktok' },
  twitter: { label: 'Twitter (X)', glyph: '/social/x-glyph.svg', color: 'var(--color-social-x)', placeholder: 'Link hoặc tên tài khoản X' },
  googlemaps: { label: 'Google Maps', glyph: '/social/googlemaps-glyph.svg', color: 'var(--color-social-googlemaps)', placeholder: 'Link Google Maps của cửa hàng' },
  pinterest: { label: 'Pinterest', glyph: '/social/pinterest-glyph.svg', color: 'var(--color-social-pinterest)', placeholder: 'Link hoặc tên Pinterest' }
};

const isWebUrl = (value: string) => {
  try { return ['https:', 'http:'].includes(new URL(value).protocol); } catch { return false; }
};
const USER = /^@?([A-Za-z0-9._-]{2,60})$/;
const user = (value: string) => USER.exec(value)?.[1];

/** Số điện thoại quốc tế (bỏ ký tự khác số; 0 đầu → 84). Không hợp lệ → undefined. */
function phoneDigits(value: string): string | undefined {
  if (!/^\+?[\d\s().-]{6,20}$/.test(value)) return undefined;
  let digits = value.replace(/\D/g, '');
  if (digits.startsWith('00')) digits = digits.slice(2);
  if (digits.startsWith('0')) digits = `84${digits.slice(1)}`;
  return digits.length >= 6 && digits.length <= 15 ? digits : undefined;
}

/**
 * Link mở được của 1 mục: link http(s) giữ nguyên (trừ hotline / email); giá trị ngắn dựng link theo từng ứng dụng.
 * Không hợp lệ → '' (ô báo lỗi, nút ẩn). Hàm thuần — có test.
 */
export function channelUrl(type: ChannelType, raw: string): string {
  const value = raw.trim();
  if (!value) return '';
  if (type === 'hotline') return /^\+?[\d\s().-]{6,20}$/.test(value) ? `tel:${value.replace(/[^\d+]/g, '')}` : '';
  if (type === 'email') return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value) ? `mailto:${value}` : '';
  if (isWebUrl(value)) return value;
  const phone = phoneDigits(value);
  const name = user(value);
  switch (type) {
    case 'zalo': return phone ? `https://zalo.me/${phone}` : '';
    case 'whatsapp': return phone ? `https://wa.me/${phone}` : '';
    case 'viber': return phone ? `viber://chat?number=%2B${phone}` : '';
    case 'messenger': return name ? `https://m.me/${name}` : '';
    case 'telegram': return name ? `https://t.me/${name}` : '';
    case 'wechat': return name ? `weixin://dl/chat?${name}` : '';
    case 'line': return name ? `https://line.me/R/ti/p/~${name}` : '';
    case 'snapchat': return name ? `https://www.snapchat.com/add/${name}` : '';
    case 'instagram': return name ? `https://www.instagram.com/${name}` : '';
    case 'facebook': return name ? `https://www.facebook.com/${name}` : '';
    case 'youtube': return name ? `https://www.youtube.com/@${name}` : '';
    case 'tiktok': return name ? `https://www.tiktok.com/@${name}` : '';
    case 'twitter': return name ? `https://x.com/${name}` : '';
    case 'pinterest': return name ? `https://www.pinterest.com/${name}` : '';
    default: return ''; // kakaotalk, googlemaps: chỉ nhận link
  }
}

export interface Channel<T extends ChannelType = ChannelType> { type: T; value: string }

/** Thông tin cũ (cột có sẵn) khi khách chưa từng chọn cổng liên lạc / mạng xã hội. */
export interface LegacyBrand { phone: string; zalo: string; whatsapp: string; facebook: string; instagram: string; x: string }

/**
 * Cấu hình lưu trước khi có danh sách chọn: dựng danh sách từ các ô cũ để không mất dữ liệu.
 * Hàm thuần — có test.
 */
export function channelsFromLegacy(b: LegacyBrand): { contacts: Channel<ContactType>[]; socials: Channel<SocialType>[] } {
  const contacts: Channel<ContactType>[] = [];
  if (b.phone.trim()) contacts.push({ type: 'hotline', value: b.phone.trim() });
  if (b.zalo.trim()) contacts.push({ type: 'zalo', value: b.zalo.trim() });
  if (b.whatsapp.trim()) contacts.push({ type: 'whatsapp', value: b.whatsapp.trim() });
  const socials: Channel<SocialType>[] = [];
  if (b.facebook.trim()) socials.push({ type: 'facebook', value: b.facebook.trim() });
  if (b.instagram.trim()) socials.push({ type: 'instagram', value: b.instagram.trim() });
  if (b.x.trim()) socials.push({ type: 'twitter', value: b.x.trim() });
  return { contacts, socials };
}

/** Màu thương hiệu dạng #rrggbb; rỗng = màu mặc định. */
export const isHexColor = (value: string) => /^#[0-9a-fA-F]{6}$/.test(value);
