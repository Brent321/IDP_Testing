import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  base: '/react/',
  build: {
    outDir: '../wwwroot/react',
    emptyOutDir: true
  },
  server: {
    proxy: {
      '/api': 'https://localhost:7235',
      '/authentication': 'https://localhost:7235'
    }
  }
})
