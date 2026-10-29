import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiUrl, toError } from '@/shared/api/http';
import type { NotificationItem } from '@/features/notifications';
import type { SupportStatus, SupportTicket } from '@/features/support';

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

/** Thu hồi token ở máy chủ (chép được token cũng không dùng tiếp được) rồi xóa khỏi trình duyệt. */
export function adminLogout() {
  if (getAdminToken()) void adminFetch('/admin/logout', { method: 'POST' }).catch(() => undefined);
  setAdminToken(null);
}

/** Thông báo quản trị đăng — GET /admin/notifications. */
export interface AdminNotification {
  id: string;
  title: string;
  status: 'draft' | 'scheduled' | 'live' | 'expired' | 'removed';
  important: boolean;
  pinned: boolean;
  /** 'yyyy-MM-ddTHH:mm' giờ Việt Nam — giờ đăng (hẹn giờ). */
  publishAt: string;
  /** 'yyyy-MM-dd' — hiện tới hết ngày này; null = không hết hạn. */
  expiresOn?: string | null;
  /** 'dd/MM/yyyy HH:mm' */
  createdAt: string;
  createdBy: string;
  audience: NotificationAudienceKind;
  /** Loại khách (TCustomer.CustomerTypeID) khi audience = type. */
  customerTypes: number[];
  /** Mã khách khi audience = pick. */
  customerCodes: string[];
  /** Số khách nhận (có tài khoản portal). */
  recipients: number;
  /** Số khách đã đọc (ít nhất 1 tài khoản) / số tài khoản đã đọc. */
  readCustomers: number;
  readCount: number;
  emailStatus: 'none' | 'scheduled' | 'sending' | 'sent' | 'partial' | 'failed' | 'no_recipients';
  emailRecipients: number;
  emailSent: number;
  emailError?: string | null;
  items: NotificationItem[];
}

export type NotificationAudienceKind = 'all' | 'type' | 'pick';

/** Số khách có tài khoản portal theo loại — GET /admin/notifications/audience. */
export interface NotificationAudience {
  total: number;
  types: Array<{ id: number; name: string; count: number }>;
}

export interface NotificationReader {
  customerCode: string;
  companyName: string;
  /** Số tài khoản (chính / con) của khách đã đọc. */
  accounts: number;
  /** 'dd/MM/yyyy HH:mm' — lần đọc đầu. */
  readAt: string;
}

/** POST / PUT /admin/notifications. */
export interface SaveNotificationBody {
  title: string;
  /** body / detail: chữ thường hoặc HTML có định dạng; chỉ 1 trong link / detail. */
  items: Array<{ title: string | null; body: string; images: string[]; link: string | null; detail: string | null }>;
  important: boolean;
  pinned: boolean;
  sendEmail: boolean;
  audience: NotificationAudienceKind;
  customerTypes: number[];
  customerCodes: string[];
  /** 'yyyy-MM-ddTHH:mm' — null = đăng ngay (hoặc giữ giờ đăng cũ khi sửa thông báo đang hiện). */
  publishAt: string | null;
  expiresOn: string | null;
  /** false = lưu nháp. */
  publish: boolean;
}

const notificationKey = ['admin', 'notifications'] as const;

const json = async <T,>(res: Response) => (await res.json()) as T;

export function useAdminNotifications(enabled: boolean) {
  return useQuery({
    queryKey: notificationKey,
    queryFn: async () => (await json<{ data: AdminNotification[] }>(await adminFetch('/admin/notifications'))).data,
    enabled,
    // Đang gửi email ở nền → tải lại để thấy kết quả.
    refetchInterval: q => (q.state.data?.some(n => n.emailStatus === 'sending') ? 5000 : false)
  });
}

export function useNotificationAudience() {
  return useQuery({
    queryKey: [...notificationKey, 'audience'],
    queryFn: async () => (await json<{ data: NotificationAudience }>(await adminFetch('/admin/notifications/audience'))).data,
    staleTime: 5 * 60_000
  });
}

export function useNotificationReaders(id: string | null) {
  return useQuery({
    queryKey: [...notificationKey, 'readers', id],
    queryFn: async () => (await json<{ data: NotificationReader[] }>(await adminFetch(`/admin/notifications/${encodeURIComponent(id!)}/readers`))).data,
    enabled: !!id
  });
}

export function useSaveNotification() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, body }: { id: string | null; body: SaveNotificationBody }) =>
      json<{ data: AdminNotification; message: string }>(
        await adminFetch(id ? `/admin/notifications/${encodeURIComponent(id)}` : '/admin/notifications', {
          method: id ? 'PUT' : 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(body)
        })
      ),
    onSuccess: () => void qc.invalidateQueries({ queryKey: notificationKey })
  });
}

export function useRemoveNotification() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => adminFetch(`/admin/notifications/${encodeURIComponent(id)}/remove`, { method: 'POST' }),
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

/** POST /admin/notifications/test-email — gửi thử email của nội dung đang soạn (không lưu); trả số địa chỉ đã gửi. */
export function useTestNotificationEmail() {
  return useMutation({
    mutationFn: async (body: { to: string } & Pick<SaveNotificationBody, 'title' | 'items' | 'important'>) =>
      json<{ data: number; message: string }>(
        await adminFetch('/admin/notifications/test-email', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(body)
        })
      )
  });
}

/** Tải 1 ảnh lên → tên tệp để gửi trong items[].images. */
export async function uploadNotificationImage(file: Blob, fileName: string): Promise<string> {
  const form = new FormData();
  form.append('file', file, fileName);
  return (await json<{ data: { name: string } }>(await adminFetch('/admin/notifications/images', { method: 'POST', body: form }))).data.name;
}

/** Yêu cầu hỗ trợ & sự cố ở trang quản trị — GET /admin/troubles. */
export interface AdminSupportTicket {
  ticket: SupportTicket;
  customerCode: string;
  companyName: string;
  isStaff: boolean;
  /** Nhân viên Việt An phụ trách khách (TCustomer.Staff_ID → USERS) — không có khi chưa phân công. */
  managerId?: number | null;
  managerName?: string | null;
  /** Email người gửi nhập — lời nhắn từ trang chủ chỉ liên hệ lại được qua email / SĐT. */
  email?: string | null;
}

const supportKey = ['admin', 'troubles'] as const;

export function useAdminSupportTickets(enabled: boolean) {
  return useQuery({
    queryKey: supportKey,
    queryFn: async () => ((await (await adminFetch('/admin/troubles')).json()) as { data: AdminSupportTicket[] }).data,
    enabled,
    refetchInterval: 60_000
  });
}

/**
 * Trả lời (nối vào TContact.Content) và / hoặc đổi trạng thái — không gửi thông báo, khách xem ở trang Hỗ trợ & góp ý;
 * "Chờ khách phản hồi" thì khách trả lời lại được.
 */
export function useReplyAdminSupportTicket() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, reply, status }: { id: string; reply: string; status?: SupportStatus }) =>
      ((await (await adminFetch(`/admin/troubles/${encodeURIComponent(id)}/reply`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ reply: reply.trim() || null, status: status ?? null })
      })).json()) as { message: string; data: AdminSupportTicket }),
    onSuccess: () => void qc.invalidateQueries({ queryKey: supportKey })
  });
}
