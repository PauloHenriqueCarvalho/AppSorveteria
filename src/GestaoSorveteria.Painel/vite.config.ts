import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  // Porta fixa: é a origem liberada no CORS da API em desenvolvimento (Cors:PainelOrigem).
  server: { port: 5173, strictPort: true },
})
