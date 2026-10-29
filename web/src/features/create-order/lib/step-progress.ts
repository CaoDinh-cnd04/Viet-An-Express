import { createOrderSchema, STEP_FIELDS, type CreateOrderValues } from '@/features/order-form';

/**
 * Bước nào đã điền đủ: bước xong khi không còn lỗi kiểm tra nào thuộc các khung của bước đó
 * (lỗi nằm ở path[0] = 'shipper' / 'receiver' / … theo STEP_FIELDS). Hàm thuần — có test.
 */
export function completedSteps(issuePaths: ReadonlyArray<ReadonlyArray<PropertyKey>>, stepCount: number): boolean[] {
  const bad = new Set(issuePaths.map(p => String(p[0])));
  return STEP_FIELDS.slice(0, stepCount).map(fields => !fields.some(f => bad.has(f)));
}

/** Kiểm tra cả form theo schema rồi suy ra bước nào đã xong. */
export function stepProgress(values: CreateOrderValues, stepCount: number): boolean[] {
  const result = createOrderSchema.safeParse(values);
  return completedSteps(result.success ? [] : result.error.issues.map(i => i.path), stepCount);
}
