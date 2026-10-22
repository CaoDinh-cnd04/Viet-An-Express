import { useCallback, useEffect, useRef, useState, type MouseEvent } from 'react';
import { cellRange, inRange, rangeSize, rangeToText, type CellPos, type CellRange } from './cell-range';
import { copyText } from './clipboard';

/** Bấm vào các phần tử này thì không bắt đầu chọn ô (để nút / link / ô tick vẫn dùng bình thường). */
const INTERACTIVE = 'button, a, input, select, textarea, label';

/**
 * Chọn vùng ô trong bảng như Excel:
 * - nhấn giữ chuột rồi kéo sang ô khác (dọc theo cột hoặc ngang) → tô vùng chữ nhật;
 * - Shift + bấm để mở rộng vùng từ ô đầu;
 * - Ctrl/Cmd + C chép vùng (Tab giữa cột, xuống dòng giữa các dòng) — dán thẳng vào Excel;
 * - Esc hoặc bấm ra ngoài bảng để bỏ chọn.
 * Nhấn rồi thả trong cùng 1 ô thì không chọn gì: bấm dòng / bôi chữ trong ô vẫn như cũ.
 */
export function useCellSelection(value: (row: number, col: number) => string, onCopied?: (count: number) => void) {
  const [anchor, setAnchor] = useState<CellPos | null>(null);
  const [focus, setFocus] = useState<CellPos | null>(null);
  const [selecting, setSelecting] = useState(false);
  const pressed = useRef<CellPos | null>(null);
  /** Vừa kéo xong → chặn cú click kế tiếp (không mở chi tiết đơn). */
  const justDragged = useRef(false);
  const tableRef = useRef<HTMLTableElement>(null);

  const range: CellRange | null = anchor && focus ? cellRange(anchor, focus) : null;
  const active = !!range && rangeSize(range) > 1;

  const clear = useCallback(() => {
    setAnchor(null);
    setFocus(null);
  }, []);

  const copy = useCallback(async () => {
    if (!range) return;
    await copyText(rangeToText(range, value));
    onCopied?.(rangeSize(range));
  }, [range, value, onCopied]);

  const cellProps = (row: number, col: number) => ({
    'data-selected': inRange(active ? range : null, row, col) || undefined,
    onMouseDown: (e: MouseEvent) => {
      if (e.button !== 0 || (e.target as HTMLElement).closest(INTERACTIVE)) return;
      if (e.shiftKey && anchor) {
        e.preventDefault();
        setFocus({ row, col });
        justDragged.current = true;
        return;
      }
      pressed.current = { row, col };
      clear();
    },
    onMouseEnter: () => {
      const start = pressed.current;
      if (!start) return;
      if (!selecting && (start.row !== row || start.col !== col)) {
        setSelecting(true);
        setAnchor(start);
        window.getSelection()?.removeAllRanges();
      }
      if (selecting || start.row !== row || start.col !== col) setFocus({ row, col });
    }
  });

  // Thả chuột ở bất kỳ đâu thì kết thúc kéo.
  useEffect(() => {
    const up = () => {
      if (selecting) justDragged.current = true;
      pressed.current = null;
      setSelecting(false);
    };
    window.addEventListener('mouseup', up);
    return () => window.removeEventListener('mouseup', up);
  }, [selecting]);

  // Ctrl+C / Esc / bấm ra ngoài.
  useEffect(() => {
    if (!active) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') clear();
      const hasTextSelection = !!window.getSelection()?.toString();
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'c' && !hasTextSelection) {
        e.preventDefault();
        void copy();
      }
    };
    const onDown = (e: globalThis.MouseEvent) => {
      if (!tableRef.current?.contains(e.target as Node) && !(e.target as HTMLElement).closest('[data-cell-toolbar]')) clear();
    };
    window.addEventListener('keydown', onKey);
    window.addEventListener('mousedown', onDown);
    return () => {
      window.removeEventListener('keydown', onKey);
      window.removeEventListener('mousedown', onDown);
    };
  }, [active, copy, clear]);

  /** Gọi ở onClick của dòng: true = đây là cú click kết thúc thao tác kéo, bỏ qua. */
  const consumeClick = () => {
    const was = justDragged.current;
    justDragged.current = false;
    return was;
  };

  return { tableRef, cellProps, range: active ? range : null, count: active && range ? rangeSize(range) : 0, selecting, copy, clear, consumeClick };
}
