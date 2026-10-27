import { describe, expect, it } from 'vitest';
import { initialsOf, loginPath, PORTAL_HOME, safeNextPath } from './session';

describe('safeNextPath', () => {
  it('giữ đường dẫn nội bộ kèm query', () => {
    expect(safeNextPath('/orders?status=new')).toBe('/orders?status=new');
  });
  it('trống hoặc không hợp lệ thì về trang chủ portal', () => {
    expect(safeNextPath(null)).toBe(PORTAL_HOME);
    expect(safeNextPath('')).toBe(PORTAL_HOME);
    expect(safeNextPath('orders')).toBe(PORTAL_HOME);
  });
  it('chặn chuyển hướng ra ngoài', () => {
    expect(safeNextPath('https://evil.com')).toBe(PORTAL_HOME);
    expect(safeNextPath('//evil.com')).toBe(PORTAL_HOME);
    expect(safeNextPath('/\\evil.com')).toBe(PORTAL_HOME);
  });
  it('không quay lại trang ngoài', () => {
    expect(safeNextPath('/')).toBe(PORTAL_HOME);
    expect(safeNextPath('/login?next=/orders')).toBe(PORTAL_HOME);
  });
});

describe('loginPath', () => {
  it('mã hóa trang cần quay lại', () => {
    expect(loginPath('/orders?status=new')).toBe('/login?next=%2Forders%3Fstatus%3Dnew');
  });
  it('bỏ next khi là trang mặc định hoặc trang ngoài', () => {
    expect(loginPath(PORTAL_HOME)).toBe('/login');
    expect(loginPath('/')).toBe('/login');
    expect(loginPath('/orders')).toBe('/login?next=%2Forders');
    expect(loginPath()).toBe('/login');
  });
});

describe('initialsOf', () => {
  it('bỏ tiền tố loại hình công ty', () => {
    expect(initialsOf('Công ty TNHH ABC')).toBe('AB');
    expect(initialsOf('Công ty cổ phần Hải Nam')).toBe('HN');
    expect(initialsOf('CTY TNHH MTV Thương Mại Sao Việt')).toBe('TV');
  });
  it('bỏ đuôi pháp lý tiếng Anh', () => {
    expect(initialsOf('SCS CO., LTD')).toBe('SC');
    expect(initialsOf('SGB EXPRESS HN')).toBe('SH');
    expect(initialsOf('Linex Trading JSC')).toBe('LT');
    expect(initialsOf('Acme Company Limited')).toBe('AC');
  });
  it('tên thường', () => {
    expect(initialsOf('nguyễn văn an')).toBe('NA');
    expect(initialsOf('')).toBe('');
    expect(initialsOf(undefined)).toBe('');
  });
});
