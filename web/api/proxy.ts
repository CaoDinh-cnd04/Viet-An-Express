/**
 * Vercel Edge Function — chuyển tiếp /api/* của portal về backend Render hoặc máy chủ Việt An (qua ngrok).
 *
 * - Trình duyệt chỉ thấy 1 tên miền (Vercel) → cookie đăng nhập SameSite=Strict hoạt động.
 * - Thêm header `ngrok-skip-browser-warning`: ngrok gói miễn phí chèn trang cảnh báo cho request từ trình duyệt.
 * - Máy chủ tắt / ngrok mất kết nối → trả 503 kèm câu tiếng Việt (ngrok trả 404 sẽ bị frontend hiểu là "chưa làm").
 *
 * Cấu hình trên Vercel: BACKEND_URL = https://<service>.onrender.com (hoặc domain backend HTTPS).
 * vercel.json rewrite: /api/:path* → /api/proxy?__path=:path*
 */
export const config = { runtime: 'edge' };

/** Header không được chuyển tiếp nguyên trạng giữa 2 kết nối. */
const HOP_BY_HOP = ['host', 'connection', 'keep-alive', 'proxy-connection', 'transfer-encoding', 'upgrade', 'content-length'];

function problem(status: number, error: string, message: string): Response {
  return new Response(JSON.stringify({ status, error, message }), {
    status,
    headers: { 'content-type': 'application/problem+json; charset=utf-8', 'cache-control': 'no-store' }
  });
}

export default async function handler(request: Request): Promise<Response> {
  const backend = (process.env.BACKEND_URL ?? '').replace(/\/+$/, '');
  if (!backend) return problem(500, 'BACKEND_NOT_CONFIGURED', 'Chưa cấu hình địa chỉ máy chủ (BACKEND_URL) trên Vercel');

  const url = new URL(request.url);
  const path = url.searchParams.get('__path') ?? '';
  url.searchParams.delete('__path');
  // Vercel tự gắn tham số tên `path` (từ `:path*` của rewrite) vào query — bỏ đi để backend nhận đúng query gốc
  // (callback OAuth của sàn thường ký HMAC trên toàn bộ query, thừa 1 tham số là sai chữ ký).
  if (url.searchParams.get('path') === path) url.searchParams.delete('path');
  // Chỉ cho phép đường dẫn API bình thường — chặn "../" để không vượt ra ngoài /api.
  if (path.split('/').some(segment => segment === '..' || segment === '.')) {
    return problem(400, 'INVALID_PATH', 'Đường dẫn không hợp lệ');
  }

  const headers = new Headers(request.headers);
  HOP_BY_HOP.forEach(h => headers.delete(h));
  // Fetch của Edge không hỗ trợ Expect: 100-continue. Proxy đã nhận body từ client;
  // không chuyển tiếp yêu cầu bắt tay này sang kết nối backend.
  headers.delete('expect');
  headers.set('ngrok-skip-browser-warning', '1');
  headers.set('x-forwarded-host', url.host);

  const init: RequestInit & { duplex?: 'half' } = { method: request.method, headers, redirect: 'manual' };
  if (request.method !== 'GET' && request.method !== 'HEAD') {
    init.body = request.body;
    init.duplex = 'half'; // bắt buộc khi gửi body dạng stream
  }

  let upstream: Response;
  try {
    upstream = await fetch(`${backend}/api/${path}${url.search}`, init);
  } catch {
    return problem(503, 'BACKEND_UNREACHABLE', 'Máy chủ Việt An tạm thời không phản hồi, vui lòng thử lại sau ít phút');
  }

  // Lỗi của chính ngrok (máy chủ tắt, tunnel mất) — không phải lỗi của API.
  if (upstream.headers.has('ngrok-error-code')) {
    return problem(503, 'BACKEND_OFFLINE', 'Máy chủ Việt An đang tạm dừng, vui lòng thử lại sau ít phút');
  }

  const out = new Headers(upstream.headers);
  // fetch đã giải nén body → bỏ các header mô tả body gốc.
  out.delete('content-encoding');
  out.delete('content-length');
  return new Response(upstream.body, { status: upstream.status, statusText: upstream.statusText, headers: out });
}
