/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Endereço da API (ex.: https://sorveteria-api.onrender.com). Lido no build. */
  readonly VITE_API_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
