import { COMPRESSED_IMAGE_BYTES, IMAGE_TYPES, MAX_IMAGE_BYTES, isWebUrl } from '../schema';

/**
 * Link khách hay dán không phải link file ảnh → đổi sang link ảnh trực tiếp khi đổi được:
 * - Google Hình ảnh (google.com/imgres?imgurl=...) và link chuyển hướng Google (google.com/url?url=...).
 * - Google Drive chia sẻ (drive.google.com/file/d/{id}/view, open?id=, uc?id=) → ảnh thu nhỏ của Drive (file phải để "Bất kỳ ai có đường liên kết").
 * Còn lại giữ nguyên. Hàm thuần — có test.
 */
export function normalizeImageUrl(raw: string): string {
  const src = raw.trim();
  let url: URL;
  try { url = new URL(src); } catch { return src; }
  const host = url.hostname.replace(/^www\./, '');
  if (/^google\.[a-z.]+$/.test(host)) {
    const target = url.pathname === '/imgres' ? url.searchParams.get('imgurl') : url.pathname === '/url' ? url.searchParams.get('url') ?? url.searchParams.get('q') : null;
    if (target && isWebUrl(target)) return target;
  }
  if (host === 'drive.google.com') {
    const id = /\/file\/d\/([\w-]+)/.exec(url.pathname)?.[1] ?? url.searchParams.get('id');
    if (id) return `https://drive.google.com/thumbnail?id=${id}&sz=w1600`;
  }
  return src;
}

/** Check remote images without requiring the source server to support CORS. */
export function validateImageUrl(raw: string): Promise<string> {
  const src = normalizeImageUrl(raw);
  if (!isWebUrl(src)) return Promise.reject(new Error('Link phải bắt đầu bằng http:// hoặc https://'));
  return new Promise((resolve, reject) => {
    const image = new Image();
    const finish = (loaded: boolean) => {
      clearTimeout(timer);
      image.onload = null;
      image.onerror = null;
      if (loaded && image.naturalWidth > 0) resolve(src);
      else {
        image.src = '';
        reject(new Error('Link này không phải link file ảnh hoặc trang nguồn chặn lấy ảnh. Mở ảnh, bấm chuột phải → "Sao chép địa chỉ hình ảnh" rồi dán lại, hoặc lưu ảnh về máy và bấm "Thêm ảnh mới".'));
      }
    };
    const timer = setTimeout(() => finish(false), 15000);
    image.referrerPolicy = 'no-referrer';
    image.onload = () => finish(true);
    image.onerror = () => finish(false);
    image.src = src;
  });
}

export function validateImageFile(file: Pick<File, 'type' | 'size'>): void {
  if (!IMAGE_TYPES.some(type => type === file.type)) throw new Error('Chỉ nhận ảnh JPG, JPEG, PNG hoặc WebP');
  if (file.size > MAX_IMAGE_BYTES) throw new Error('Ảnh tối đa 200 KB trước khi nén');
}
function asDataUrl(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => typeof reader.result === 'string' ? resolve(reader.result) : reject(new Error('Không đọc được hình ảnh'));
    reader.onerror = () => reject(new Error('Không đọc được hình ảnh'));
    reader.readAsDataURL(blob);
  });
}
/** Nén thành WebP, giảm chất lượng và kích thước để mỗi ảnh không quá 200 KB. */
export async function readImage(file: File): Promise<string> {
  validateImageFile(file);
  const bitmap = await createImageBitmap(file).catch(() => { throw new Error('Không đọc được hình ảnh'); });
  try {
    const canvas = document.createElement('canvas');
    const context = canvas.getContext('2d');
    if (!context) throw new Error('Trình duyệt không hỗ trợ xử lý hình ảnh');
    let scale = Math.min(1, 1600 / Math.max(bitmap.width, bitmap.height));
    for (let attempt = 0; attempt < 8; attempt++) {
      canvas.width = Math.max(1, Math.round(bitmap.width * scale));
      canvas.height = Math.max(1, Math.round(bitmap.height * scale));
      context.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
      for (const quality of [0.85, 0.65, 0.45]) {
        const blob = await new Promise<Blob | null>(resolve => canvas.toBlob(resolve, 'image/webp', quality));
        if (blob && blob.size <= COMPRESSED_IMAGE_BYTES) return asDataUrl(blob);
      }
      scale *= 0.75;
    }
    throw new Error('Không thể nén ảnh xuống 200 KB. Vui lòng chọn ảnh khác.');
  } finally { bitmap.close(); }
}
