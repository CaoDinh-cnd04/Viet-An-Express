import { fill } from '@/shared/i18n';
import { parseCsv } from '@/shared/lib/files';
import { normalizeUnit, qtyError } from './units';
import { hasMaxDecimals } from './cell-nav';
import { RULES } from '../constants';
import type { InvoiceItemValues } from '../schema';

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
}

/** CSV → dòng invoice. Dòng thiếu tên EN hoặc SL/đơn giá không phải số bị bỏ qua. */
export function parseInvoiceCsv(text: string): InvoiceImportResult {
  const { rows } = parseCsv(text);
  const items: InvoiceItemValues[] = [];
  const skipped: string[] = [];

  rows.forEach((raw, i) => {
    const lower = Object.fromEntries(Object.entries(raw).map(([k, v]) => [k.toLowerCase(), v.trim()]));
    const get = (header: string) => lower[header.toLowerCase()] ?? '';
    const item = Object.fromEntries(INVOICE_COLUMNS.map(c => [c.field, get(c.header)])) as unknown as InvoiceItemValues;
    const line = i + 2;
    if (!item.descEn) return skipped.push(fill('Dòng {line}: thiếu DescriptionEN', { line }));
    if (!(Number(item.qty) > 0)) return skipped.push(fill('Dòng {line}: Qty không hợp lệ', { line }));
    if (item.price === '' || !(Number(item.price) >= 0)) return skipped.push(fill('Dòng {line}: UnitPrice không hợp lệ', { line }));
    if (!hasMaxDecimals(item.price, RULES.priceDecimals)) return skipped.push(fill('Dòng {line}: UnitPrice tối đa 2 số lẻ', { line }));
    const unit = normalizeUnit(item.unit);
    if (qtyError(item.qty, unit)) return skipped.push(fill('Dòng {line}: Qty phải là số nguyên với ĐVT {unit}', { line, unit }));
    items.push({ ...item, origin: item.origin || 'VN', unit });
  });

  return { items, skipped };
}
