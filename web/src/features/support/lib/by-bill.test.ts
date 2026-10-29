import { describe, expect, it } from 'vitest';
import type { SupportTicket } from '../types';
import { openTicketOf, ticketsOfBill } from './by-bill';

const ticket = (id: string, bill: string, status: SupportTicket['status']): SupportTicket => ({
  id, bill, status, cnee: '', ct: '', type: 'Giao chậm / trễ hẹn', lv: 'mid', desc: '', req: '', contact: '', date: ''
});

const list = [ticket('TRB-3', 'VAHCM6010842NO', 'done'), ticket('TRB-2', 'vahcm6010842no ', 'doing'), ticket('TRB-1', 'VAHCM6010840NZ', 'new')];

describe('sự cố theo vận đơn', () => {
  it('lọc sự cố của 1 bill, không phân biệt hoa thường / khoảng trắng', () => {
    expect(ticketsOfBill(list, 'VAHCM6010842NO').map(t => t.id)).toEqual(['TRB-3', 'TRB-2']);
    expect(ticketsOfBill(list, 'KHONGCO')).toEqual([]);
  });

  it('sự cố chưa xong đầu tiên để nhắc trên dòng đơn', () => {
    expect(openTicketOf(list, 'VAHCM6010842NO')?.id).toBe('TRB-2');
    expect(openTicketOf([ticket('TRB-9', 'X', 'done')], 'X')).toBeNull();
  });
});
