import type { SavedProduct } from '../api';
import { emptyInvoiceItem, type InvoiceItemValues } from '@/features/order-form';
import { stripDiacritics } from '@/shared/lib/text';

/** Chép thông tin mặt hàng, khách khai lại số lượng và đơn giá cho lần gửi mới. */
export const invoiceItemFromProduct = (p: SavedProduct): InvoiceItemValues => ({
  ...emptyInvoiceItem(),
  descEn: p.descEn,
  descVi: p.descVi,
  manufacturer: p.manufacturer,
  origin: p.origin,
  hs: p.hs,
  unit: p.unit,
  price: '0'
});

/** Bỏ dấu tiếng Việt + chữ thường để tìm không phân biệt "ao" / "Áo". */
export const fold = (s: string) => stripDiacritics(s).toLowerCase();

/**
 * Lọc thư viện mặt hàng theo ô tìm (tên EN / VN, mã HS, nhà sản xuất) và tab "Yêu thích".
 * Giữ thứ tự backend trả (yêu thích trước, rồi dùng gần đây).
 */
export function filterProducts(products: readonly SavedProduct[], query: string, favoritesOnly: boolean): SavedProduct[] {
  const words = fold(query).split(/\s+/).filter(Boolean);
  return products.filter(p => {
    if (favoritesOnly && !p.isFavorite) return false;
    if (!words.length) return true;
    const text = fold([p.descEn, p.descVi, p.hs, p.manufacturer].join(' '));
    return words.every(w => text.includes(w));
  });
}
