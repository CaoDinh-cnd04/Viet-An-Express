import { fill } from '@/shared/i18n';
import { parseCsv } from '@/shared/lib/files';
import { normalizeUnit, qtyError, hasMaxDecimals, RULES } from '@/features/order-form';
import type { InvoiceItemValues } from '@/features/order-form';

/** Cột file mẫu import dòng invoice. Đổi cột: sửa ở đây. */
export const INVOICE_COLUMNS: ReadonlyArray<{ header: string; field: keyof InvoiceItemValues; example: string }> = [
  { header: 'DescriptionEN', field: 'descEn', example: "Women's flower dress" },
  { header: 'DescriptionVN', field: 'descVi', example: 'Váy hoa nữ' },
  { header: 'Manufacturer', field: 'manufacturer', example: 'Cty May ABC, TP.HCM' },
  { header: 'Origin', field: 'origin', example: 'VN' },
  { header: 'HSCode', field: 'hs', example: '6204.43' },
  { header: 'Qty', field: 'qty', example: '5' },
  { header: 'Unit', field: 'unit', example: 'PCS' },
  { header: 'UnitPrice', field: 'price', example: '8' }
];

export const invoiceTemplate = (): string[][] => [INVOICE_COLUMNS.map(c => c.header), INVOICE_COLUMNS.map(c => c.example)];

export interface InvoiceImportResult {
  items: InvoiceItemValues[];
  /** Dòng bị bỏ qua kèm lý do. */
  skipped: string[];
  /** Dán Excel: số dòng đã thêm nhưng còn trống Qty / UnitPrice — khách nhập tiếp trong bảng invoice. */
  incomplete?: number;
}

type RawItem = Record<keyof InvoiceItemValues, string>;

/**
 * Kiểm tra 1 dòng đã tách cột (dùng chung cho CSV và dữ liệu dán từ Excel): thiếu tên EN, SL / đơn giá sai → bỏ qua kèm lý do.
 * `line` = số dòng khách thấy trong file / vùng đã dán. `allowBlank` (dán Excel): ô Qty / UnitPrice để trống vẫn thêm dòng
 * (khách nhập tiếp trong bảng, form báo thiếu khi tạo đơn) — chỉ bỏ dòng khi ô có giá trị mà sai. Trả true = dòng còn trống ô.
 */
function checkItem(item: RawItem, line: number, items: InvoiceItemValues[], skipped: string[], allowBlank = false): boolean {
  const blankQty = allowBlank && item.qty === '';
  const blankPrice = allowBlank && item.price === '';
  if (!item.descEn) return !skipped.push(fill('Dòng {line}: thiếu DescriptionEN', { line }));
  if (!blankQty && !(Number(item.qty) > 0)) return !skipped.push(fill('Dòng {line}: Qty không hợp lệ', { line }));
  if (!blankPrice && (item.price === '' || !(Number(item.price) >= 0))) return !skipped.push(fill('Dòng {line}: UnitPrice không hợp lệ', { line }));
  if (!blankPrice && !hasMaxDecimals(item.price, RULES.priceDecimals)) return !skipped.push(fill('Dòng {line}: UnitPrice tối đa 2 số lẻ', { line }));
  const unit = normalizeUnit(item.unit);
  if (!blankQty && qtyError(item.qty, unit)) return !skipped.push(fill('Dòng {line}: Qty phải là số nguyên với ĐVT {unit}', { line, unit }));
  items.push({ ...(item as unknown as InvoiceItemValues), origin: item.origin || 'VN', unit });
  return blankQty || blankPrice;
}

/** CSV → dòng invoice. Dòng thiếu tên EN hoặc SL/đơn giá không phải số bị bỏ qua. */
export function parseInvoiceCsv(text: string): InvoiceImportResult {
  const { rows } = parseCsv(text);
  const items: InvoiceItemValues[] = [];
  const skipped: string[] = [];

  rows.forEach((raw, i) => {
    const lower = Object.fromEntries(Object.entries(raw).map(([k, v]) => [k.toLowerCase(), v.trim()]));
    const get = (header: string) => lower[header.toLowerCase()] ?? '';
    const item = Object.fromEntries(INVOICE_COLUMNS.map(c => [c.field, get(c.header)])) as RawItem;
    checkItem(item, i + 2, items, skipped);
  });

  return { items, skipped };
}

/**
 * Tách vùng ô copy từ Excel / Google Sheets: cột ngăn bằng Tab, dòng ngăn bằng xuống dòng; ô có xuống dòng bên trong
 * (Alt+Enter) Excel bọc trong dấu nháy kép — giữ nguyên xuống dòng trong ô. Bỏ dòng trống. Hàm thuần — có test.
 */
export function splitPastedTable(text: string): string[][] {
  const rows: string[][] = [];
  let row: string[] = [];
  let cell = '';
  let quoted = false;
  const src = text.charCodeAt(0) === 0xfeff ? text.slice(1) : text; // bỏ BOM
  for (let i = 0; i < src.length; i++) {
    const ch = src[i];
    if (quoted) {
      if (ch === '"' && src[i + 1] === '"') {
        cell += '"';
        i++;
      } else if (ch === '"') quoted = false;
      else cell += ch;
    } else if (ch === '"' && cell === '') quoted = true;
    else if (ch === '\t') {
      row.push(cell);
      cell = '';
    } else if (ch === '\n' || ch === '\r') {
      if (ch === '\r' && src[i + 1] === '\n') i++;
      row.push(cell);
      rows.push(row);
      row = [];
      cell = '';
    } else cell += ch;
  }
  if (cell || row.length) {
    row.push(cell);
    rows.push(row);
  }
  return rows.map(r => r.map(c => c.replace(/\r\n?/g, '\n').trim())).filter(r => r.some(c => c !== ''));
}

/**
 * Số dán từ Excel theo kiểu Việt / Mỹ → dạng máy đọc: "1,234.50" → "1234.50", "8,5" → "8.5", "1,500" → "1500"
 * (phẩy tách đúng nhóm 3 số = ngăn nghìn); dấu chấm luôn là phần lẻ. Hàm thuần — có test.
 */
export function normalizePastedNumber(value: string): string {
  const v = value.replace(/\s/g, '');
  if (v.includes(',') && v.includes('.')) return v.replace(/,/g, '');
  if (/^\d{1,3}(,\d{3})+$/.test(v)) return v.replace(/,/g, '');
  return v.replace(',', '.');
}

/** Tên cột nhận ra ở dòng tiêu đề khi dán (không phân biệt hoa thường, bỏ khoảng trắng / dấu chấm): cột mẫu + tên tiếng Việt quen dùng. */
const HEADER_ALIASES: Record<string, keyof InvoiceItemValues> = {
  ...Object.fromEntries(INVOICE_COLUMNS.map(c => [c.header.toLowerCase(), c.field])),
  descriptionofgoods: 'descEn', description: 'descEn', motatienganh: 'descEn', tentienganh: 'descEn', tenen: 'descEn',
  motatiengviet: 'descVi', tentiengviet: 'descVi', tenvn: 'descVi', tenvi: 'descVi',
  nhasanxuat: 'manufacturer', xuatxu: 'origin', countryoforigin: 'origin', mahs: 'hs', hs: 'hs', hscode: 'hs',
  soluong: 'qty', sl: 'qty', quantity: 'qty', dvt: 'unit', donvitinh: 'unit', dongia: 'price', price: 'price', unitprice: 'price'
};

const headerKey = (cell: string) =>
  cell.normalize('NFD').replace(/\p{Diacritic}/gu, '').replace(/đ/gi, 'd').toLowerCase().replace(/[^a-z0-9]/g, '');

/**
 * Tên cột → trường invoice: khớp đúng tên quen dùng, không thì theo từ khóa — để dán thẳng bảng invoice của hệ thống cũ
 * ("TÊN HÀNG TIẾNG ANH", "MATERIAL COMPOSITION + MANUFACTURER & ADDRESS (ENGLISH)", "HS CODE (HTSUS 10 DIGITS)", "QUANTITY"…).
 * Thứ tự kiểm tra quan trọng: nhà sản xuất trước tên tiếng Anh (cột NSX có chữ ENGLISH), đơn giá trước ĐVT. Cột STT / lạ → undefined.
 */
export function invoiceHeaderField(cell: string): keyof InvoiceItemValues | undefined {
  const k = headerKey(cell);
  if (!k) return undefined;
  if (HEADER_ALIASES[k]) return HEADER_ALIASES[k];
  if (/manufacturer|nhasanxuat|material/.test(k)) return 'manufacturer';
  if (/origin|xuatxu/.test(k)) return 'origin';
  if (/^hs|mahs/.test(k)) return 'hs';
  if (/unitprice|dongia|price/.test(k)) return 'price';
  if (/quantity|soluong/.test(k) || k === 'qty') return 'qty';
  if (/^unit|donvi|dvt/.test(k)) return 'unit';
  if (/tienganh|english|descriptionen/.test(k)) return 'descEn';
  if (/tiengviet|vietnamese|descriptionvn|descriptionvi/.test(k)) return 'descVi';
  return undefined;
}

/**
 * Dữ liệu copy từ Excel (bôi chọn các ô rồi Ctrl+C) → dòng invoice. Cột theo đúng thứ tự file mẫu
 * (DescriptionEN · DescriptionVN · Manufacturer · Origin · HSCode · Qty · Unit · UnitPrice); copy kèm dòng tiêu đề
 * thì đọc theo tên cột (`invoiceHeaderField`: tên mẫu, tên tiếng Việt, tiêu đề bảng invoice hệ thống cũ), cột lạ / STT bỏ qua;
 * không tiêu đề mà cột đầu là STT thì bỏ cột đó.
 * Kiểm tra như nhập file, riêng ô Qty / UnitPrice để trống thì vẫn thêm dòng (đếm ở `incomplete`). Hàm thuần — có test.
 */
export function parseInvoicePaste(text: string): InvoiceImportResult {
  const rows = splitPastedTable(text);
  const items: InvoiceItemValues[] = [];
  const skipped: string[] = [];
  let incomplete = 0;
  const first = rows[0];
  if (!first) return { items, skipped, incomplete };

  const headerFields = first.map(invoiceHeaderField);
  const hasHeader = headerFields.filter(Boolean).length >= 2;
  const body = rows.slice(hasHeader ? 1 : 0);
  // Không tiêu đề mà cột đầu là STT (1, 2, 3…) như bảng hệ thống cũ → bỏ cột STT, các cột sau theo thứ tự file mẫu.
  const numbered = !hasHeader && body.length > 0 && body.every(r => /^\d{1,4}$/.test(r[0] ?? '') && !/^\d+$/.test(r[1] ?? ''));
  const template = INVOICE_COLUMNS.map(c => c.field);
  const columns: ReadonlyArray<keyof InvoiceItemValues | undefined> = hasHeader ? headerFields : numbered ? [undefined, ...template] : template;

  body.forEach((cells, i) => {
    const item = Object.fromEntries(INVOICE_COLUMNS.map(c => [c.field, ''])) as RawItem;
    columns.forEach((field, c) => {
      if (field && cells[c] !== undefined && !item[field]) item[field] = cells[c];
    });
    item.qty = normalizePastedNumber(item.qty);
    item.price = normalizePastedNumber(item.price);
    if (checkItem(item, i + 1 + (hasHeader ? 1 : 0), items, skipped, true)) incomplete++;
  });
  return { items, skipped, incomplete };
}
