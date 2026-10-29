import { describe, expect, it } from 'vitest';
import { isPerPackageInvoice } from '@/shared/config/domain';
import { createOrderSchema, defaultValues, emptyInvoiceItem, emptyPackage, type InvoiceItemValues } from '../schema';
import {
  copyPreviousPackage, packageCount, packageGroups, packageInvoiceIssues, syncPackageItems, writeInvoiceItems
} from './package-invoice';

const item = (descEn: string, pkg = '', extra: Partial<InvoiceItemValues> = {}): InvoiceItemValues =>
  ({ ...emptyInvoiceItem(), descEn, descVi: descEn, qty: '1', price: '10', package: pkg, ...extra });
const blank = (pkg = '') => ({ ...emptyInvoiceItem(), package: pkg });
const pkgs = (items: InvoiceItemValues[]) => items.map(i => i.package);

describe('isPerPackageInvoice', () => {
  it('Chuyên tuyến Úc / EU / Canada khai invoice riêng từng kiện, tuyến khác thì không', () => {
    expect(isPerPackageInvoice('Chuyên tuyến - AU_AuPost')).toBe(true);
    expect(isPerPackageInvoice('Chuyên tuyến - AU_Toll Vip (Hàng thường)')).toBe(true);
    expect(isPerPackageInvoice('Chuyên tuyến - EU')).toBe(true);
    expect(isPerPackageInvoice('Chuyên tuyến - Canada')).toBe(true);
    expect(isPerPackageInvoice('Chuyên tuyến - Singapore')).toBe(false);
    expect(isPerPackageInvoice('SEA - Australia')).toBe(false);
    expect(isPerPackageInvoice('')).toBe(false);
  });

  it('quy tắc "Hãng - *" áp cho mọi hub của hãng', () => {
    expect(isPerPackageInvoice('UPS - Dubai', ['UPS - *'])).toBe(true);
    expect(isPerPackageInvoice('UPSX - Dubai', ['UPS - *'])).toBe(false);
  });
});

describe('packageCount', () => {
  it('tổng SL các dòng kiện, bỏ ô chưa nhập', () => {
    expect(packageCount([{ qty: '3' }, { qty: '2' }, { qty: '' }, { qty: 'a' }])).toBe(5);
  });
});

describe('syncPackageItems', () => {
  it('bật invoice từng kiện: dòng cũ vào kiện 1, kiện còn thiếu được thêm dòng trống', () => {
    const next = syncPackageItems([item('Dress'), item('Shirt')], true, 3, emptyInvoiceItem)!;
    expect(pkgs(next)).toEqual(['1', '1', '2', '3']);
  });

  it('đủ rồi thì không đổi', () => {
    expect(syncPackageItems([item('A', '1'), item('B', '2')], true, 2, emptyInvoiceItem)).toBeNull();
  });

  it('bớt kiện: dòng trống của kiện thừa bị bỏ, dòng có dữ liệu được giữ', () => {
    const next = syncPackageItems([item('A', '1'), blank('2'), item('C', '3')], true, 1, emptyInvoiceItem)!;
    expect(next.map(i => [i.descEn, i.package])).toEqual([['A', '1'], ['C', '3']]);
  });

  it('tắt (đổi sang tuyến thường): gộp về invoice chung, không mất dòng', () => {
    const next = syncPackageItems([item('A', '1'), item('B', '2')], false, 2, emptyInvoiceItem)!;
    expect(next.map(i => [i.descEn, i.package])).toEqual([['A', ''], ['B', '']]);
    expect(syncPackageItems([item('A')], false, 2, emptyInvoiceItem)).toBeNull();
  });
});

describe('packageGroups / packageInvoiceIssues', () => {
  it('nhóm theo kiện và đánh dấu kiện đã bớt', () => {
    const items = [item('A', '1'), item('B', '2'), item('C', '1'), item('D', '4')];
    expect(packageGroups(items, 3)).toEqual([
      { pkg: 1, indices: [0, 2], extra: false },
      { pkg: 2, indices: [1], extra: false },
      { pkg: 3, indices: [], extra: false },
      { pkg: 4, indices: [3], extra: true }
    ]);
  });

  it('báo kiện chưa có mặt hàng và kiện không còn', () => {
    const issues = packageInvoiceIssues([item('A', '1'), blank('2'), item('D', '3')], 2);
    expect(issues).toEqual([
      'Kiện 2 chưa có mặt hàng trong invoice.',
      'Kiện 3 không còn trong chi tiết kiện hàng (tổng 2 kiện) — xóa dòng hàng hoặc tăng số kiện.'
    ]);
    expect(packageInvoiceIssues([item('A', '1'), item('B', '2')], 2)).toEqual([]);
  });
});

describe('writeInvoiceItems', () => {
  it('invoice chung: thay dòng trống mặc định, còn lại thì nối thêm', () => {
    expect(writeInvoiceItems([blank()], [item('A')], 'append').map(i => i.descEn)).toEqual(['A']);
    expect(writeInvoiceItems([item('A')], [item('B')], 'append').map(i => i.descEn)).toEqual(['A', 'B']);
    expect(writeInvoiceItems([item('A')], [item('B')], 'replace').map(i => i.descEn)).toEqual(['B']);
  });

  it('invoice từng kiện: chỉ ghi vào kiện đang chọn', () => {
    const items = [item('A', '1'), blank('2')];
    expect(writeInvoiceItems(items, [item('B')], 'append', 2).map(i => [i.descEn, i.package])).toEqual([['A', '1'], ['B', '2']]);
    expect(writeInvoiceItems([item('A', '1'), item('X', '2')], [item('B')], 'replace', 2).map(i => [i.descEn, i.package]))
      .toEqual([['A', '1'], ['B', '2']]);
  });
});

describe('copyPreviousPackage', () => {
  it('chép mặt hàng kiện trước sang kiện đang khai, thay dòng trống', () => {
    const next = copyPreviousPackage([item('A', '1'), item('B', '1'), blank('2')], 2);
    expect(next.map(i => [i.descEn, i.package])).toEqual([['A', '1'], ['B', '1'], ['A', '2'], ['B', '2']]);
  });
});

describe('schema — invoice riêng từng kiện', () => {
  const order = (hub: string, items: InvoiceItemValues[]) => {
    const v = defaultValues();
    v.service = { carrier: 'Chuyên tuyến', hub, reference: '' };
    v.shipment.pieces = '2';
    v.packages = [{ ...emptyPackage(), qty: '2', weight: '3', category: 'Quần áo' }];
    v.invoice.items = items;
    return v;
  };
  const rootMessage = (v: ReturnType<typeof order>) =>
    createOrderSchema.safeParse(v).error?.issues.find(i => i.path.join('.') === 'invoice.items.root')?.message;

  it('Chuyên tuyến EU: kiện thiếu mặt hàng thì chặn lưu', () => {
    expect(rootMessage(order('Chuyên tuyến - EU', [item('A', '1')]))).toBe('Kiện 2 chưa có mặt hàng trong invoice.');
    expect(rootMessage(order('Chuyên tuyến - EU', [item('A', '1'), item('B', '2')]))).toBeUndefined();
  });

  it('tuyến thường không kiểm tra theo kiện', () => {
    expect(rootMessage(order('Chuyên tuyến - Singapore', [item('A')]))).toBeUndefined();
  });
});
