import { describe, expect, it } from 'vitest';
import { popupSignature } from './popup';

describe('popupSignature', () => {
  it('cùng nội dung cùng dấu, sửa nội dung thì đổi dấu', () => {
    expect(popupSignature('<p>Nghỉ lễ 2/9</p>')).toBe(popupSignature('<p>Nghỉ lễ 2/9</p>'));
    expect(popupSignature('<p>Nghỉ lễ 2/9</p>')).not.toBe(popupSignature('<p>Nghỉ lễ 1/9</p>'));
    expect(popupSignature('')).toBe('0.' + (0x811c9dc5).toString(36));
  });
});
