import { fill } from '@/shared/i18n';
import type { InvoiceItemValues, PackageValues } from '../schema';

/*
 * Invoice riêng từng kiện (tuyến trong PER_PACKAGE_INVOICE_HUBS, vd Chuyên tuyến Úc / EU / Canada).
 * Dòng hàng vẫn là 1 mảng invoice.items; mỗi dòng mang `package` = số thứ tự kiện ("1".."N"), "" = invoice chung.
 * Hàm thuần — có test. Không import giá trị từ schema (schema dùng lại các hàm này).
 */

type Item = InvoiceItemValues;

const isCount = (v: string | undefined) => /^\d+$/.test((v ?? '').trim());

/** Số kiện của dòng hàng: "2" → 2; trống / không hợp lệ → 0. */
export const packageOf = (item: Pick<Item, 'package'>): number => (isCount(item.package) ? Number(item.package) : 0);

/** Tổng số kiện = tổng SL các dòng kiện (số nguyên ≥ 1). */
export const packageCount = (packages: ReadonlyArray<Pick<PackageValues, 'qty'>>): number =>
  packages.reduce((sum, p) => sum + (isCount(p.qty) ? Number(p.qty) : 0), 0);

/** Dòng hàng chưa nhập gì (bỏ được khi bớt kiện mà không mất dữ liệu). */
export const isBlankItem = (item: Item): boolean =>
  !item.descEn.trim() && !item.descVi.trim() && !item.manufacturer.trim() && !item.hs.trim() && !item.qty.trim() && !item.price.trim();

/**
 * Đưa danh sách dòng hàng về đúng chế độ invoice. Trả null khi không cần đổi.
 * - Invoice chung: bỏ số kiện ở mọi dòng (gộp lại thành 1 invoice, không mất dòng nào).
 * - Riêng từng kiện: dòng chưa có kiện → kiện 1; kiện 1..N thiếu dòng → thêm 1 dòng trống;
 *   dòng thuộc kiện đã bị bớt mà còn trống → bỏ; còn dữ liệu thì giữ lại để khách tự chuyển / xóa.
 */
export function syncPackageItems(items: ReadonlyArray<Item>, perPackage: boolean, count: number, empty: () => Item): Item[] | null {
  if (!perPackage) {
    return items.some(i => (i.package ?? '') !== '') ? items.map(i => ({ ...i, package: '' })) : null;
  }
  if (count < 1) return null;
  let changed = false;
  const next: Item[] = [];
  for (const item of items) {
    const pkg = packageOf(item);
    if (pkg === 0) {
      next.push({ ...item, package: '1' });
      changed = true;
    } else if (pkg > count && isBlankItem(item)) changed = true;
    else next.push(item);
  }
  for (let k = 1; k <= count; k++) {
    if (!next.some(i => packageOf(i) === k)) {
      next.push({ ...empty(), package: String(k) });
      changed = true;
    }
  }
  return changed ? next : null;
}

export interface PackageGroup {
  /** Số kiện (1..N); kiện lớn hơn N là kiện đã bị bớt nhưng còn dòng hàng. */
  pkg: number;
  /** Vị trí các dòng hàng của kiện trong invoice.items. */
  indices: number[];
  /** true khi kiện không còn trong bảng kiện (đã bớt số kiện). */
  extra: boolean;
}

/** Nhóm dòng hàng theo kiện để hiển thị: kiện 1..N, rồi các kiện thừa (nếu có). */
export function packageGroups(items: ReadonlyArray<Item>, count: number): PackageGroup[] {
  const max = items.reduce((m, i) => Math.max(m, packageOf(i)), count);
  const groups: PackageGroup[] = [];
  for (let k = 1; k <= max; k++) {
    const indices = items.flatMap((i, index) => (packageOf(i) === k ? [index] : []));
    if (k <= count || indices.length) groups.push({ pkg: k, indices, extra: k > count });
  }
  return groups;
}

export const PACKAGE_MISSING_ITEMS = 'Kiện {pkg} chưa có mặt hàng trong invoice.';
export const PACKAGE_REMOVED = 'Kiện {pkg} không còn trong chi tiết kiện hàng (tổng {count} kiện) — xóa dòng hàng hoặc tăng số kiện.';

/** Lỗi chặn lưu đơn của invoice riêng từng kiện: kiện thiếu mặt hàng, dòng hàng thuộc kiện đã bớt. */
export function packageInvoiceIssues(items: ReadonlyArray<Item>, count: number): string[] {
  const issues: string[] = [];
  for (const g of packageGroups(items, count)) {
    if (g.extra) issues.push(fill(PACKAGE_REMOVED, { pkg: g.pkg, count }));
    else if (!g.indices.some(i => !isBlankItem(items[i]!))) issues.push(fill(PACKAGE_MISSING_ITEMS, { pkg: g.pkg }));
  }
  return issues;
}

/**
 * Thêm / thay dòng hàng (từ thư viện mặt hàng, invoice cũ, file Excel) vào invoice.
 * `target` = số kiện khi invoice riêng từng kiện: dòng mới gán vào kiện đó, "replace" chỉ thay dòng của kiện đó;
 * không có `target` = invoice chung. Dòng trống sẵn có của nơi nhận được thay luôn (không để lại dòng rỗng).
 */
export function writeInvoiceItems(items: ReadonlyArray<Item>, list: ReadonlyArray<Item>, mode: 'append' | 'replace', target?: number): Item[] {
  if (!target) {
    const onlyBlank = items.length === 1 && isBlankItem(items[0]!);
    return mode === 'replace' || onlyBlank ? [...list] : [...items, ...list];
  }
  const tagged = list.map(i => ({ ...i, package: String(target) }));
  const own = items.filter(i => packageOf(i) === target);
  const dropOwn = mode === 'replace' || own.every(isBlankItem);
  return [...items.filter(i => packageOf(i) !== target || !dropOwn), ...tagged];
}

/** Chép mặt hàng của kiện trước sang kiện `target` (thay các dòng trống của kiện đó). */
export function copyPreviousPackage(items: ReadonlyArray<Item>, target: number): Item[] {
  const source = items.filter(i => packageOf(i) === target - 1 && !isBlankItem(i));
  if (target < 2 || !source.length) return [...items];
  const kept = items.filter(i => packageOf(i) !== target || !isBlankItem(i));
  return [...kept, ...source.map(i => ({ ...i, package: String(target) }))];
}
