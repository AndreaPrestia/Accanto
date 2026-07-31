import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Admin Control Plane frontend. App SEPARATA dalla PWA pubblica:
// niente Service Worker, niente manifest PWA, niente caching aggressivo
// (evita di cachare risposte admin su client condivisi).
export default defineConfig({
  plugins: [react()],
  // Servito sotto il path /admin/ in produzione (es. https://admin.accanto.care/admin/...).
  base: '/',
  server: {
    host: true,
    port: 5174
  }
});
