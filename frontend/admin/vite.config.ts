import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  // O app é servido sob /admin/ (mesmo basename do BrowserRouter), no dev e no nginx.
  base: '/admin/',
  plugins: [react()],
  server: {
    proxy: {
      '/api': 'http://localhost:5200',
    },
  },
})
