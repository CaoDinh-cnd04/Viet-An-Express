import { z } from 'zod';
import { fill } from '@/shared/i18n';
import { isPhone, PHONE_MESSAGE } from '@/shared/lib/phone';
import { DEFAULT_SERVICE, MULTI_CATEGORY, RULES } from './constants';
import { DEFAULT_UNIT, qtyError, UNIT_MAX } from './lib/units';
import { hasMaxDecimals } from './lib/cell-nav';
import { euCountryCode } from '@/shared/config/eu';
import { isPerPackageInvoice } from '@/shared/config/domain';
import { packageCount, packageInvoiceIssues } from './lib/package-invoice';
import { EORI_MESSAGE, IOSS_MESSAGE, validateEoriNo, validateIossNo } from './lib/eu-tax';

/*
 * Schema form tạo đơn — nguồn duy nhất cho kiểu dữ liệu + quy tắc kiểm tra.
 * Giá trị ô nhập giữ dạng chuỗi (đúng như input HTML); đổi sang số ở lib/shipment.ts.
 */

const REQUIRED = 'Bắt buộc';
const required = (message = REQUIRED) => z.string().trim().min(1, message);
const optional = z.string().trim();
const maxLen = (max: number) => fill('Tối đa {n} ký tự', { n: max });

const isNumber = (v: string) => v.trim() !== '' && Number.isFinite(Number(v));
/** Số (dạng chuỗi) ≥ min; `integer` để bắt số nguyên. */
const optionalNumeric = z.string().refine(v => v.trim() === '' || (isNumber(v) && Number(v) >= 0), 'Số không hợp lệ');

const optionalEmail = z.union([z.literal(''), z.email('Email không hợp lệ')]);

/** Lỗi tổng SL các dòng kiện khác số kiện dự kiến khai ở đầu "Chi tiết kiện hàng". */
export const PIECES_MISMATCH = 'Tổng SL các dòng kiện là {total}, chưa bằng số kiện dự kiến ({pieces}). Hãy bấm "Thêm kiện" hoặc sửa SL cho khớp — hoặc sửa lại số kiện dự kiến.';

export const packageSchema = z.object({
  qty: z.string(),
  packaging: z.string(),
  length: optionalNumeric,
  width: optionalNumeric,
  height: optionalNumeric,
  /** Cân nặng mỗi kiện (kg). */
  weight: optionalNumeric,
  /** Nhóm hàng hóa của dòng kiện (dbo.NhomHangHoa). Nháp cũ có thể thiếu. */
  category: z.string().optional(),
  /** Mô tả mặt hàng chính — chỉ nhập khi nhóm là "Nhiều loại hàng". */
  description: z.string().optional()
});

export const invoiceItemSchema = z.object({
  descEn: z.string(),
  descVi: z.string(),
  manufacturer: z.string(),
  origin: z.string(),
  hs: z.string(),
  qty: z.string(),
  unit: z.string(),
  price: z.string(),
  /** Số thứ tự kiện ("1".."N") khi tuyến khai invoice riêng từng kiện; "" = invoice chung. Nháp cũ có thể thiếu. */
  package: z.string().optional()
});

export const createOrderSchema = z
  .object({
    shipper: z.object({
      company: required(),
      /** Tên shipper gốc — khách là đơn vị forwarder gửi hộ (dbo.MaVanDon.Ten_Khach_Cua_FWD). Nháp cũ có thể thiếu. */
      originalShipper: optional.max(RULES.originalShipperMax, maxLen(RULES.originalShipperMax)).optional(),
      contact: required(),
      tel: required().refine(isPhone, PHONE_MESSAGE),
      address: required().max(RULES.shipperAddressMax, maxLen(RULES.shipperAddressMax)),
      taxId: optional,
      email: optionalEmail,
      country: required(),
      branch: required('Chọn chi nhánh gửi')
    }),
    service: z.object({
      carrier: required('Chọn dịch vụ'),
      hub: required('Chọn hub'),
      reference: optional
    }),
    shipment: z.object({
      type: z.enum(['DOC', 'PACK']),
      /** DOC: khách khai. PACK: = tổng SL các dòng kiện (tự tính). */
      pieces: z.string(),
      /** DOC: khách khai. PACK: = tổng cân các dòng kiện (tự tính). */
      grossWeight: z.string()
    }),
    receiver: z.object({
      country: required(),
      countryCode: optional.optional(),
      iossNo: optional.optional(),
      eoriNo: optional.optional(),
      city: required(),
      company: required(),
      contact: required(),
      tel: required().refine(isPhone, PHONE_MESSAGE),
      /** Mã điện thoại theo nước đến, vd "+1" — tự điền, lưu vào ConsigneePhoneCode. Có thể thiếu ở nháp cũ. */
      phoneCode: z.string().optional(),
      taxId: optional,
      email: optionalEmail,
      postal: optional,
      state: optional,
      addr1: required().max(RULES.receiverAddressMax, maxLen(RULES.receiverAddressMax)),
      addr2: required().max(RULES.receiverAddressMax, maxLen(RULES.receiverAddressMax)),
      addr3: optional.max(RULES.receiverAddress3Max, maxLen(RULES.receiverAddress3Max))
    }),
    goods: z.object({
      category: optional,
      description: optional,
      docContent: optional,
      /** Khi category = MULTI_CATEGORY: các nhóm hàng trong kiện (in bảng phân loại kèm bill). */
      multi: z.array(z.object({ category: z.string(), note: z.string() }))
    }),
    packages: z.array(packageSchema),
    addons: z.array(z.string()),
    invoice: z.object({
      exportType: z.string(),
      /** DDU (người nhận chịu thuế) | DDP (người gửi chịu thuế). Nháp cũ có thể thiếu. */
      dutyTerms: z.string().optional(),
      currency: z.string(),
      shippingFee: optionalNumeric,
      items: z.array(invoiceItemSchema)
    })
  })
  .superRefine((v, ctx) => {
    const issue = (path: (string | number)[], message = REQUIRED) => ctx.addIssue({ code: 'custom', path, message });

    const countryCode = euCountryCode(v.receiver.country.trim() ? v.receiver.country : v.receiver.countryCode);
    if (!validateIossNo(countryCode, v.receiver.iossNo)) issue(['receiver', 'iossNo'], IOSS_MESSAGE);
    if (!validateEoriNo(countryCode, v.receiver.eoriNo)) issue(['receiver', 'eoriNo'], EORI_MESSAGE);

    if (v.shipment.type === 'DOC') {
      // Chứng từ: chỉ khai số kiện + cân nặng; nội dung mặc định "Documents"
      const pcs = Number(v.shipment.pieces);
      if (!isNumber(v.shipment.pieces) || pcs < 1 || !Number.isInteger(pcs)) issue(['shipment', 'pieces'], 'Số kiện tối thiểu là 1');
      if (!isNumber(v.shipment.grossWeight) || Number(v.shipment.grossWeight) < 0.01) issue(['shipment', 'grossWeight'], 'Nhập cân nặng (kg)');
      return;
    }

    // Hàng hóa (PACK): mô tả tổng quan (content) bắt buộc; số kiện / cân lấy từ bảng kiện
    if (!v.goods.description.trim()) issue(['goods', 'description'], 'Nhập mô tả tổng quan hàng hóa');
    else if (v.goods.description.trim().length > RULES.contentMax) issue(['goods', 'description'], maxLen(RULES.contentMax));

    // Hàng hóa (PACK): bắt buộc chi tiết kiện và invoice. Mỗi dòng kiện chọn nhóm hàng;
    // mô tả mặt hàng chỉ cần khi dòng đó chọn "Nhiều loại hàng" (nhóm khác thì tên nhóm là nội dung hàng).
    v.packages.forEach((p, i) => {
      if (!(p.category ?? '').trim()) issue(['packages', i, 'category'], 'Chọn nhóm hàng hóa');
      else if (p.category === MULTI_CATEGORY && !(p.description ?? '').trim()) issue(['packages', i, 'description'], 'Nhập mô tả mặt hàng');
    });
    if (!v.packages.length) issue(['packages'], 'Cần ít nhất 1 kiện');
    const declared = Number(v.shipment.pieces);
    if (!isNumber(v.shipment.pieces) || declared < 1 || !Number.isInteger(declared)) issue(['shipment', 'pieces'], 'Số kiện tối thiểu là 1');
    else {
      const totalQty = v.packages.reduce((s, p) => s + (isNumber(p.qty) ? Number(p.qty) : 0), 0);
      if (v.packages.length && totalQty !== declared) issue(['packages', 'root'], fill(PIECES_MISMATCH, { total: totalQty, pieces: declared }));
    }
    v.packages.forEach((p, i) => {
      if (!isNumber(p.qty) || Number(p.qty) < 1 || !Number.isInteger(Number(p.qty))) issue(['packages', i, 'qty'], 'SL ≥ 1');
      if (!p.packaging) issue(['packages', i, 'packaging'], 'Chọn bao bì');
      if (!isNumber(p.weight) || Number(p.weight) <= 0) issue(['packages', i, 'weight'], 'Nhập cân/kiện');
    });
    if (!v.invoice.exportType) issue(['invoice', 'exportType'], 'Chọn hình thức xuất khẩu');
    if (!v.invoice.dutyTerms) issue(['invoice', 'dutyTerms'], 'Chọn hình thức chịu thuế');
    if (!v.invoice.currency) issue(['invoice', 'currency'], 'Chọn đơn vị tiền tệ');
    if (!v.invoice.items.length) issue(['invoice', 'items'], 'Cần ít nhất 1 mặt hàng');
    // Tuyến khai invoice riêng từng kiện: kiện nào cũng phải có mặt hàng, không còn dòng hàng của kiện đã bớt.
    if (isPerPackageInvoice(v.service.hub)) {
      const [first] = packageInvoiceIssues(v.invoice.items, packageCount(v.packages));
      if (first) issue(['invoice', 'items', 'root'], first);
    }
    v.invoice.items.forEach((it, i) => {
      if (!it.descEn.trim()) issue(['invoice', 'items', i, 'descEn'], 'Nhập tên hàng (EN)');
      if (!it.descVi.trim()) issue(['invoice', 'items', i, 'descVi'], 'Nhập tên hàng (VN)');
      const qtyIssue = qtyError(it.qty, it.unit);
      if (qtyIssue) issue(['invoice', 'items', i, 'qty'], qtyIssue);
      if (!it.unit.trim()) issue(['invoice', 'items', i, 'unit'], 'Nhập đơn vị tính');
      else if (it.unit.trim().length > UNIT_MAX) issue(['invoice', 'items', i, 'unit'], maxLen(UNIT_MAX));
      if (!isNumber(it.price) || Number(it.price) < 0) issue(['invoice', 'items', i, 'price'], 'Nhập đơn giá');
      else if (!hasMaxDecimals(it.price, RULES.priceDecimals)) issue(['invoice', 'items', i, 'price'], 'Đơn giá tối đa 2 số lẻ');
    });
  }, {
    // Chạy cả khi ô khác còn lỗi — để lỗi mô tả hàng / kiện / invoice hiện ngay khi rời ô, không đợi sửa hết ô trên.
    when: payload => typeof payload.value === 'object' && payload.value !== null
  });

export type CreateOrderValues = z.infer<typeof createOrderSchema>;
export type PackageValues = z.infer<typeof packageSchema>;
export type InvoiceItemValues = z.infer<typeof invoiceItemSchema>;

/** Nhóm trường theo bước wizard — dùng để kiểm tra từng bước trước khi sang bước sau. */
export const STEP_FIELDS = [
  ['shipper', 'service', 'shipment', 'receiver'],
  ['goods', 'packages', 'addons'],
  ['invoice']
] as const satisfies ReadonlyArray<ReadonlyArray<keyof CreateOrderValues>>;

export const emptyPackage = (): PackageValues => ({ qty: '1', packaging: 'Thùng carton', length: '', width: '', height: '', weight: '', category: '', description: '' });
export const emptyInvoiceItem = (): InvoiceItemValues => ({ descEn: '', descVi: '', manufacturer: '', origin: 'VN', hs: '', qty: '', unit: DEFAULT_UNIT, price: '', package: '' });

export const defaultValues = (): CreateOrderValues => ({
  shipper: { company: '', originalShipper: '', contact: '', tel: '', address: '', taxId: '', email: '', country: 'Vietnam', branch: 'TP.HCM' },
  service: { carrier: DEFAULT_SERVICE.carrier, hub: DEFAULT_SERVICE.hub, reference: '' },
  shipment: { type: 'PACK', pieces: '1', grossWeight: '' },
  receiver: { country: '', countryCode: '', iossNo: '', eoriNo: '', city: '', company: '', contact: '', tel: '', phoneCode: '', taxId: '', email: '', postal: '', state: '', addr1: '', addr2: '', addr3: '' },
  goods: { category: '', description: '', docContent: '', multi: [] },
  packages: [emptyPackage()],
  addons: [],
  invoice: { exportType: 'GIFT', dutyTerms: 'DDU', currency: 'USD', shippingFee: '', items: [emptyInvoiceItem()] }
});
