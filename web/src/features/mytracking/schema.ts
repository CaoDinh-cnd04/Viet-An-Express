import { z } from 'zod';
import { legacyAssetUrl } from './lib/link';
import {
  CONTACT_TYPES, channelsFromLegacy, channelUrl, isHexColor, MAX_CHANNELS, MAX_CHANNEL_VALUE, SOCIAL_TYPES, type Channel, type ChannelType
} from './lib/channels';

export const MAX_AD_IMAGES = 5;
export const MAX_IMAGE_BYTES = 200 * 1024;
export const COMPRESSED_IMAGE_BYTES = 200 * 1024;
export const IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp'] as const;
export function isWebUrl(value: string): boolean {
  try { return ['https:', 'http:'].includes(new URL(value).protocol); } catch { return false; }
}
export const isImageSource = (value: string): boolean =>
  isWebUrl(value) || /^data:image\/(png|jpeg|webp);base64,[A-Za-z0-9+/=]+$/.test(value);
const imageSource = z.string().refine(isImageSource, 'Địa chỉ hình ảnh không hợp lệ');
const optionalImage = z.string().refine(v => !v || isImageSource(v), 'Địa chỉ hình ảnh không hợp lệ');
const optionalLink = z.string().trim().max(MAX_CHANNEL_VALUE, 'Tối đa 250 ký tự').refine(v => !v || isWebUrl(v), 'Link phải bắt đầu bằng http:// hoặc https://');
const optionalColor = z.string().trim().refine(v => !v || isHexColor(v), 'Màu dạng #rrggbb');

/** Danh sách cổng liên lạc / mạng xã hội: tối đa 5 mục, không trùng loại, giá trị dựng được link. */
function channelList<T extends ChannelType>(types: readonly [T, ...T[]], maxMessage: string) {
  return z.array(z.object({ type: z.enum(types), value: z.string().trim().max(MAX_CHANNEL_VALUE, 'Tối đa 250 ký tự') }))
    .max(MAX_CHANNELS, maxMessage)
    .superRefine((list, ctx) => {
      const seen = new Set<string>();
      list.forEach((item, index) => {
        if (seen.has(item.type)) ctx.addIssue({ code: 'custom', path: [index, 'type'], message: 'Mục này đã chọn' });
        seen.add(item.type);
        if (!channelUrl(item.type, item.value)) ctx.addIssue({ code: 'custom', path: [index, 'value'], message: 'Giá trị chưa hợp lệ' });
      });
    });
}
export const myTrackingSchema = z.object({
  title: z.string().trim().min(1, 'Nhập tiêu đề MyTracking').max(120, 'Tiêu đề tối đa 120 ký tự'),
  description: z.string().trim().max(MAX_CHANNEL_VALUE, 'Mô tả tối đa 250 ký tự'),
  images: z.array(z.object({
    id: z.string().max(MAX_CHANNEL_VALUE), src: imageSource, linkUrl: optionalLink,
    title: z.string().trim().max(120, 'Tiêu đề ảnh tối đa 120 ký tự').optional(),
    buttonText: z.string().trim().max(40, 'Chữ trên nút tối đa 40 ký tự').optional()
  })).max(MAX_AD_IMAGES, 'Tối đa 5 ảnh quảng cáo'),
  background: optionalImage,
  brand: z.object({
    logo: optionalImage, companyName: z.string().trim().max(MAX_CHANNEL_VALUE), address: z.string().trim().max(MAX_CHANNEL_VALUE), phone: z.string().trim().max(50),
    /** Khóa cũ (portal lưu trước 23/10/2026) — chỉ để dựng contacts / socials khi đọc; backend ghi các ô CongTinNhan / CongTruyenThong theo danh sách. */
    facebook: z.string().trim(), instagram: z.string().trim(), x: z.string().trim(), zalo: z.string().trim(), whatsapp: z.string().trim(),
    website: optionalLink.refine(v => v.length <= MAX_CHANNEL_VALUE, 'Tối đa 250 ký tự'),
    primaryColor: optionalColor,
    secondaryColor: optionalColor,
    contacts: channelList(CONTACT_TYPES, 'Chọn tối đa 5 cổng liên lạc'),
    socials: channelList(SOCIAL_TYPES, 'Chọn tối đa 5 mạng xã hội')
  })
});
export type MyTrackingConfig = z.infer<typeof myTrackingSchema>;
export type AdImage = MyTrackingConfig['images'][number];
export const emptyConfig = (): MyTrackingConfig => ({
  title: 'DỊCH VỤ TIÊU BIỂU', description: '', images: [], background: '',
  brand: {
    logo: '', companyName: '', address: '', phone: '', facebook: '', instagram: '', x: '', zalo: '', whatsapp: '',
    website: '', primaryColor: '', secondaryColor: '', contacts: [], socials: []
  }
});
/** Chuyển cấu hình thử nghiệm cũ sang cấu trúc mới, chỉ dùng dữ liệu đã kiểm tra. */
export function parseConfig(value: unknown): MyTrackingConfig {
  if (!value || typeof value !== 'object') return emptyConfig();
  const raw = value as Record<string, unknown>;
  // Ảnh hệ thống cũ lưu đường dẫn tương đối ("/Upload/…") → link đầy đủ để hiển thị và qua kiểm tra.
  const asset = (v: unknown) => (typeof v === 'string' ? legacyAssetUrl(v) : v);
  const images = Array.isArray(raw.images) ? raw.images.map((image: unknown) => {
    if (!image || typeof image !== 'object') return image;
    const item = image as Record<string, unknown>;
    return { ...item, src: asset(item.src), linkUrl: item.linkUrl ?? item.link ?? '' };
  }) : [];
  const defaults = emptyConfig();
  const rawBrand = raw.brand && typeof raw.brand === 'object' ? raw.brand as Record<string, unknown> : {};
  const brand = { ...defaults.brand, ...rawBrand };
  brand.logo = asset(brand.logo) as string;
  // Cấu hình lưu trước khi có danh sách chọn: dựng cổng liên lạc / mạng xã hội từ các ô cũ.
  if (!Array.isArray(rawBrand.contacts) && !Array.isArray(rawBrand.socials)) Object.assign(brand, channelsFromLegacy(brand));
  // Mục sai (giá trị cũ không dựng được link, trùng loại, quá 5) bỏ riêng mục đó, không bỏ cả cấu hình.
  brand.contacts = validChannels(brand.contacts, CONTACT_TYPES);
  brand.socials = validChannels(brand.socials, SOCIAL_TYPES);
  const parsed = myTrackingSchema.safeParse({ ...defaults, ...raw, background: asset(raw.background ?? defaults.background), images, brand });
  return parsed.success ? parsed.data : defaults;
}

function validChannels<T extends ChannelType>(value: unknown, types: readonly T[]): Channel<T>[] {
  if (!Array.isArray(value)) return [];
  const seen = new Set<string>();
  return value.flatMap((item: unknown) => {
    const c = item as Partial<Channel>;
    if (!c || typeof c.value !== 'string' || !types.includes(c.type as T) || seen.has(c.type as string) || !channelUrl(c.type as T, c.value)) return [];
    seen.add(c.type as string);
    return [{ type: c.type as T, value: c.value.trim() }];
  }).slice(0, MAX_CHANNELS);
}
