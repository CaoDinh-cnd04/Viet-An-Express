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

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AppProviders>
      <RouterProvider router={router} />
    </AppProviders>
  </StrictMode>
);
