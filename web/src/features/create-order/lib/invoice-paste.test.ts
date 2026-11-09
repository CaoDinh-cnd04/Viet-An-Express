import { describe, expect, it } from 'vitest';
import { normalizePastedNumber, parseInvoicePaste, splitPastedTable } from './invoice-import';

/** 1 dòng copy từ Excel: các ô ngăn bằng Tab. */
const row = (...cells: string[]) => cells.join('\t');
const lines = (...rows: string[]) => rows.join('\n');

describe('dán dữ liệu Excel vào invoice', () => {
  it('tách ô theo Tab, giữ xuống dòng trong ô bọc nháy (Alt+Enter), bỏ dòng trống', () => {
    expect(splitPastedTable('a\tb\r\n\r\n"Cty ABC\n12 Lê Lợi"\tc\n')).toEqual([['a', 'b'], ['Cty ABC\n12 Lê Lợi', 'c']]);
  });

  it('số kiểu Việt / Mỹ', () => {
    expect(['1,234.50', '8,5', '1,500', '12.5', ' 3 '].map(normalizePastedNumber)).toEqual(['1234.50', '8.5', '1500', '12.5', '3']);
  });

  it('không có tiêu đề → cột theo thứ tự file mẫu', () => {
    const { items, skipped } = parseInvoicePaste(lines(
      row("Women's dress", 'Váy nữ', 'Cty May ABC', 'VN', '6204.43', '5', 'pcs', '8,5'),
      row('Dried mango', 'Xoài sấy', '', '', '0804.50', '2', 'KG', '12')
    ));
    expect(skipped).toEqual([]);
    expect(items).toHaveLength(2);
    expect(items[0]).toMatchObject({ descEn: "Women's dress", descVi: 'Váy nữ', manufacturer: 'Cty May ABC', hs: '6204.43', qty: '5', unit: 'PCS', price: '8.5' });
    expect(items[1]).toMatchObject({ origin: 'VN', qty: '2', price: '12' });
  });

  it('có dòng tiêu đề (tên mẫu hoặc tiếng Việt) → đọc theo tên cột, cột lạ bỏ qua', () => {
    const { items } = parseInvoicePaste(lines(row('STT', 'Mô tả tiếng Anh', 'Mã HS', 'SL', 'ĐVT', 'Đơn giá'), row('1', 'T-shirt', '6109.10', '3', 'PCS', '4')));
    expect(items).toEqual([expect.objectContaining({ descEn: 'T-shirt', hs: '6109.10', qty: '3', unit: 'PCS', price: '4' })]);
    const byTemplate = parseInvoicePaste(lines(row('UnitPrice', 'DescriptionEN', 'Qty'), row('2', 'Cap', '1')));
    expect(byTemplate.items[0]).toMatchObject({ descEn: 'Cap', qty: '1', price: '2' });
  });

  it('dòng lỗi bị bỏ qua kèm số dòng', () => {
    const { items, skipped } = parseInvoicePaste(lines(
      row('', 'Thiếu tên EN', '', '', '', '1', 'PCS', '1'),
      row('Bag', '', '', '', '', '0', 'PCS', '1'),
      row('Cap', '', '', '', '', '1', 'PCS', '1')
    ));
    expect(items).toHaveLength(1);
    expect(skipped).toEqual(['Dòng 1: thiếu DescriptionEN', 'Dòng 2: Qty không hợp lệ']);
  });

  it('ô trống / chữ thường không phải bảng → không có dòng nào', () => {
    expect(parseInvoicePaste('  \n\n')).toEqual({ items: [], skipped: [], incomplete: 0 });
  });

  it('chưa có đơn giá / SL (khách nhập sau) → vẫn thêm dòng, đếm dòng còn trống; ô có giá trị sai vẫn bỏ', () => {
    // Như dữ liệu khách dán 06/11/2026: cột UnitPrice trống, dòng cuối thiếu cả Qty / Unit (Excel bỏ ô trống cuối dòng).
    const { items, skipped, incomplete } = parseInvoicePaste(lines(
      row("Premium Bird's Nest", 'Yến sào cao cấp Ngọc Bích', '', 'VN', '', '6', 'BOX', ''),
      row('Dried Dates with Walnuts', 'Táo khô kẹp óc chó', '', 'VN', '', '13', 'PACK', ''),
      row('Instant Noodles', 'Mì ăn liền', '', 'VN'),
      row('Bag', '', '', '', '', 'abc', 'PCS', '1')
    ));
    expect(items.map(i => [i.descEn, i.descVi, i.qty, i.price])).toEqual([
      ["Premium Bird's Nest", 'Yến sào cao cấp Ngọc Bích', '6', ''],
      ['Dried Dates with Walnuts', 'Táo khô kẹp óc chó', '13', ''],
      ['Instant Noodles', 'Mì ăn liền', '', '']
    ]);
    expect(items.map(i => i.unit).slice(0, 2)).toEqual(['BOX', 'PACK']);
    expect(incomplete).toBe(3);
    expect(skipped).toEqual(['Dòng 4: Qty không hợp lệ']);
  });
});

describe('dán bảng invoice của hệ thống cũ', () => {
  const LEGACY_HEADER = row('STT', 'TÊN HÀNG TIẾNG ANH', 'TÊN HÀNG TIẾNG VIỆT', 'MATERIAL COMPOSITION + MANUFACTURER & ADDRESS (ENGLISH)',
    'COUNTRY OF ORIGIN', 'HS CODE (HTSUS 10 DIGITS)', 'QUANTITY', 'UNIT');

  it('tiêu đề hệ thống cũ → nhận cột theo từ khóa, bỏ STT; thiếu đơn giá vẫn thêm', () => {
    const { items, skipped, incomplete } = parseInvoicePaste(lines(
      LEGACY_HEADER,
      row('1', "Edible Bird's Nest", 'Yến sào', "Edible bird's nest 100%. Manufacturer: Not specified", 'VN', '0410900000', '6', 'BOX'),
      row('11', 'Instant Noodles', 'Miến ăn liền', 'Dried starch noodles. Manufacturer: Not specified', 'VN', '1902300060', '', 'BAG')
    ));
    expect(skipped).toEqual([]);
    expect(items.map(i => [i.descEn, i.descVi, i.manufacturer, i.origin, i.hs, i.qty, i.price])).toEqual([
      ["Edible Bird's Nest", 'Yến sào', "Edible bird's nest 100%. Manufacturer: Not specified", 'VN', '0410900000', '6', ''],
      ['Instant Noodles', 'Miến ăn liền', 'Dried starch noodles. Manufacturer: Not specified', 'VN', '1902300060', '', '']
    ]);
    expect(incomplete).toBe(2);
  });

  it('không tiêu đề mà cột đầu là STT → bỏ cột STT', () => {
    const { items } = parseInvoicePaste(lines(
      row('1', 'Shampoo', 'Dầu gội', 'Hair cleansing preparation', 'VN', '3305100000', '3', 'BOTTLE'),
      row('2', 'Peanut Candy', 'Kẹo lạc', 'Peanuts, sugar', 'VN', '1704903550', '2', 'BAG')
    ));
    expect(items.map(i => [i.descEn, i.hs, i.qty])).toEqual([['Shampoo', '3305100000', '3'], ['Peanut Candy', '1704903550', '2']]);
  });
});
