import { defineConfig } from 'vite';
import { hybridSignaling } from './src/hlp/vite-plugin.js';

export default defineConfig({
  plugins: [hybridSignaling()],
  server: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: true,
  },
});
