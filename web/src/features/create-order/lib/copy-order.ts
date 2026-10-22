import { defaultValues, emptyInvoiceItem, emptyPackage, type CreateOrderValues } from '../schema';
import { MULTI_CATEGORY } from '../constants';
import { sanitizePhone } from '@/shared/lib/phone';

const packagingNames: Record<string, string> = { CARTON: 'Thùng carton', CTN: 'Thùng carton', BOX: 'Thùng carton', BAG: 'Bao / túi', PALLET: 'Pallet', 'WOODEN CASE': 'Kiện gỗ' };

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/**
 * Làm sạch liên hệ đọc từ đơn cũ / đơn nháp (dbo.MaVanDon có dữ liệu cũ như email "=", SĐT lẫn chữ):
 * email sai → bỏ trống (ô không bắt buộc), SĐT → bỏ ký tự lạ. Tránh form báo lỗi ở ô khách không để ý.
 */
export function cleanLoadedContacts(v: CreateOrderValues): CreateOrderValues {
  const email = (s: string | undefined) => (EMAIL.test((s ?? '').trim()) ? (s ?? '').trim() : '');
  return {
    ...v,
    shipper: { ...v.shipper, email: email(v.shipper.email), tel: sanitizePhone(v.shipper.tel ?? '').trim() },
    receiver: { ...v.receiver, email: email(v.receiver.email), tel: sanitizePhone(v.receiver.tel ?? '').trim() }
  };
}

/** Copy all form fields into independent objects; source IDs/status never become a draft ID. */
export function copyOrderValues(payload: Partial<CreateOrderValues>): CreateOrderValues {
  const p = structuredClone(payload);
  const base = defaultValues();
  return {
    shipper: { ...base.shipper, ...p.shipper },
    receiver: { ...base.receiver, ...p.receiver },
    service: { ...base.service, ...p.service },
    shipment: { ...base.shipment, ...p.shipment },
    goods: { ...base.goods, ...p.goods, multi: p.goods?.multi ?? base.goods.multi },
    packages: Array.isArray(p.packages) ? p.packages.map(row => {
      const code = (row as typeof row & { type?: string }).type ?? '';
      return { ...emptyPackage(), ...row, packaging: row.packaging || packagingNames[code.toUpperCase()] || code || 'Thùng carton',
        category: row.category || MULTI_CATEGORY, description: row.description || (row.category ? '' : p.goods?.description ?? '') };
    }) : base.packages,
    addons: Array.isArray(p.addons) ? p.addons : base.addons,
    invoice: { ...base.invoice, ...p.invoice, items: Array.isArray(p.invoice?.items) ? p.invoice.items.map(row => ({ ...emptyInvoiceItem(), ...row })) : base.invoice.items }
  };
}
