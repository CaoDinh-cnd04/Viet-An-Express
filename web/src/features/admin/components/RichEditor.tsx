import { useEffect, useRef, useState, type ClipboardEvent, type KeyboardEvent } from 'react';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Icon } from '@/shared/ui';
import { FONT_SIZES, sizeLabel, stepFontSize, TEXT_COLORS } from '@/features/notifications';
import styles from './RichEditor.module.css';

const EDITOR = '[data-rich-editor]';

/** Vùng chọn / con trỏ gần nhất trong 1 ô soạn — bấm nút trên thanh công cụ vẫn định dạng đúng chỗ đang chọn. */
let saved: Range | null = null;
/** Cỡ chữ vừa chọn khi chưa bôi đen (như Word: áp cho chữ gõ tiếp theo) — ô soạn đổi <font size=7> sang px khi gõ. */
let pendingSize: number | null = null;

const editorOf = (node: Node | null) => (node instanceof Element ? node : node?.parentElement)?.closest<HTMLElement>(EDITOR) ?? null;

/** Phạm vi của 1 thanh công cụ: trình soạn / hộp thoại chứa nó (thanh trong popup nội dung chi tiết chỉ định dạng ô trong popup). */
const scopeOf = (el: Element | null): ParentNode => el?.closest('dialog, [role="dialog"]') ?? document;

/**
 * Đưa con trỏ về chỗ đã chọn trong phạm vi; chưa đặt con trỏ lần nào thì đặt cuối ô soạn đầu tiên (như Word: luôn có con trỏ).
 * Trả ô soạn đang định dạng.
 */
function target(scope: ParentNode): HTMLElement | null {
  const sel = window.getSelection();
  if (!sel) return null;
  const current = sel.rangeCount ? sel.getRangeAt(0) : null;
  const live = current && editorOf(current.commonAncestorContainer);
  if (live && scope.contains(live)) return live;

  let editor = saved && editorOf(saved.commonAncestorContainer);
  if (editor && saved && scope.contains(editor)) {
    editor.focus({ preventScroll: true });
    sel.removeAllRanges();
    sel.addRange(saved);
    return editor;
  }
  editor = scope.querySelector<HTMLElement>(EDITOR);
  if (!editor) return null;
  editor.focus({ preventScroll: true });
  const end = document.createRange();
  end.selectNodeContents(editor);
  end.collapse(false);
  sel.removeAllRanges();
  sel.addRange(end);
  saved = end.cloneRange();
  return editor;
}

/** Báo ô soạn đã đổi (execCommand không luôn bắn sự kiện input trên mọi trình duyệt). */
const changed = (editor: HTMLElement | null) => editor?.dispatchEvent(new Event('input', { bubbles: true }));

function exec(scope: ParentNode, command: string, value?: string) {
  const editor = target(scope);
  if (!editor) return;
  document.execCommand('styleWithCSS', false, 'true');
  document.execCommand(command, false, value);
  changed(editor);
}

/**
 * Đổi thẻ <font size=7> (execCommand fontSize chỉ có 7 bậc) sang <span style="font-size: …px"> — giữ nguyên vùng chọn / con trỏ.
 * Trả true nếu có đổi.
 */
function convertFonts(editor: HTMLElement, px: number): boolean {
  const fonts = [...editor.querySelectorAll('font[size="7"]')];
  if (!fonts.length) return false;
  const sel = window.getSelection();
  const range = sel?.rangeCount ? sel.getRangeAt(0) : null;
  const marks = range && { sc: range.startContainer, so: range.startOffset, ec: range.endContainer, eo: range.endOffset };
  const swapped = new Map<Node, Node>();
  for (const font of fonts) {
    const span = document.createElement('span');
    span.style.fontSize = `${px}px`;
    span.append(...font.childNodes);
    font.replaceWith(span);
    swapped.set(font, span);
  }
  if (sel && marks) {
    const r = document.createRange();
    r.setStart(swapped.get(marks.sc) ?? marks.sc, marks.so);
    r.setEnd(swapped.get(marks.ec) ?? marks.ec, marks.eo);
    sel.removeAllRanges();
    sel.addRange(r);
  }
  return true;
}

/** Đổi cỡ chữ: bôi đen → đổi ngay (giữ vùng chọn); chỉ đặt con trỏ → chữ gõ tiếp theo có cỡ mới (như Word). */
function applyFontSize(scope: ParentNode, px: number) {
  const editor = target(scope);
  if (!editor) return;
  pendingSize = px;
  document.execCommand('styleWithCSS', false, 'false');
  document.execCommand('fontSize', false, '7');
  if (convertFonts(editor, px)) changed(editor);
}

/** Cỡ chữ (px) và màu của chữ tại con trỏ / đầu vùng chọn. */
function styleAt(range: Range): { size: number; color: string } {
  const node = range.startContainer;
  const el = node instanceof Element ? node : node.parentElement;
  if (!el) return { size: NaN, color: '' };
  const css = window.getComputedStyle(el);
  return { size: parseFloat(css.fontSize), color: css.color };
}

interface ToolState {
  bold: boolean;
  italic: boolean;
  underline: boolean;
  size: number;
  color: string;
  /** Con trỏ đang ở ô chữ thường (tiêu đề…) — không định dạng được. */
  plain: boolean;
}

/**
 * Dải công cụ định dạng kiểu Word: cỡ chữ (hiện cỡ tại con trỏ, tăng / giảm 1 bậc), in đậm, in nghiêng, gạch chân, màu chữ,
 * xóa định dạng. Bôi đen → áp cho chữ đã chọn; chỉ đặt con trỏ → áp cho chữ gõ tiếp theo. Nút không lấy focus nên con trỏ giữ nguyên.
 */
export function RichToolbar({ className }: { className?: string }) {
  const { t } = useI18n();
  const ref = useRef<HTMLDivElement>(null);
  const [menu, setMenu] = useState<'size' | 'color' | null>(null);
  const [state, setState] = useState<ToolState>({ bold: false, italic: false, underline: false, size: NaN, color: '', plain: false });

  useEffect(() => {
    const onChange = () => {
      const scope = scopeOf(ref.current);
      const active = document.activeElement;
      const plain = !!active && scope.contains(active) && (active instanceof HTMLTextAreaElement || active instanceof HTMLInputElement);
      const sel = window.getSelection();
      const range = sel?.rangeCount ? sel.getRangeAt(0) : null;
      const editor = range && editorOf(range.commonAncestorContainer);
      const inside = !!editor && scope.contains(editor);
      if (inside) saved = range!.cloneRange();
      const at = inside ? styleAt(range!) : saved && scope.contains(saved.commonAncestorContainer) ? styleAt(saved) : null;
      setState({
        plain,
        bold: inside && document.queryCommandState('bold'),
        italic: inside && document.queryCommandState('italic'),
        underline: inside && document.queryCommandState('underline'),
        size: at?.size ?? NaN,
        color: at?.color ?? ''
      });
    };
    document.addEventListener('selectionchange', onChange);
    document.addEventListener('focusin', onChange);
    return () => {
      document.removeEventListener('selectionchange', onChange);
      document.removeEventListener('focusin', onChange);
    };
  }, []);

  // Bấm ra ngoài → đóng menu cỡ chữ / màu.
  useEffect(() => {
    if (!menu) return;
    const close = () => setMenu(null);
    window.addEventListener('mousedown', close);
    return () => window.removeEventListener('mousedown', close);
  }, [menu]);

  const scope = () => scopeOf(ref.current);
  const setSize = (px: number) => {
    applyFontSize(scope(), px);
    setState(s => ({ ...s, size: px }));
  };
  const step = (direction: 1 | -1) => setSize(stepFontSize(Number.isFinite(state.size) ? state.size : 15, direction));
  const toggle = (command: 'bold' | 'italic' | 'underline') => {
    exec(scope(), command);
    setState(s => ({ ...s, [command]: document.queryCommandState(command) }));
  };

  const keep = (e: React.MouseEvent) => e.preventDefault();
  const off = state.plain;
  const tool = (label: string, on: boolean, run: () => void, content: React.ReactNode) => (
    <button type="button" className={cx(styles.tool, on && styles.on)} title={t(label)} aria-label={t(label)} aria-pressed={on}
      onMouseDown={keep} onClick={run} disabled={off}>
      {content}
    </button>
  );

  return (
    <div ref={ref} className={cx(styles.toolbar, className)} role="toolbar" aria-label={t('Định dạng chữ')}>
      <div className={styles.group} onMouseDown={e => e.stopPropagation()}>
        <button type="button" className={cx(styles.select, styles.size)} onMouseDown={keep} onClick={() => setMenu(menu === 'size' ? null : 'size')}
          disabled={off} aria-haspopup="menu" aria-expanded={menu === 'size'} title={t('Cỡ chữ')} aria-label={t('Cỡ chữ')}>
          <span className={styles.sizeValue}>{sizeLabel(state.size) || t('Cỡ')}</span> <Icon name="chevronDown" size={13} />
        </button>
        {menu === 'size' && (
          <div className={cx(styles.menu, styles.sizes)} role="menu">
            {FONT_SIZES.map(px => (
              <button key={px} type="button" role="menuitemradio" aria-checked={Math.round(state.size) === px}
                className={cx(Math.round(state.size) === px && styles.current)}
                onMouseDown={keep} onClick={() => { setSize(px); setMenu(null); }} style={{ fontSize: Math.min(px, 22) }}>
                {px}
              </button>
            ))}
          </div>
        )}
      </div>
      {tool('Tăng cỡ chữ (Ctrl+Shift+>)', false, () => step(1), <span className={styles.grow}>A<small>+</small></span>)}
      {tool('Giảm cỡ chữ (Ctrl+Shift+<)', false, () => step(-1), <span className={styles.shrink}>A<small>−</small></span>)}
      <span className={styles.sep} />
      {tool('In đậm (Ctrl+B)', state.bold, () => toggle('bold'), <b>B</b>)}
      {tool('In nghiêng (Ctrl+I)', state.italic, () => toggle('italic'), <i>I</i>)}
      {tool('Gạch chân (Ctrl+U)', state.underline, () => toggle('underline'), <u>U</u>)}
      <span className={styles.sep} />
      <div className={styles.group} onMouseDown={e => e.stopPropagation()}>
        <button type="button" className={styles.select} onMouseDown={keep} onClick={() => setMenu(menu === 'color' ? null : 'color')} disabled={off}
          aria-haspopup="menu" aria-expanded={menu === 'color'} title={t('Màu chữ')}>
          <span className={styles.colorA} style={state.color ? { textDecorationColor: state.color } : undefined}>A</span> {t('Màu chữ')}{' '}
          <Icon name="chevronDown" size={13} />
        </button>
        {menu === 'color' && (
          <div className={cx(styles.menu, styles.colors)} role="menu">
            {TEXT_COLORS.map(c => (
              <button key={c} type="button" role="menuitem" className={styles.swatch} style={{ background: c }} aria-label={c}
                onMouseDown={keep} onClick={() => { exec(scope(), 'foreColor', c); setState(s => ({ ...s, color: c })); setMenu(null); }} />
            ))}
          </div>
        )}
      </div>
      <span className={styles.sep} />
      {tool('Xóa định dạng', false, () => exec(scope(), 'removeFormat'), <Icon name="close" size={13} />)}
      {off && <span className={styles.hint}>{t('Tiêu đề không định dạng được')}</span>}
    </div>
  );
}

interface RichEditorProps {
  /** HTML ban đầu (ô soạn không điều khiển theo từng phím để không nhảy con trỏ — đổi key để nạp lại). */
  initial: string;
  onChange: (html: string) => void;
  placeholder: string;
  ariaLabel: string;
  className?: string;
  autoFocus?: boolean;
}

/**
 * Ô soạn có định dạng (contentEditable). Ctrl+B / I / U như Word (trình duyệt sẵn có), Ctrl+Shift+> / < tăng / giảm cỡ chữ.
 * Dán chữ từ nơi khác → chỉ lấy chữ thuần; dán ảnh → để trình soạn thêm ảnh vào tin.
 */
export function RichEditor({ initial, onChange, placeholder, ariaLabel, className, autoFocus }: RichEditorProps) {
  const ref = useRef<HTMLDivElement>(null);
  const [empty, setEmpty] = useState(!initial);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    el.innerHTML = initial;
    if (autoFocus) el.focus();
    // Chỉ nạp nội dung ban đầu khi gắn ô soạn.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const onInput = () => {
    const el = ref.current;
    if (!el) return;
    // Chữ gõ sau khi chọn cỡ (chưa bôi đen) nằm trong <font size=7> → đổi sang cỡ px đã chọn.
    if (pendingSize) convertFonts(el, pendingSize);
    setEmpty(!el.textContent?.trim());
    onChange(el.innerHTML);
  };

  const onKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
    if (!(e.ctrlKey || e.metaKey) || !e.shiftKey || (e.key !== '>' && e.key !== '<' && e.key !== '.' && e.key !== ',')) return;
    e.preventDefault();
    const sel = window.getSelection();
    const current = sel?.rangeCount ? styleAt(sel.getRangeAt(0)).size : NaN;
    applyFontSize(scopeOf(e.currentTarget), stepFontSize(Number.isFinite(current) ? current : 15, e.key === '>' || e.key === '.' ? 1 : -1));
  };

  const onPaste = (e: ClipboardEvent<HTMLDivElement>) => {
    if (e.clipboardData.files.length) return;
    e.preventDefault();
    document.execCommand('insertText', false, e.clipboardData.getData('text/plain'));
  };

  return (
    <div
      ref={ref}
      className={cx(styles.editor, empty && styles.empty, className)}
      data-rich-editor=""
      data-placeholder={placeholder}
      contentEditable
      suppressContentEditableWarning
      role="textbox"
      aria-multiline="true"
      aria-label={ariaLabel}
      onInput={onInput}
      onKeyDown={onKeyDown}
      onPaste={onPaste}
    />
  );
}
