import { describe, expect, it } from 'vitest';
import { addTags, MAX_TERMS, parseStatuses, parseTags, pastedValues, serializeTags, toggleStatus, tokenize } from './multi-filter';

describe('trạng thái chọn nhiều', () => {
  it('đọc danh sách, bỏ giá trị lạ, giữ thứ tự chuẩn', () => {
    expect(parseStatuses('ok,wait,abc')).toEqual(['wait', 'ok']);
    expect(parseStatuses('all')).toEqual([]);
  });
  it('bật / tắt', () => {
    expect(toggleStatus('all', 'fly')).toBe('fly');
    expect(toggleStatus('fly', 'wait')).toBe('wait,fly');
    expect(toggleStatus('wait,fly', 'fly')).toBe('wait');
    expect(toggleStatus('wait', 'wait')).toBe('all');
  });
  it('chọn đủ 4 trạng thái = tất cả', () => {
    expect(toggleStatus('wait,fly,nd', 'ok')).toBe('all');
  });
});

describe('thẻ tìm kiếm', () => {
  it('chỉ tách theo dấu phẩy — giữ dấu cách, dấu chấm trong từ khóa', () => {
    expect(tokenize('HONG AN DUONG,  Ms. Uyen ,6010839,,')).toEqual(['HONG AN DUONG', 'Ms. Uyen', '6010839']);
    expect(tokenize('"HONG   AN DUONG", US')).toEqual(['HONG AN DUONG', 'US']);
  });
  it('ghi / đọc URL, mỗi thẻ nhớ trường của nó', () => {
    const all = addTags(addTags([], 'cnee', ['Ms Uyen']), 'ct', ['United States']);
    expect(serializeTags(all)).toBe('cnee:Ms Uyen\nct:United States');
    expect(parseTags(serializeTags(all))).toEqual(all);
  });
  it('link cũ không có tiền tố → dùng trường mặc định', () => {
    expect(parseTags('6010839', 'bill')).toEqual([{ field: 'bill', value: '6010839' }]);
    expect(parseTags('foo:bar')).toEqual([{ field: 'all', value: 'foo:bar' }]);
    expect(parseTags('')).toEqual([]);
  });
  it('bỏ trùng cùng trường (không phân biệt hoa thường), khác trường thì giữ', () => {
    expect(addTags([{ field: 'ct', value: 'US' }], 'ct', ['us', 'UK'])).toHaveLength(2);
    expect(addTags([{ field: 'ct', value: 'US' }], 'all', ['US'])).toHaveLength(2);
  });
  it('giới hạn số thẻ', () => {
    const many = Array.from({ length: MAX_TERMS + 5 }, (_, i) => `B${i}`);
    expect(addTags([], 'bill', many)).toHaveLength(MAX_TERMS);
  });
  it('dán cột từ Excel: mỗi dòng 1 thẻ, giữ khoảng trắng trong tên', () => {
    expect(pastedValues('HONG AN DUONG\r\nMs Uyen\r\n')).toEqual(['HONG AN DUONG', 'Ms Uyen']);
    expect(pastedValues('6010839')).toBeNull();
  });
});
