/**
 * HTTP client dùng chung cho toàn bộ feature.
 * - Base URL: VITE_API_BASE_URL (mặc định /api/v1; dev được Vite proxy sang backend). Bản đưa lên máy chủ đặt địa chỉ đầy đủ
 *   khi backend ở tên miền riêng, vd http://api.vietanexpress.com.vn/api/v1 (publish-ftp.ps1 truyền vào lúc build).
 * - Lỗi HTTP được chuẩn hóa thành ApiError để UI hiển thị thống nhất.
 * - Hợp đồng API từng endpoint: xem web/docs/API_CONTRACT.md.
 */

import { isPendingEndpoint } from './pending';

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api/v1';


export class ApiError extends Error {
  /** Backend chưa có endpoint: 404 / 501 không kèm message (lỗi 404 có message như "Không tìm thấy đơn" là lỗi thật). */
  readonly notImplemented: boolean;

  constructor(
    message: string,
    readonly status: number,
    readonly code?: string,
    notImplemented = false
  ) {
    super(message);
    this.name = 'ApiError';
    this.notImplemented = notImplemented;
  }
}

/** Thông báo lỗi thân thiện để hiện toast. */
export const getErrorMessage = (e: unknown, fallback = 'Có lỗi xảy ra, vui lòng thử lại'): string =>
  e instanceof ApiError ? e.message : fallback;

/** Endpoint backend chưa làm — UI hiện trạng thái "đang cập nhật" thay vì báo lỗi. */
export const isNotImplemented = (e: unknown): boolean => e instanceof ApiError && e.notImplemented;

/** Câu báo khi backend chưa có endpoint (404 / 501 không kèm message — vd route chưa làm). */
export const NOT_READY_MESSAGE = 'Chức năng đang được kết nối máy chủ, vui lòng thử lại sau';

/** Phản hồi lỗi → ApiError: ưu tiên message của backend; 404 / 501 không có message = chức năng chưa có. */
export function toError(status: number, message?: string, code?: string): ApiError {
  if (message) return new ApiError(message, status, code);
  if (status === 404 || status === 501) return new ApiError(NOT_READY_MESSAGE, status, code, true);
  return new ApiError('Lỗi máy chủ ({status})'.replace('{status}', String(status)), status, code);
}

export type QueryParams = Record<string, string | number | boolean | null | undefined>;

/** URL đầy đủ của 1 API (vd làm src cho ảnh, hoặc gọi fetch riêng). */
export const apiUrl = (path: string, params?: QueryParams): string => buildUrl(path, params);

function buildUrl(path: string, params?: QueryParams): string {
  return resolveApiUrl(BASE_URL, path, params, window.location.origin);
}

/**
 * URL gọi API: cùng tên miền với trang → đường dẫn tương đối ("/api/v1/orders?page=2");
 * backend ở tên miền khác (base là địa chỉ đầy đủ) → giữ nguyên tên miền đó. Hàm thuần — có test.
 */
export function resolveApiUrl(base: string, path: string, params: QueryParams | undefined, origin: string): string {
  const url = new URL(base + path, origin);
  Object.entries(params ?? {}).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') url.searchParams.set(key, String(value));
  });
  return url.origin === new URL(origin).origin ? url.pathname + url.search : url.href;
}

interface RequestOptions {
  params?: QueryParams;
  body?: unknown;
}

/** Đang làm mới phiên — nhiều request cùng gặp 401 thì chỉ gọi /auth/refresh một lần. */
let refreshing: Promise<boolean> | null = null;

/** Access token (cookie) sống ngắn; hết hạn thì đổi refresh token lấy phiên mới. Trả false nếu phiên đã hết hẳn. */
function refreshSession(): Promise<boolean> {
  refreshing ??= fetch(buildUrl('/auth/refresh'), {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: '{}' // refresh token nằm trong cookie HttpOnly
  })
    .then(r => r.ok)
    .catch(() => false)
    .finally(() => { refreshing = null; });
  return refreshing;
}

/** Đợi lần làm mới phiên đang chạy (nếu có) xong — gọi trước khi đăng xuất để response làm mới về muộn không ghi lại cookie. */
export async function waitForPendingRefresh(): Promise<void> {
  await refreshing;
}

/** Gọi API; gặp 401 thì làm mới phiên 1 lần rồi gọi lại (trừ các endpoint /auth/*). */
async function send(path: string, init: RequestInit, params?: QueryParams): Promise<Response> {
  const url = buildUrl(path, params);
  const res = await fetch(url, { ...init, credentials: 'include' });
  if (res.status !== 401 || path.startsWith('/auth/') || !(await refreshSession())) return res;
  return fetch(url, { ...init, credentials: 'include' });
}

async function request<T>(method: string, path: string, { params, body }: RequestOptions = {}): Promise<T> {
  // Endpoint backend chưa làm: báo "chưa có" ngay, không gửi request (tránh lỗi 404 trên trình duyệt).
  if (isPendingEndpoint(path)) throw new ApiError(NOT_READY_MESSAGE, 501, 'NOT_IMPLEMENTED', true);
  const isForm = body instanceof FormData;
  const res = await send(path, {
    method,
    headers: body !== undefined && !isForm ? { 'Content-Type': 'application/json' } : undefined,
    body: body === undefined ? undefined : isForm ? body : JSON.stringify(body)
  }, params);

  const payload: unknown = await res.json().catch(() => null);
  if (!res.ok) {
    const err = (payload ?? {}) as { message?: string; error?: string };
    throw toError(res.status, err.message, err.error);
  }
  return payload as T;
}

/** Lỗi HTTP → ApiError (đọc message / error từ ProblemDetails của backend). */
async function toApiError(res: Response): Promise<ApiError> {
  const err = ((await res.json().catch(() => null)) ?? {}) as { message?: string; error?: string };
  return toError(res.status, err.message, err.error);
}

/** GET nội dung không phải JSON (trang in HTML, file Excel…). */
async function getRaw(path: string, params?: QueryParams): Promise<Response> {
  const res = await send(path, {}, params);
  if (!res.ok) throw await toApiError(res);
  return res;
}

export const http = {
  get: <T>(path: string, params?: QueryParams) => request<T>('GET', path, { params }),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, { body }),
  put: <T>(path: string, body?: unknown) => request<T>('PUT', path, { body }),
  patch: <T>(path: string, body?: unknown) => request<T>('PATCH', path, { body }),
  delete: <T>(path: string) => request<T>('DELETE', path),
  /** Lấy văn bản (vd trang HTML để in). */
  getText: (path: string, params?: QueryParams) => getRaw(path, params).then(r => r.text()),
  /** Tải file kèm header Content-Disposition (để lấy tên file). */
  getFile: (path: string, params?: QueryParams) =>
    getRaw(path, params).then(async r => ({ blob: await r.blob(), disposition: r.headers.get('Content-Disposition') }))
};

/** Dạng phản hồi danh sách chuẩn của backend: { success, count, data }. */
export interface ListResponse<T> {
  data: T[];
  count?: number;
}
