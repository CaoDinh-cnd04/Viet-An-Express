import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError, getErrorMessage, http, isNotImplemented, waitForPendingRefresh } from '@/shared/api/http';
import { markLogin } from '@/shared/lib/login-mark';
import { useToast } from '@/shared/ui';
import { can } from './lib/permissions';
import type { ActableCustomer, LoginRequest, OtpChallenge, OtpKind, Session, SessionUser } from './types';

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

/** Bật OTP khách: /auth/login trả thử thách (otpRequired) thay cho phiên. */
export const isOtpChallenge = (data: SessionUser | OtpChallenge): data is OtpChallenge => 'challengeId' in data;

/**
 * Đăng nhập khách — backend đặt cookie phiên và trả thông tin khách; khi bật OTP khách (CustomerOtp:Mode) thì trả thử thách,
 * chưa có phiên — nhập mã ở bước 2 (useVerifyOtp('customer')). Xem API_CONTRACT.md.
 */
export function useLogin() {
  const qc = useQueryClient();
  const toast = useToast();
  return useMutation({
    mutationFn: (body: LoginRequest) => http.post<{ data: SessionUser | OtpChallenge }>('/auth/login', body).then(r => r.data),
    onSuccess: data => {
      if (isOtpChallenge(data)) return;
      markLogin();
      const session: Session = { status: 'authenticated', user: data };
      qc.setQueryData(sessionKey, session);
    },
    onError: e =>
      isNotImplemented(e)
        ? toast.show('Chức năng đăng nhập đang được kết nối máy chủ')
        : toast.show(getErrorMessage(e, 'Đăng nhập không thành công'), 'error')
  });
}

/** Nhân viên Việt An đăng nhập bằng tài khoản dbo.USERS — bước 1: đúng mật khẩu thì backend gửi mã OTP tới email, chưa cấp phiên. */
export function useVietAnLogin() {
  const toast = useToast();
  return useMutation({
    mutationFn: (body: LoginRequest) =>
      http
        .post<{ data: OtpChallenge }>('/auth/vietan/login', { userName: body.username, password: body.password, remember: body.remember })
        .then(r => r.data),
    onError: e => toast.show(getErrorMessage(e, 'Đăng nhập không thành công'), 'error')
  });
}

/** OTP khách lần đầu: email nhận mã + đồng ý xử lý dữ liệu cá nhân → backend gửi mã xác nhận tới email đó. */
export function useSetOtpEmail() {
  const toast = useToast();
  return useMutation({
    mutationFn: (body: { challengeId: string; email: string; acceptTerms: boolean }) =>
      http.post<{ data: OtpChallenge }>('/auth/otp-email', { ...body, termsVersion: 'BVDLCN-2026.11' }).then(r => r.data),
    onError: e => toast.show(getErrorMessage(e, 'Không gửi được mã xác nhận'), 'error')
  });
}

/** Bước 2: nhập đúng mã OTP → backend đặt cookie phiên (nhân viên: chưa gắn khách, chọn khách ở /chon-khach). */
export function useVerifyOtp(kind: OtpKind) {
  const qc = useQueryClient();
  const toast = useToast();
  return useMutation({
    mutationFn: (body: { challengeId: string; code: string }) =>
      http.post<{ data: SessionUser }>(kind === 'vietan' ? '/auth/vietan/verify-otp' : '/auth/verify-otp', body),
    onSuccess: res => {
      markLogin();
      const session: Session = { status: 'authenticated', user: res.data };
      qc.setQueryData(sessionKey, session);
    },
    onError: e => toast.show(getErrorMessage(e, 'Mã OTP không đúng'), 'error')
  });
}

/** Khách nhân viên Việt An được làm thay, tìm theo mã / tên / SĐT / tên đăng nhập. */
export function useActableCustomers(query: string) {
  return useQuery({
    queryKey: ['vietan-customers', query],
    queryFn: () => http.get<{ data: ActableCustomer[] }>('/auth/vietan/customers', { q: query }).then(r => r.data),
    staleTime: 60_000
  });
}

/**
 * Chọn / đổi khách: backend cấp lại cookie phiên gắn khách đó. Xóa mọi dữ liệu đã tải của khách trước (đơn, nháp, sổ địa chỉ…)
 * để không lẫn sang khách mới.
 */
export function useActAsCustomer() {
  const qc = useQueryClient();
  const toast = useToast();
  return useMutation({
    mutationFn: (customerId: number) => http.post<{ data: SessionUser }>('/auth/vietan/act-as', { customerId }),
    onSuccess: res => {
      qc.clear();
      const session: Session = { status: 'authenticated', user: res.data };
      qc.setQueryData(sessionKey, session);
    },
    onError: e => toast.show(getErrorMessage(e, 'Không chọn được khách'), 'error')
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
