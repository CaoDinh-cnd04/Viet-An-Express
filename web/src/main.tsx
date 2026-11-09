import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { RouterProvider } from 'react-router-dom';
import { AppProviders } from '@/app/providers';
import { router } from '@/app/routes';
import '@/shared/styles/global.css';

// Trình duyệt khôi phục trang từ bộ nhớ Back/Forward (ảnh chụp cả dữ liệu cũ) → tải lại để kiểm tra phiên đăng nhập.
window.addEventListener('pageshow', e => {
  if (e.persisted) window.location.reload();
});

// Vừa đẩy bản mới lên máy chủ: tab đang mở còn giữ index.html cũ, file chunk cũ (vd ManagedOrdersPage-xxxx.js) đã bị thay →
// "Failed to fetch dynamically imported module". Tải lại trang 1 lần để lấy bản mới; đã thử trong 30 giây thì thôi (tránh lặp vô hạn).
window.addEventListener('vite:preloadError', e => {
  const KEY = 'va.chunkReloadAt';
  let last = 0;
  try {
    last = Number(sessionStorage.getItem(KEY) ?? 0);
    sessionStorage.setItem(KEY, String(Date.now()));
  } catch {
    /* sessionStorage bị chặn: vẫn tải lại 1 lần */
  }
  if (Date.now() - last < 30_000) return;
  e.preventDefault();
  window.location.reload();
});

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AppProviders>
      <RouterProvider router={router} />
    </AppProviders>
  </StrictMode>
);
