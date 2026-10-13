import { describe, expect, it } from 'vitest';
import { hasSentence, toggleSentence } from './report';

describe('toggleSentence', () => {
  it('thêm câu gợi ý vào mô tả trống, có dấu chấm cuối', () => {
    expect(toggleSentence('', 'Tracking không cập nhật nhiều ngày')).toBe('Tracking không cập nhật nhiều ngày.');
  });

  it('nối câu sau phần khách đã gõ, tự thêm dấu chấm còn thiếu', () => {
    expect(toggleSentence('Khách bên Mỹ đang hỏi', 'Nhờ CS hối hãng phát sớm')).toBe('Khách bên Mỹ đang hỏi. Nhờ CS hối hãng phát sớm.');
    expect(toggleSentence('Đã 7 ngày!', 'Nhờ CS hối hãng phát sớm')).toBe('Đã 7 ngày! Nhờ CS hối hãng phát sớm.');
  });

  it('bấm lại câu đã chọn thì bỏ câu đó, giữ phần còn lại', () => {
    const desc = 'Tracking không cập nhật nhiều ngày. Khách đang hỏi. Nhờ CS hối hãng phát sớm.';
    expect(toggleSentence(desc, 'Khách đang hỏi')).toBe('Tracking không cập nhật nhiều ngày. Nhờ CS hối hãng phát sớm.');
  });

  it('hasSentence nhận ra câu đã có', () => {
    expect(hasSentence('Hàng bên trong bị vỡ.', 'Hàng bên trong bị vỡ')).toBe(true);
    expect(hasSentence('Hàng bên trong bị vỡ.', 'Thùng hàng bị móp')).toBe(false);
  });
});
