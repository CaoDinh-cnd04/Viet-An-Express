/** Cuộn mượt tới khung có id (mục lục bên trái). */
export const scrollToId = (id: string) => document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });

/** Cuộn tới ô lỗi đầu tiên sau khi kiểm tra không đạt — đợi khung đang thu gọn (vd người gửi) tự mở ra trước. */
export const focusFirstError = () =>
  window.setTimeout(() => {
    const el = [...document.querySelectorAll<HTMLElement>('[aria-invalid="true"]')].find(e => e.offsetParent !== null);
    el?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    el?.focus({ preventScroll: true });
  }, 120);
