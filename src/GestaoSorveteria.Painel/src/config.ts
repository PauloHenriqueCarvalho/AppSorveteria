/** Endereço da API, sem barra no fim. Em produção vem de VITE_API_URL (docs/07). */
export const API_URL = (import.meta.env.VITE_API_URL ?? 'http://localhost:5080').replace(/\/+$/, '')
