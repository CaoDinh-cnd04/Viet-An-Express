import { describe, expect, it } from 'vitest';
import { cleanLoadedContacts, copyOrderValues } from './copy-order';
import { defaultValues, emptyPackage, emptyInvoiceItem } from '../schema';

describe('copy entire order form', () => {
  it('preserves every group, references, quantities, original decimals, invoice and addons', () => {
    const source = defaultValues();
    source.shipper = { ...source.shipper, company: 'Original sender', originalShipper: 'Forwarder customer', branch: 'Hà Nội' };
    source.receiver = { ...source.receiver, company: 'Original receiver', addr1: 'Address one', addr2: 'Address two', addr3: 'Address three', phoneCode: '+1', taxId: 'TAX' };
    source.service = { carrier: 'DHL', hub: 'DHL - Original hub', reference: 'KEEP-REF' };
    source.shipment = { type: 'PACK', pieces: '3', grossWeight: '8' };
    source.goods = { category: 'Clothes', description: 'Content', docContent: '', multi: [{ category: 'Clothes', note: 'Two pieces' }] };
    source.packages = [{ ...emptyPackage(), qty: '2', weight: '3', length: '20.25', width: '30.5', height: '40', category: 'Clothes', description: 'Keep description' }, { ...emptyPackage(), weight: '2', category: 'Shoes' }];
    source.addons = ['Đóng thuế hộ (DDP)', 'Original extra service'];
    source.invoice = { ...source.invoice, currency: 'CAD', exportType: 'SAMPLE', dutyTerms: 'DDP', shippingFee: '12.50', items: [{ ...emptyInvoiceItem(), descEn: 'Item', descVi: 'Hàng', manufacturer: 'Maker\nAddress', qty: '2', price: '1.25', hs: '610910', origin: 'CN' }] };
    const copy = copyOrderValues(source);
    expect(copy).toEqual(source);
    copy.packages[0]!.weight = '99';
    copy.invoice.items[0]!.manufacturer = 'Changed';
    copy.addons.push('More');
    expect(source.packages[0]!.weight).toBe('3');
    expect(source.invoice.items[0]!.manufacturer).toBe('Maker\nAddress');
    expect(source.addons).toHaveLength(2);
  });
  it('fills missing fields in older snapshots and maps Excel packaging without deleting saved fields', () => {
    const source = { goods: { description: 'Dresses' }, packages: [{ type: 'BAG', qty: '2', weight: '1.5', length: '10', width: '20', height: '30', packaging: '', category: '' }], invoice: { currency: 'SGD', items: [{ descEn: 'Dress', price: '5' }] }, bill: 'OLD-BILL', id: 'OLD-ID' };
    const copy = copyOrderValues(source as unknown as Parameters<typeof copyOrderValues>[0]);
    expect(copy.packages[0]!.packaging).toBe('Bao / túi');
    expect(copy.packages[0]!.category).toBe('Nhiều loại hàng');
    expect(copy.packages[0]!.description).toBe('Dresses');
    expect(copy.invoice.items[0]!.unit).toBe('PCS');
    expect(copy.invoice.currency).toBe('SGD');
    expect(copy).not.toHaveProperty('bill');
    expect(copy).not.toHaveProperty('id');
  });
  it('keeps document type, content, pieces and actual weight', () => {
    const source = defaultValues();
    source.shipment = { type: 'DOC', pieces: '2', grossWeight: '1.75' };
    source.goods.docContent = 'Signed contract';
    source.packages = [];
    expect(copyOrderValues(source)).toEqual(source);
  });
});

describe('cleanLoadedContacts', () => {
  it('bỏ email sai (vd "=") và ký tự lạ trong SĐT của đơn cũ', () => {
    const v = defaultValues();
    v.shipper.email = '=';
    v.shipper.tel = '0909 123 456 (Anh)';
    v.receiver.email = 'a@b.com';
    const out = cleanLoadedContacts(v);
    expect(out.shipper.email).toBe('');
    expect(out.shipper.tel).toBe('0909 123 456 ()');
    expect(out.receiver.email).toBe('a@b.com');
  });
});

describe('mở đơn nháp thiếu trường form', () => {
  it('goods.multi thiếu → mảng rỗng (không báo lỗi ô ẩn)', () => {
    const draft = { ...defaultValues(), goods: { category: '', description: 'Clothes', docContent: '' } } as unknown as Partial<ReturnType<typeof defaultValues>>;
    expect(copyOrderValues(draft).goods.multi).toEqual([]);
  });
});
