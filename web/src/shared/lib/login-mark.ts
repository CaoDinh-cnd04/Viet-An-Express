/**
 * Mốc lần đăng nhập gần nhất (localStorage, chung mọi tab) — phân biệt "vừa đăng nhập" với "tải lại trang":
 * thông tin chỉ hiện 1 lần sau mỗi lần đăng nhập (vd popup thông báo CRM) nhớ trạng thái theo mốc này.
 */
const KEY = 'va.loginMark';

/** Gọi khi đăng nhập thành công. */
export function markLogin(): void {
  try {
    window.localStorage.setItem(KEY, String(Date.now()));
  } catch {
    /* storage bị chặn: popup hiện lại mỗi lần tải trang */
  }
}

export function loginMark(): string {
  try {
    return window.localStorage.getItem(KEY) ?? '';
  } catch {
    return '';
  }
}
