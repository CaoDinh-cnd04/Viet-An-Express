import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiUrl, toError } from '@/shared/api/http';
import type { TroubleStatus, TroubleTicket } from '@/features/troubles';

/**
 * Trang quản trị Việt An (/admin): token riêng giữ trong sessionStorage, gửi bằng header Authorization —
 * không dùng cookie phiên của khách (đăng nhập admin không đăng xuất tài khoản khách trên cùng trình duyệt).
 */
const TOKEN_KEY = 'va.admin.token';

export function getAdminToken(): string | null {
  try {
    return window.sessionStorage.getItem(TOKEN_KEY);
  } catch {
    return null;
  }
}

function setAdminToken(token: string | null) {
  try {
    if (token) window.sessionStorage.setItem(TOKEN_KEY, token);
    else window.sessionStorage.removeItem(TOKEN_KEY);
  } catch {
    /* storage bị chặn: phải đăng nhập lại mỗi lần tải trang */
  }
}

export async function adminFetch(path: string, init: RequestInit = {}): Promise<Response> {
  const token = getAdminToken();
  const res = await fetch(apiUrl(path), {
    ...init,
    headers: { ...(init.headers ?? {}), ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    credentials: 'omit'
  });
  if (res.status === 401) setAdminToken(null);
  if (!res.ok) {
    const body = (await res.json().catch(() => null)) as { message?: string; error?: string } | null;
    throw toError(res.status, body?.message, body?.error);
  }
  return res;
}

const adminKey = ['admin'] as const;

export function useAdminLogin() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (body: { userName: string; password: string }) => {
      const res = await adminFetch('/admin/login', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
      const json = (await res.json()) as { data: { token: string } };
      setAdminToken(json.data.token);
      return json.data;
    },
    onSuccess: () => void qc.invalidateQueries({ queryKey: adminKey })
  });
}

export function adminLogout() {
  setAdminToken(null);
}

/** Thông báo gửi khách — GET /admin/notifications. */
export interface AdminNotification {
  id: string;
  title: string;
  body: string;
  link?: string | null;
  important: boolean;
  /** 'dd/MM/yyyy HH:mm' */
  createdAt: string;
  createdBy: string;
  emailStatus: 'none' | 'sending' | 'sent' | 'partial' | 'failed' | 'no_recipients';
  emailRecipients: number;
  emailSent: number;
  emailError?: string | null;
  /** Số tài khoản (chính / con) đã đọc. */
  readCount: number;
}

export interface NewNotification {
  title: string;
  body: string;
  /** Trống = không có nút "Xem tại đây". */
  link: string;
  important: boolean;
  sendEmail: boolean;
}

const notificationKey = ['admin', 'notifications'] as const;

export function useAdminNotifications(enabled: boolean) {
  return useQuery({
    queryKey: notificationKey,
    queryFn: async () => ((await (await adminFetch('/admin/notifications')).json()) as { data: AdminNotification[] }).data,
    enabled,
    // Đang gửi email ở nền → tải lại để thấy kết quả.
    refetchInterval: q => (q.state.data?.some(n => n.emailStatus === 'sending') ? 5000 : false)
  });
}

export function useCreateNotification() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (body: NewNotification) =>
      ((await (await adminFetch('/admin/notifications', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
      })).json()) as { message: string }).message,
    onSuccess: () => void qc.invalidateQueries({ queryKey: notificationKey })
  });
}

export function useDeleteNotification() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => adminFetch(`/admin/notifications/${encodeURIComponent(id)}`, { method: 'DELETE' }),
    onSuccess: () => void qc.invalidateQueries({ queryKey: notificationKey })
  });
}

/** Yêu cầu hỗ trợ & sự cố ở trang quản trị — GET /admin/troubles. */
export interface AdminTrouble {
  ticket: TroubleTicket;
  customerCode: string;
  companyName: string;
  isStaff: boolean;
}

const troubleKey = ['admin', 'troubles'] as const;

export function useAdminTroubles(enabled: boolean) {
  return useQuery({
    queryKey: troubleKey,
    queryFn: async () => ((await (await adminFetch('/admin/troubles')).json()) as { data: AdminTrouble[] }).data,
    enabled,
    refetchInterval: 60_000
  });
}

/** Đổi trạng thái và / hoặc ghi chú cho khách — khách nhận thông báo riêng trên portal (ghi chú không lưu ở yêu cầu). */
export function useReplyAdminTrouble() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, reply, status }: { id: string; reply: string; status?: TroubleStatus }) =>
      ((await (await adminFetch(`/admin/troubles/${encodeURIComponent(id)}/reply`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ reply: reply.trim() || null, status: status ?? null })
      })).json()) as { message: string; data: AdminTrouble }),
    onSuccess: () => void qc.invalidateQueries({ queryKey: troubleKey })
  });
}
