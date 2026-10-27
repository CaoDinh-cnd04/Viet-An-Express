import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError, getErrorMessage, http, isNotImplemented, waitForPendingRefresh } from '@/shared/api/http';
import { useToast } from '@/shared/ui';
import { can } from './lib/permissions';
import type { LoginRequest, Session, SessionUser } from './types';

export const sessionKey = ['session'] as const;


/** Phiên hiện tại: GET /auth/session luôn trả 200 (data null = chưa đăng nhập) nên mở trang khi chưa đăng nhập không sinh lỗi 401. */
async function fetchSession(): Promise<Session> {
  try {
    const res = await http.get<{ data: SessionUser | null }>('/auth/session');
    return res.data ? { status: 'authenticated', user: res.data } : { status: 'anonymous' };
  } catch (e) {
    if (e instanceof ApiError && e.status === 401) return { status: 'anonymous' };
    if (isNotImplemented(e)) return { status: 'open' };
    // Dev: backend chưa chạy (proxy trả 5xx / mất mạng) — vẫn cho vào portal để làm giao diện.
    if (import.meta.env.DEV) return { status: 'open' };
    throw e;
  }
}

/** Phiên đăng nhập hiện tại. 401 = chưa đăng nhập; 404/501 = backend chưa bật đăng nhập. */
export function useSession() {
  return useQuery({ queryKey: sessionKey, queryFn: fetchSession, staleTime: 5 * 60_000, retry: false });
}

/** Đăng nhập — backend đặt cookie phiên và trả thông tin khách. Endpoint mới — xem API_CONTRACT.md. */
export function useLogin() {
  const qc = useQueryClient();
  const toast = useToast();
  return useMutation({
    mutationFn: (body: LoginRequest) => http.post<{ data: SessionUser }>('/auth/login', body),
    onSuccess: res => {
      const session: Session = { status: 'authenticated', user: res.data };
      qc.setQueryData(sessionKey, session);
    },
    onError: e =>
      isNotImplemented(e)
        ? toast.show('Chức năng đăng nhập đang được kết nối máy chủ')
        : toast.show(getErrorMessage(e, 'Đăng nhập không thành công'), 'error')
  });
}

/**
 * Đăng xuất: đợi lần làm mới phiên đang chạy xong (tránh response về muộn ghi lại cookie), máy chủ thu hồi token và xoá cookie;
 * sau đó tải lại hẳn trang /login để xoá mọi dữ liệu còn trong bộ nhớ — bấm Back không hiện lại danh sách đơn cũ.
 * `idle` = tự đăng xuất vì không thao tác → trang đăng nhập báo lý do; đăng nhập lại mở `next` (mặc định trang đang xem).
 */
export function useLogout() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (_?: { idle?: boolean; next?: string }) => {
      await waitForPendingRefresh();
      return http.post<{ message: string }>('/auth/logout').catch(() => undefined);
    },
    onSettled: (_data, _error, options) => {
      qc.clear();
      const session: Session = { status: 'anonymous' };
      qc.setQueryData(sessionKey, session);
      const back = options?.next ?? `${window.location.pathname}${window.location.search}`;
      window.location.replace(options?.idle ? `/login?idle=1&next=${encodeURIComponent(back)}` : '/login');
    }
  });
}

/** Kiểm tra quyền của phiên hiện tại: `const allowed = useCan(); allowed(PERMISSIONS.shipmentsCreate)`. */
export function useCan() {
  const session = useSession();
  return (permission?: string) => can(session.data, permission);
}
