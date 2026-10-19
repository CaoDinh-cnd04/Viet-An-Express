import { account } from './account';
import { categories } from './categories';
import { common } from './common';
import { createOrder } from './create-order';
import { drafts } from './drafts';
import { ecommerce } from './ecommerce';
import { landing } from './landing';
import { orders } from './orders';
import { pricing } from './pricing';
import { feedback } from './feedback';
import { support } from './support';
import { start } from './start';
import { mytracking } from './mytracking';
import { staff } from './staff';
import { troubles } from './troubles';

/**
 * Từ điển tiếng Anh: khoá = câu tiếng Việt gốc trong code, giá trị = bản tiếng Anh.
 * Khoá có {biến} dịch được cả câu đã điền số. Chia theo khu vực cho dễ tìm; test i18n kiểm tra không thiếu câu nào.
 */
export const EN: Readonly<Record<string, string>> = {
  ...common,
  ...start,
  ...createOrder,
  ...drafts,
  ...orders,
  ...account,
  ...mytracking,
  ...staff,
  ...support,
  ...pricing,
  ...ecommerce,
  ...landing,
  ...categories,
  ...feedback,
  ...troubles
};
