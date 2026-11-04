import { describe, expect, it } from 'vitest';
import { fillOtp } from './otp';

const empty = ['', '', '', '', '', ''];

describe('fillOtp', () => {
  it('gõ 1 số thì điền ô đó và sang ô sau', () => {
    expect(fillOtp(empty, 2, '7')).toEqual({ digits: ['', '', '7', '', '', ''], focus: 3 });
  });

  it('dán đủ mã ở ô bất kỳ thì điền lại từ ô đầu, bỏ khoảng trắng và chữ', () => {
    expect(fillOtp(['9', '9', '', '', '', ''], 3, 'Mã: 123 456')).toEqual({ digits: ['1', '2', '3', '4', '5', '6'], focus: 5 });
  });

  it('dán thiếu số thì điền từ ô đang đứng', () => {
    expect(fillOtp(empty, 1, '12')).toEqual({ digits: ['', '1', '2', '', '', ''], focus: 3 });
  });

  it('không có chữ số thì giữ nguyên', () => {
    expect(fillOtp(empty, 0, 'abc')).toEqual({ digits: empty, focus: 0 });
  });
});
