import { describe, expect, it } from 'vitest';
import { classicDate, classicPlace, classicStatus, groupClassic, splitClassic } from './classic';

describe('splitClassic / classicDate', () => {
  it('đổi ngày Việt Nam / ISO sang yyyy-MM-dd', () => {
    expect(splitClassic('10/10/2026 11:25')).toEqual({ date: '2026-10-10', time: '11:25' });
    expect(splitClassic('7/10/2026')).toEqual({ date: '2026-10-07', time: '' });
    expect(splitClassic('2026-10-08T15:16:00')).toEqual({ date: '2026-10-08', time: '15:16' });
    expect(classicDate('07/10/2026')).toBe('2026-10-07');
    expect(classicDate(undefined)).toBe('');
  });
});

describe('classicPlace', () => {
  it('nối bằng " - "', () => {
    expect(classicPlace('TARINGA, Australia')).toBe('TARINGA - Australia');
    expect(classicPlace('Australia')).toBe('Australia');
    expect(classicPlace(undefined)).toBe('');
  });
});

describe('groupClassic', () => {
  it('gom theo ngày, giữ thứ tự mới nhất trước', () => {
    expect(groupClassic([
      { time: '10/10/2026 11:25', title: 'Information submitted to local courier', location: 'Australia' },
      { time: '10/10/2026 08:32', title: 'Processed in Warehouse', location: 'Australia' },
      { time: '09/10/2026 16:59', title: 'Processed in Warehouse' }
    ])).toEqual([
      { date: '2026-10-10', items: [
        { time: '11:25', title: 'Information submitted to local courier', location: 'Australia' },
        { time: '08:32', title: 'Processed in Warehouse', location: 'Australia' }
      ] },
      { date: '2026-10-09', items: [{ time: '16:59', title: 'Processed in Warehouse', location: '' }] }
    ]);
  });
});

describe('classicStatus', () => {
  it('trạng thái mới nhất kèm ngày giờ', () => {
    expect(classicStatus({ time: '10/10/2026 11:25', title: 'Information submitted to local courier' }, 'x'))
      .toBe('Information submitted to local courier (2026-10-10 11:25)');
    expect(classicStatus(undefined, 'Updating')).toBe('Updating');
  });
});
