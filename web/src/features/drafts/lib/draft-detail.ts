import { EXPORT_TYPES, invoiceTotal, summarizePackages, type CreateOrderValues } from '@/features/create-order';

export type DetailRows = ReadonlyArray<[label: string, value: string]>;

export interface DetailParty {
  name: string;
  lines: string[];
  fields: DetailRows;
}

export interface DraftDetail {
  isDoc: boolean;
  shipper: DetailParty;
  receiver: DetailParty;
  /** Dịch vụ & lô hàng. */
  shipment: DetailRows;
  packages: ReadonlyArray<{ qty: string; packaging: string; category: string; size: string; weight: string }>;
  packageTotals: ReturnType<typeof summarizePackages> | null;
  items: ReadonlyArray<{ descEn: string; descVi: string; manufacturer: string; hs: string; origin: string; qty: string; price: string; amount: number }>;
  invoice: DetailRows;
  invoiceTotal: number;
  shippingFee: number;
  currency: string;
}

const text = (v: unknown) => (typeof v === 'string' ? v.trim() : typeof v === 'number' ? String(v) : '');
const join = (...parts: unknown[]) => parts.map(text).filter(Boolean).join(', ');
const num = (v: unknown) => {
  const n = Number(text(v));
  return Number.isFinite(n) ? n : 0;
};

/** Nhóm hàng của dòng kiện: "Nhiều loại hàng" thì kèm mô tả. */
const packageCategory = (p: { category?: string; description?: string }) => {
  const c = text(p.category);
  const d = text(p.description);
  return c && d ? `${c}: ${d}` : c || d;
};

/**
 * Dựng nội dung "Xem chi tiết" (bố cục phiếu vận đơn) từ form đã lưu cùng đơn nháp (payload của GET /drafts).
 * Nháp cũ có thể thiếu trường → để trống (giao diện hiện "—"), không lỗi.
 */
export function draftDetail(payload: unknown): DraftDetail {
  const v = (payload ?? {}) as Partial<CreateOrderValues>;
  const s = v.shipper ?? ({} as Partial<CreateOrderValues['shipper']>);
  const r = v.receiver ?? ({} as Partial<CreateOrderValues['receiver']>);
  const sv = v.service ?? ({} as Partial<CreateOrderValues['service']>);
  const sh = v.shipment ?? ({} as Partial<CreateOrderValues['shipment']>);
  const g = v.goods ?? ({} as Partial<CreateOrderValues['goods']>);
  const inv = v.invoice ?? ({} as Partial<CreateOrderValues['invoice']>);
  const isDoc = sh.type === 'DOC';
  const currency = text(inv.currency) || 'USD';

  const packages = isDoc ? [] : (v.packages ?? []).filter(p => text(p.qty) || text(p.weight) || text(p.length));
  const items = isDoc ? [] : (inv.items ?? []).filter(it => text(it.descEn));
  const exportType = EXPORT_TYPES.find(t => t.value === inv.exportType)?.label ?? text(inv.exportType);
  const sum = packages.length ? summarizePackages(packages, sv) : null;
  const content = isDoc ? text(g.docContent) || 'Documents' : text(g.description) || [...new Set(packages.map(packageCategory).filter(Boolean))].join(', ');

  return {
    isDoc,
    shipper: {
      name: text(s.company),
      lines: [text(s.address)],
      fields: [
        ...(text(s.originalShipper) ? [['Shipper gốc', text(s.originalShipper)] as [string, string]] : []),
        ['Người liên hệ', text(s.contact)],
        ['Điện thoại', text(s.tel)],
        ['MST / CCCD', text(s.taxId)],
        ['Email', text(s.email)],
        ['Chi nhánh gửi', text(s.branch)]
      ]
    },
    receiver: {
      name: text(r.company),
      lines: [text(r.addr1), text(r.addr2), text(r.addr3), join(r.city, r.state, r.postal), text(r.country).toUpperCase()],
      fields: [
        ['Người liên hệ', text(r.contact)],
        ['Điện thoại', text(r.tel) ? `${text(r.phoneCode)} ${text(r.tel)}`.trim() : ''],
        ['Tax ID', text(r.taxId)],
        ...(text(r.iossNo) ? [['IOSS No', text(r.iossNo)] as [string, string]] : []),
        ...(text(r.eoriNo) ? [['EORI No', text(r.eoriNo)] as [string, string]] : []),
        ['Email', text(r.email)]
      ]
    },
    shipment: [
      ['Dịch vụ', text(sv.hub || sv.carrier)],
      ['Số tham chiếu', text(sv.reference)],
      ['Loại hàng', isDoc ? 'Chứng từ (DOC)' : 'Hàng hóa (PACK)'],
      ['Số kiện', isDoc ? text(sh.pieces) : String(sum?.pieces ?? text(sh.pieces))],
      ['Cân nặng', isDoc ? (text(sh.grossWeight) ? `${text(sh.grossWeight)} kg` : '') : sum ? `${sum.chargeableWeight} kg` : ''],
      ['Nội dung hàng', content],
      ['Tùy chọn dịch vụ', (v.addons ?? []).join(', ') || 'Không sử dụng dịch vụ'],
      ...(!isDoc ? [['Chịu thuế', text(inv.dutyTerms) === 'DDP' ? 'DDP — người gửi chịu thuế' : 'DDU — người nhận chịu thuế'] as [string, string]] : [])
    ],
    packages: packages.map(p => ({
      qty: text(p.qty),
      packaging: text(p.packaging),
      category: packageCategory(p),
      size: text(p.length) ? [text(p.length), text(p.width), text(p.height)].join(' × ') : '',
      weight: text(p.weight) ? `${text(p.weight)} kg` : ''
    })),
    packageTotals: sum,
    items: items.map(it => ({
      descEn: text(it.descEn),
      descVi: text(it.descVi),
      manufacturer: text(it.manufacturer),
      hs: text(it.hs),
      origin: text(it.origin),
      qty: `${text(it.qty)} ${text(it.unit)}`.trim(),
      price: text(it.price),
      amount: Math.round(num(it.qty) * num(it.price) * 100) / 100
    })),
    invoice: isDoc ? [] : [['Hình thức xuất khẩu', exportType], ['Tiền tệ', currency]],
    invoiceTotal: invoiceTotal(items, inv.shippingFee),
    shippingFee: num(inv.shippingFee),
    currency
  };
}
