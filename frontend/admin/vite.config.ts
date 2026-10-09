import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  // O app é servido sob /admin/ (mesmo basename do BrowserRouter), no dev e no nginx.
  base: '/admin/',
  plugins: [react()],
  // O app inteiro vai num pacote só (uso interno, poucos usuários); 500 kB é só o limite do aviso do Vite.
  build: { chunkSizeWarningLimit: 900 },
  server: {
    proxy: {
      '/api': 'http://localhost:5200',
    },
  },
})
