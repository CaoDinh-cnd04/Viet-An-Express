import { describe, expect, it } from 'vitest';
import { defaultValues } from '../schema';
import { completedSteps, stepProgress } from './step-progress';

describe('completedSteps', () => {
  it('bước có lỗi ở khung của mình thì chưa xong, bước khác vẫn xong', () => {
    expect(completedSteps([['receiver', 'city'], ['invoice', 'items', 0, 'hs']], 3)).toEqual([false, true, false]);
    expect(completedSteps([['packages', 0, 'weight']], 3)).toEqual([true, false, true]);
    expect(completedSteps([], 2)).toEqual([true, true]);
  });
});

describe('stepProgress', () => {
  it('form mới chưa điền thì bước 1 chưa xong', () => {
    expect(stepProgress(defaultValues(), 3)[0]).toBe(false);
  });
});
