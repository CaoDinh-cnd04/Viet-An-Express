import { describe, expect, it } from 'vitest';
import type { TroubleTicket } from '../types';
import { openTroubleOf, troublesOfBill } from './by-bill';

const ticket = (id: string, bill: string, status: TroubleTicket['status']): TroubleTicket => ({
  id, bill, status, cnee: '', ct: '', type: 'Giao chậm / trễ hẹn', lv: 'mid', desc: '', req: '', contact: '', date: ''
});

const list = [ticket('TRB-3', 'VAHCM6010842NO', 'done'), ticket('TRB-2', 'vahcm6010842no ', 'doing'), ticket('TRB-1', 'VAHCM6010840NZ', 'new')];

describe('sự cố theo vận đơn', () => {
  it('lọc sự cố của 1 bill, không phân biệt hoa thường / khoảng trắng', () => {
    expect(troublesOfBill(list, 'VAHCM6010842NO').map(t => t.id)).toEqual(['TRB-3', 'TRB-2']);
    expect(troublesOfBill(list, 'KHONGCO')).toEqual([]);
  });

  it('sự cố chưa xong đầu tiên để nhắc trên dòng đơn', () => {
    expect(openTroubleOf(list, 'VAHCM6010842NO')?.id).toBe('TRB-2');
    expect(openTroubleOf([ticket('TRB-9', 'X', 'done')], 'X')).toBeNull();
  });
});
