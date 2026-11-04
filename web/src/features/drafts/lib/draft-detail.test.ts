import { describe, expect, it } from 'vitest';
import { draftDetail } from './draft-detail';

const pack = {
  shipper: { company: 'SCS CO., LTD', contact: 'MR TUẤN ANH', tel: '0938061868', address: 'HCM', taxId: '', email: '', country: 'Vietnam', branch: 'TP.HCM' },
  service: { carrier: 'Chuyên tuyến', hub: 'Chuyên tuyến - Singapore', reference: 'PO-1' },
  shipment: { type: 'PACK', pieces: '2', grossWeight: '10' },
  receiver: { company: 'HONG AN DUONG', contact: 'andrew', tel: '2125550100', phoneCode: '+1', country: 'United States', city: 'New York City', state: 'New York', postal: '10002', addr1: '1 Main St', addr2: 'Apt 2', addr3: '', taxId: '', email: '' },
  goods: { category: '', description: 'Clothes', docContent: '', multi: [] },
  packages: [{ qty: '2', packaging: 'Thùng carton', length: '50', width: '40', height: '30', weight: '5', category: 'Quần áo, giày dép', description: '' }],
  addons: [],
  invoice: { exportType: 'GIFT', dutyTerms: 'DDP', currency: 'USD', shippingFee: '3', items: [{ descEn: 'Dress', descVi: 'Đầm', manufacturer: '', origin: 'VN', hs: '6204', qty: '5', unit: 'PCS', price: '8' }] }
};

describe('draftDetail', () => {
  it('hàng hóa: người gửi / nhận theo bố cục phiếu, kiện có nhóm hàng, cân tính cước, invoice', () => {
    const d = draftDetail(pack);
    expect(d.isDoc).toBe(false);
    expect(d.receiver.name).toBe('HONG AN DUONG');
    expect(d.receiver.lines).toEqual(['1 Main St', 'Apt 2', '', 'New York City, New York, 10002', 'UNITED STATES']);
    expect(d.receiver.fields).toContainEqual(['Điện thoại', '+1 2125550100']);
    expect(d.packages[0]).toEqual({ qty: '2', packaging: 'Thùng carton', category: 'Quần áo, giày dép', size: '50 × 40 × 30', weight: '5 kg' });
    expect(d.packageTotals?.chargeableWeight).toBe(24); // max(2×5, 2×50×40×30/5000)
    expect(d.shipment).toContainEqual(['Nội dung hàng', 'Clothes']);
    expect(d.shipment).toContainEqual(['Chịu thuế', 'DDP — người gửi chịu thuế']);
    expect(d.items[0]).toMatchObject({ descEn: 'Dress', descVi: 'Đầm', qty: '5 PCS', amount: 40 });
    expect(d.invoice).toContainEqual(['Hình thức xuất khẩu', 'gift (no commercial value)']);
    expect(d.invoiceTotal).toBe(43);
    expect(d.shippingFee).toBe(3);
  });

  it('chứng từ: không có kiện / invoice, nội dung mặc định Document', () => {
    const d = draftDetail({ ...pack, shipment: { type: 'DOC', pieces: '1', grossWeight: '0.5' } });
    expect(d.isDoc).toBe(true);
    expect(d.packages).toEqual([]);
    expect(d.items).toEqual([]);
    expect(d.shipment).toContainEqual(['Nội dung hàng', 'Document']);
    expect(d.shipment).toContainEqual(['Cân nặng', '0.5 kg']);
  });

  it('nháp cũ thiếu dữ liệu: để trống, không lỗi', () => {
    const d = draftDetail(undefined);
    expect(d.shipper.name).toBe('');
    expect(d.packageTotals).toBeNull();
  });
});
