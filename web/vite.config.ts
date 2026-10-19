/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { fileURLToPath, URL } from 'node:url';

// Dev: Vite chạy ở :5173, proxy /api sang backend Express (:3000).
// Build: xuất ra web/dist — backend tự phục vụ thư mục này nếu có.
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) }
  },
  server: {
    port: 5173,
    proxy: { '/api': 'http://localhost:3000' }
  },
  build: {
    outDir: 'dist',
    // Không xuất source map khi build production: file .map công khai làm lộ mã nguồn gốc (dev vẫn có map).
    sourcemap: false
  },
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts']
  }
});
