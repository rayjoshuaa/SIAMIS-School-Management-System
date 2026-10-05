import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  const target = env.SIAMIS_DEV_API_TARGET || 'https://localhost:7142';
  if (!/^https?:\/\/(localhost|127\.0\.0\.1)(:\d+)?$/.test(target)) {
    throw new Error('The Development proxy target must be an explicit loopback URL.');
  }
  return {
    plugins: [react(), tailwindcss()],
    server: {
      host: 'localhost',
      port: 5173,
      strictPort: true,
      proxy: { '/api': { target, changeOrigin: true, secure: true } },
    },
    build: { sourcemap: false },
  };
});
