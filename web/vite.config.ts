import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// The API origin is read from VITE_FORKCAST_API and falls back to the launchSettings http
// profile, so `npm run dev` works with no configuration at all.
export default defineConfig({
  plugins: [react()],
  server: { port: 5173, strictPort: true },
  preview: { port: 4173, strictPort: true },
  build: { outDir: 'dist', sourcemap: true },
})
