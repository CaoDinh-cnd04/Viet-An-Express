import { describe, expect, it } from 'vitest';
import { expiredPrintJobKeys, parsePrintJob, printJobKey } from './print-job';

describe('parsePrintJob', () => {
  it('đọc lệnh in và lệnh hủy', () => {
    expect(parsePrintJob(JSON.stringify({ bills: ['VA1', 'VA2'], doc: 'bill-a4,invoice', at: 1 }))).toEqual({
      bills: ['VA1', 'VA2'],
      doc: 'bill-a4,invoice'
    });
    expect(parsePrintJob(JSON.stringify({ cancel: true, at: 1 }))).toEqual({ cancel: true });
  });

  it('dữ liệu rỗng / hỏng / sai dạng → null', () => {
    expect(parsePrintJob(null)).toBeNull();
    expect(parsePrintJob('{')).toBeNull();
    expect(parsePrintJob(JSON.stringify({ bills: [1], doc: 'bill-a4' }))).toBeNull();
    expect(parsePrintJob(JSON.stringify({ bills: ['VA1'] }))).toBeNull();
  });
});

describe('expiredPrintJobKeys', () => {
  it('chỉ dọn lệnh in quá 10 phút hoặc hỏng, không đụng khóa khác', () => {
    const now = 1_000_000_000;
    const keys = expiredPrintJobKeys(
      [
        [printJobKey('cu'), JSON.stringify({ cancel: true, at: now - 11 * 60 * 1000 })],
        [printJobKey('moi'), JSON.stringify({ cancel: true, at: now - 1000 })],
        [printJobKey('hong'), '{'],
        ['theme', 'dark']
      ],
      now
    );
    expect(keys).toEqual([printJobKey('cu'), printJobKey('hong')]);
  });
});
