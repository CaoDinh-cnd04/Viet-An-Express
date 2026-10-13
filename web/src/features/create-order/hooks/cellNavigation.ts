import type { ChangeEvent, KeyboardEvent } from 'react';
import { arrowMove, sanitizeDecimal } from '../lib/cell-nav';

const FIELDS = 'input:not([type=hidden]):not([type=checkbox]):not([type=radio]):not(:disabled), select:not(:disabled), textarea:not(:disabled)';

/**
 * Gắn vào onKeyDown của bảng: ← → chuyển giữa các ô theo vị trí con trỏ (xem arrowMove).
 * Sang ô sau thì con trỏ ở đầu ô, về ô trước thì ở cuối ô — nhấn tiếp vẫn đi liền mạch. Ô chọn (select): ← → luôn chuyển ô.
 */
export function onCellArrows(e: KeyboardEvent<HTMLElement>) {
  if ((e.key !== 'ArrowLeft' && e.key !== 'ArrowRight') || e.altKey || e.ctrlKey || e.metaKey || e.shiftKey) return;
  const el = e.target;
  let move;
  if (el instanceof HTMLSelectElement) move = e.key === 'ArrowRight' ? 'next' : 'prev';
  else if (el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement) move = arrowMove(e.key, el.selectionStart, el.selectionEnd, el.value.length);
  if (!move) return;

  const fields = [...e.currentTarget.querySelectorAll<HTMLElement>(FIELDS)].filter(f => f.offsetParent !== null);
  const target = fields[fields.indexOf(el as HTMLElement) + (move === 'next' ? 1 : -1)];
  if (!target) return;
  e.preventDefault();
  target.focus();
  if (target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement) {
    const pos = move === 'next' ? 0 : target.value.length;
    target.setSelectionRange(pos, pos);
  }
}

/**
 * Ô số dạng chữ có bàn phím số: trình duyệt không cho biết vị trí con trỏ trong ô type="number", nên ô số trong bảng dùng
 * type="text" + inputMode; gõ / dán thì bỏ ký tự không phải số trước khi react-hook-form nhận giá trị.
 */
export function decimalField<P extends { onChange: (e: ChangeEvent<HTMLInputElement>) => unknown }>(props: P, integer = false, maxDecimals?: number) {
  return {
    ...props,
    type: 'text',
    inputMode: integer ? ('numeric' as const) : ('decimal' as const),
    autoComplete: 'off',
    onChange: (e: ChangeEvent<HTMLInputElement>) => {
      const clean = sanitizeDecimal(e.target.value, integer, maxDecimals);
      if (clean !== e.target.value) e.target.value = clean;
      return props.onChange(e);
    }
  };
}
