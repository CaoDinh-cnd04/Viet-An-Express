import { describe, expect, it } from 'vitest';
import { defaultValues } from '@/features/order-form';
import { completedSteps, stepProgress } from './step-progress';

describe('completedSteps', () => {
  it('bước có lỗi ở khung của mình thì chưa xong, bước khác vẫn xong', () => {
    expect(completedSteps([['receiver', 'city'], ['invoice', 'items', 0, 'hs']], 3)).toEqual([false, true, false]);
    expect(completedSteps([['packages', 0, 'weight']], 3)).toEqual([true, false, true]);
    expect(completedSteps([], 2)).toEqual([true, true]);
  });

  it('thiếu mô tả tổng quan hàng hóa thì bước đầu chưa xong (ô nằm ở khung Thông tin đơn hàng)', () => {
    expect(completedSteps([['goods', 'description']], 3)).toEqual([false, true, true]);
  });
});

describe('stepProgress', () => {
  it('form mới chưa điền thì bước 1 chưa xong', () => {
    expect(stepProgress(defaultValues(), 3)[0]).toBe(false);
  });
});
