import { describe, expect, it } from 'vitest';
import { createOrderSchema, defaultValues, emptyInvoiceItem, emptyPackage } from '../schema';
import { MULTI_CATEGORY } from '../constants';
import { applyDocWeightRule, buildDraftPayload, goodsName, invoiceTotal, isDocOverweight, summarizePackages, volumetricWeight } from './shipment';

const pkg = (over: Partial<ReturnType<typeof emptyPackage>>) => ({ ...emptyPackage(), ...over });

describe('volumetricWeight', () => {
  it('D×R×C/5000 nhân số lượng', () => {
    expect(volumetricWeight(pkg({ qty: '1', length: '30', width: '20', height: '15' }))).toBe(1.8);
    expect(volumetricWeight(pkg({ qty: '2', length: '50', width: '40', height: '30' }))).toBe(24);
  });
  it('thiếu kích thước thì bằng 0', () => {
    expect(volumetricWeight(pkg({ length: '', width: '20', height: '15' }))).toBe(0);
  });
});

describe('summarizePackages', () => {
  it('cân tính cước = tổng max(cân thực, quy đổi) từng kiện', () => {
    const s = summarizePackages([
      pkg({ qty: '2', weight: '3', length: '50', width: '40', height: '30' }),
      pkg({ qty: '1', weight: '5' })
    ]);
    expect(s).toEqual({ pieces: 3, grossWeight: 11, volumetricWeight: 24, chargeableWeight: 29 });
  });
});

describe('applyDocWeightRule', () => {
  it('chứng từ ≤ 2kg giữ nguyên, > 2kg tự chuyển PACK', () => {
    expect(applyDocWeightRule({ type: 'DOC', autoConverted: false }, '2')).toEqual({ type: 'DOC', autoConverted: false, change: null });
    expect(applyDocWeightRule({ type: 'DOC', autoConverted: false }, '2.5')).toEqual({ type: 'PACK', autoConverted: true, change: 'toPack' });
  });
  it('PACK do hệ thống tự chuyển: cân giảm về ≤ 2kg thì trả lại chứng từ', () => {
    expect(applyDocWeightRule({ type: 'PACK', autoConverted: true }, '1')).toEqual({ type: 'DOC', autoConverted: false, change: 'toDoc' });
    expect(applyDocWeightRule({ type: 'PACK', autoConverted: true }, '3')).toEqual({ type: 'PACK', autoConverted: true, change: null });
  });
  it('PACK khách tự chọn thì giữ nguyên dù cân nhẹ', () => {
    expect(applyDocWeightRule({ type: 'PACK', autoConverted: false }, '1')).toEqual({ type: 'PACK', autoConverted: false, change: null });
  });
});

describe('isDocOverweight', () => {
  it('chỉ áp dụng cho DOC > 2kg', () => {
    expect(isDocOverweight('DOC', '2')).toBe(false);
    expect(isDocOverweight('DOC', '2.1')).toBe(true);
    expect(isDocOverweight('PACK', '10')).toBe(false);
  });
});

describe('invoiceTotal', () => {
  it('cộng shipping fee khi nhập, để trống thì chỉ tính tiền hàng', () => {
    const items = [{ ...emptyInvoiceItem(), qty: '5', price: '8' }];
    expect(invoiceTotal(items, '3.5')).toBe(43.5);
    expect(invoiceTotal(items, '')).toBe(40);
    expect(invoiceTotal(items, '0')).toBe(40);
  });
  it('cộng SL × đơn giá', () => {
    expect(invoiceTotal([{ ...emptyInvoiceItem(), qty: '5', price: '8' }, { ...emptyInvoiceItem(), qty: '2', price: '1.25' }])).toBe(42.5);
  });
});

describe('createOrderSchema', () => {
  const valid = () => {
    const v = defaultValues();
    v.shipper = { ...v.shipper, company: 'ABC', contact: 'A', tel: '0909 805 845', address: 'HCM' };
    v.receiver = { ...v.receiver, country: 'Singapore', city: 'Singapore', company: 'LINEX', contact: 'Lim', tel: '+65 6545 3778/79', addr1: '1 Raffles', addr2: 'Tower One' };
    v.goods.description = 'Clothes';
    v.packages = [pkg({ category: 'Quần áo, giày dép', weight: '8' })];
    v.invoice.items = [{ ...emptyInvoiceItem(), descEn: 'Dress', descVi: 'Váy', qty: '5', price: '8' }];
    return v;
  };

  it('hợp lệ với đủ trường', () => {
    expect(createOrderSchema.safeParse(valid()).success).toBe(true);
  });

  it('bắt buộc địa chỉ 2 và giới hạn 30 ký tự', () => {
    const v = valid();
    v.receiver.addr2 = '';
    v.receiver.addr1 = 'x'.repeat(31);
    const paths = createOrderSchema.safeParse(v).error?.issues.map(i => i.path.join('.'));
    expect(paths).toEqual(expect.arrayContaining(['receiver.addr2', 'receiver.addr1']));
  });

  it('số điện thoại: chỉ số và + ( ) - . /, tối thiểu 6 chữ số', () => {
    const v = valid();
    v.shipper.tel = '09a123';
    v.receiver.tel = '12345';
    const paths = createOrderSchema.safeParse(v).error?.issues.map(i => i.path.join('.'));
    expect(paths).toEqual(expect.arrayContaining(['shipper.tel', 'receiver.tel']));
  });

  it('mỗi dòng kiện bắt buộc nhóm hàng; mô tả mặt hàng chỉ bắt buộc khi dòng đó chọn nhiều loại', () => {
    const none = valid();
    none.packages = [pkg({ category: '', weight: '1' })];
    expect(createOrderSchema.safeParse(none).error?.issues.map(i => i.path.join('.'))).toContain('packages.0.category');

    const multi = valid();
    multi.shipment.pieces = '2';
    multi.packages = [pkg({ category: 'Quần áo, giày dép', weight: '1' }), pkg({ category: MULTI_CATEGORY, weight: '1' })];
    expect(createOrderSchema.safeParse(multi).error?.issues.map(i => i.path.join('.'))).toEqual(['packages.1.description']);
    multi.packages[1]!.description = 'Quần áo + mỹ phẩm';
    expect(createOrderSchema.safeParse(multi).success).toBe(true);
  });

  it('hàng hóa: bắt buộc mô tả tổng quan, cân từng kiện và số kiện dự kiến; cân cấp đơn tự tính', () => {
    const v = valid();
    v.goods.description = '';
    v.packages[0]!.weight = '';
    v.shipment.pieces = '';
    v.shipment.grossWeight = '';
    const paths = createOrderSchema.safeParse(v).error?.issues.map(i => i.path.join('.'));
    expect(paths).toEqual(expect.arrayContaining(['goods.description', 'packages.0.weight', 'shipment.pieces']));
    expect(paths).not.toContain('shipment.grossWeight');
  });

  it('tổng SL các dòng kiện phải bằng số kiện dự kiến', () => {
    const v = valid();
    v.shipment.pieces = '2';
    const issue = createOrderSchema.safeParse(v).error?.issues.find(i => i.path.join('.') === 'packages.root');
    expect(issue?.message).toContain('Tổng SL các dòng kiện là 1, chưa bằng số kiện dự kiến (2)');
    v.packages.push(pkg({ category: 'Đồ chơi', weight: '1' }));
    expect(createOrderSchema.safeParse(v).success).toBe(true);
  });

  it('chứng từ: chỉ cần số kiện và cân nặng, nội dung mặc định Documents', () => {
    const doc = valid();
    doc.shipment.type = 'DOC';
    doc.shipment.pieces = '1';
    doc.shipment.grossWeight = '';
    doc.goods.description = '';
    expect(createOrderSchema.safeParse(doc).error?.issues.map(i => i.path.join('.'))).toEqual(['shipment.grossWeight']);
    doc.shipment.grossWeight = '0.5';
    expect(createOrderSchema.safeParse(doc).success).toBe(true);
    expect(buildDraftPayload(doc, 'ready').content).toBe('Documents');
  });

  it('invoice: bắt buộc tên tiếng Việt và hình thức chịu thuế', () => {
    const v = valid();
    v.invoice.items[0]!.descVi = '';
    v.invoice.dutyTerms = '';
    const paths = createOrderSchema.safeParse(v).error?.issues.map(i => i.path.join('.'));
    expect(paths).toEqual(expect.arrayContaining(['invoice.items.0.descVi', 'invoice.dutyTerms']));
    expect(defaultValues().invoice.dutyTerms).toBe('DDU');
  });

  it('PACK bắt buộc invoice; DOC thì không', () => {
    const pack = valid();
    pack.invoice.items = [emptyInvoiceItem()];
    expect(createOrderSchema.safeParse(pack).success).toBe(false);

    const doc = valid();
    doc.shipment.type = 'DOC';
    doc.shipment.grossWeight = '0.5';
    doc.invoice.items = [emptyInvoiceItem()];
    expect(createOrderSchema.safeParse(doc).success).toBe(true);
  });
});

describe('goodsName', () => {
  const goods = defaultValues().goods;

  it('tên hàng từng dòng kiện: nhóm thường → tên nhóm; nhiều loại → mô tả; bỏ trùng, nối bằng dấu phẩy', () => {
    const packages = [
      pkg({ category: 'Quần áo, giày dép', description: 'bỏ qua' }),
      pkg({ category: MULTI_CATEGORY, description: ' Mỹ phẩm + đồ chơi ' }),
      pkg({ category: 'Quần áo, giày dép' })
    ];
    expect(goodsName({ goods, packages })).toBe('Quần áo, giày dép, Mỹ phẩm + đồ chơi');
  });

  it('nháp cũ khai nhóm ở cấp đơn: dùng cách cũ', () => {
    expect(goodsName({ goods: { ...goods, category: 'Đồ chơi' }, packages: [pkg({})] })).toBe('Đồ chơi');
    expect(goodsName({ goods: { ...goods, description: 'Váy nữ' }, packages: [pkg({})] })).toBe('Váy nữ');
  });
});

describe('buildDraftPayload', () => {
  it('dùng hub làm dịch vụ và cân tính cước từ bảng kiện', () => {
    const v = defaultValues();
    v.receiver.company = 'LINEX';
    v.receiver.country = 'Singapore';
    v.packages = [pkg({ qty: '1', weight: '8', length: '30', width: '20', height: '15', category: MULTI_CATEGORY, description: 'Váy nữ' })];
    const d = buildDraftPayload(v, 'ready');
    expect(d).toMatchObject({ stt: 'ready', cnee: 'LINEX', service: 'Chuyên tuyến - Singapore', pcs: '1 kiện · 8 kg', content: 'Váy nữ' });
  });
});
