import {
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  type CSSProperties,
} from "react";
import { createPortal } from "react-dom";
import { useI18n } from "@/shared/i18n";
import { cx } from "@/shared/lib/cx";
import { Icon } from "@/shared/ui";
import type { Category } from "../api";
import { MULTI_CATEGORY } from "@/features/order-form";
import { groupCategories } from "../lib/category-picker";
import styles from "./CategoryPicker.module.css";

interface CategoryPickerProps {
  value: string;
  onChange: (name: string) => void;
  onBlur?: () => void;
  categories: readonly Category[];
  /** Bấm ngôi sao cạnh nhóm: đánh dấu / bỏ yêu thích. */
  onToggleFavorite: (c: Category) => void;
  invalid?: boolean;
  ariaLabel: string;
  title?: string;
}

const PANEL_MAX_H = 340;
const PANEL_MIN_W = 280;

/**
 * Ô chọn nhóm hàng hóa (thay cho <select>): danh sách xổ có ô tìm, mục "Nhóm yêu thích" ở đầu
 * và ngôi sao trên từng nhóm để khách đánh dấu yêu thích ngay khi chọn.
 * Danh sách vẽ ra ngoài bảng (portal, position: fixed) để không bị khung cuộn của bảng che.
 */
export function CategoryPicker({
  value,
  onChange,
  onBlur,
  categories,
  onToggleFavorite,
  invalid,
  ariaLabel,
  title,
}: CategoryPickerProps) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [pos, setPos] = useState<CSSProperties>({});
  const triggerRef = useRef<HTMLButtonElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const { favorites, others } = groupCategories(categories, query, t);
  // Nhóm đang chọn mà không còn trong danh sách (đã xóa / đổi tên) vẫn hiện để không mất dữ liệu.
  const missing =
    value &&
    value !== MULTI_CATEGORY &&
    !categories.some((c) => c.name === value)
      ? value
      : "";
  const showMulti =
    !query || t(MULTI_CATEGORY).toLowerCase().includes(query.toLowerCase());

  const close = (focusTrigger = false) => {
    setOpen(false);
    setQuery("");
    onBlur?.();
    if (focusTrigger) triggerRef.current?.focus();
  };

  const pick = (name: string) => {
    onChange(name);
    close(true);
  };

  // Đặt danh sách ngay dưới ô (hoặc phía trên nếu sát đáy màn hình)
  useLayoutEffect(() => {
    if (!open || !triggerRef.current) return;
    const r = triggerRef.current.getBoundingClientRect();
    const width = Math.max(r.width, PANEL_MIN_W);
    const left = Math.min(r.left, window.innerWidth - width - 8);
    const below = window.innerHeight - r.bottom;
    setPos(
      below >= Math.min(PANEL_MAX_H, 240) || below >= r.top
        ? {
            top: r.bottom + 4,
            left,
            width,
            maxHeight: Math.min(PANEL_MAX_H, below - 12),
          }
        : {
            bottom: window.innerHeight - r.top + 4,
            left,
            width,
            maxHeight: Math.min(PANEL_MAX_H, r.top - 12),
          },
    );
  }, [open]);

  // Bấm ra ngoài / cuộn trang / đổi cỡ cửa sổ → đóng
  useEffect(() => {
    if (!open) return;
    const outside = (e: Event) => {
      const target = e.target as Node;
      if (
        panelRef.current?.contains(target) ||
        triggerRef.current?.contains(target)
      )
        return;
      close();
    };
    const onResize = () => close();
    document.addEventListener("mousedown", outside);
    document.addEventListener("scroll", outside, true);
    window.addEventListener("resize", onResize);
    return () => {
      document.removeEventListener("mousedown", outside);
      document.removeEventListener("scroll", outside, true);
      window.removeEventListener("resize", onResize);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const row = (c: Category) => (
    <li key={c.id} className={cx(styles.row, c.name === value && styles.rowOn)}>
      <button
        type="button"
        className={styles.star}
        aria-pressed={c.isFavorite}
        aria-label={t(c.isFavorite ? "Bỏ yêu thích" : "Đánh dấu yêu thích")}
        title={t(
          c.isFavorite
            ? "Bỏ yêu thích"
            : "Đánh dấu yêu thích — nhóm hiện lên đầu danh sách",
        )}
        onClick={() => onToggleFavorite(c)}
      >
        <Icon
          name="star"
          size={15}
          className={c.isFavorite ? styles.starOn : undefined}
        />
      </button>
      <button
        type="button"
        role="option"
        aria-selected={c.name === value}
        className={styles.option}
        onClick={() => pick(c.name)}
      >
        {t(c.name)}
      </button>
    </li>
  );

  return (
    <>
      <button
        ref={triggerRef}
        type="button"
        className={cx(
          styles.trigger,
          invalid && styles.invalid,
          !value && styles.placeholder,
        )}
        aria-label={ariaLabel}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-invalid={invalid || undefined}
        title={title}
        onClick={() => (open ? close() : setOpen(true))}
        onKeyDown={(e) => {
          if (
            !open &&
            (e.key === "ArrowDown" || e.key === "Enter" || e.key === " ")
          ) {
            e.preventDefault();
            setOpen(true);
          }
        }}
      >
        <span className={styles.value}>
          {value ? t(value) : t("Chọn nhóm hàng")}
        </span>
        <Icon name="chevronDown" size={14} />
      </button>

      {open &&
        createPortal(
          <div
            ref={panelRef}
            className={styles.panel}
            style={pos}
            onKeyDown={(e) => {
              if (e.key === "Escape") {
                e.stopPropagation();
                close(true);
              }
            }}
          >
            <div className={styles.searchBox}>
              <Icon name="search" size={15} />
              <input
                autoFocus
                className={styles.search}
                placeholder={t("Tìm nhóm hàng…")}
                aria-label={t("Tìm nhóm hàng")}
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter") {
                    e.preventDefault();
                    const first = favorites[0] ?? others[0];
                    if (first) pick(first.name);
                  }
                }}
              />
            </div>
            <ul className={styles.list} role="listbox" aria-label={ariaLabel}>
              {showMulti && (
                <li
                  className={cx(
                    styles.row,
                    value === MULTI_CATEGORY && styles.rowOn,
                  )}
                >
                  <button
                    type="button"
                    role="option"
                    aria-selected={value === MULTI_CATEGORY}
                    className={cx(styles.option, styles.indent)}
                    onClick={() => pick(MULTI_CATEGORY)}
                  >
                    {t(MULTI_CATEGORY)}
                  </button>
                </li>
              )}
              {favorites.length > 0 && (
                <>
                  <li className={styles.group} role="presentation">
                    {t("Nhóm yêu thích")}
                  </li>
                  {favorites.map(row)}
                </>
              )}
              {(others.length > 0 || missing) && (
                <>
                  <li className={styles.group} role="presentation">
                    {t(favorites.length ? "Nhóm khác" : "Nhóm hàng hóa")}
                  </li>
                  {others.map(row)}
                  {missing && !query && (
                    <li className={cx(styles.row, styles.rowOn)}>
                      <button
                        type="button"
                        role="option"
                        aria-selected
                        className={cx(styles.option, styles.indent)}
                        onClick={() => pick(missing)}
                      >
                        {missing}
                      </button>
                    </li>
                  )}
                </>
              )}
              {!favorites.length && !others.length && !showMulti && (
                <li className={styles.empty}>{t("Không có nhóm phù hợp.")}</li>
              )}
            </ul>
          </div>,
          document.body,
        )}
    </>
  );
}
