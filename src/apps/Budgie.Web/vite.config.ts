import path from "path"
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// Where the dev server proxies /api and /uploads. Defaults to the API running on
// the same machine; the devcontainer `web` service sets it to the API container
// (http://app:5057).
const apiProxyTarget = process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5057'

// Vite rejects requests whose Host header it does not know. Port forwarding from
// the devcontainer (or a VS Code tunnel) reaches the dev server under arbitrary
// hostnames, so the host check is relaxed only when explicitly asked for:
// VITE_ALLOWED_HOSTS=true (any host) or VITE_ALLOWED_HOSTS=a,b (a list).
const allowedHosts = process.env.VITE_ALLOWED_HOSTS
  ? process.env.VITE_ALLOWED_HOSTS === 'true'
    ? true
    : process.env.VITE_ALLOWED_HOSTS.split(',').map((host) => host.trim())
  : undefined

// https://vite.dev/config/
export default defineConfig({
  plugins: [tailwindcss(), react()],
  build: {
    chunkSizeWarningLimit: 700,
  },
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  optimizeDeps: {
    include: ["react-is"],
  },
  server: {
    host: true,
    port: 5173,
    ...(allowedHosts ? { allowedHosts } : {}),
    proxy: {
      '/api': {
        target: apiProxyTarget,
        changeOrigin: true,
        secure: false,
      },
      '/uploads': {
        target: apiProxyTarget,
        changeOrigin: true,
        secure: false,
      }
    }
  }
})
