// Ícones de traço (24×24), desenhados aqui para o painel não depender de biblioteca.
// Sempre decorativos: o texto ao lado (ou o aria-label do botão) diz o que é.

const CAMINHOS = {
  inicio: 'M3 10.5 12 3l9 7.5V21H3zM9 21v-6h6v6',
  vendas: 'M5 3h14v18l-3-2-2 2-2-2-2 2-2-2-3 2zM9 8h6M9 12h6',
  caixas: 'M3 7h18v13H3zM7 7V4h10v3M3 12h18',
  produtos: 'M20 12 12 20 4 12V4h8zM8.5 7a1.5 1.5 0 1 0 0 3 1.5 1.5 0 0 0 0-3',
  usuarios: 'M9 4a4 4 0 1 0 0 8 4 4 0 0 0 0-8M2 21c0-4 3-6 7-6s7 2 7 6M17 11a3 3 0 0 0 0-6M22 21c0-3-2-5-4-5.5',
  sair: 'M15 4h4v16h-4M10 17l5-5-5-5M15 12H3',
  menu: 'M4 6h16M4 12h16M4 18h16',
  fechar: 'M6 6l12 12M18 6 6 18',
  alerta: 'M12 3 2 21h20zM12 10v5M12 18h.01',
  seta: 'M5 12h14M13 6l6 6-6 6',
  calendario: 'M4 5h16v16H4zM4 10h16M9 3v4M15 3v4',
  estoque: 'M3 7.5 12 3l9 4.5v9L12 21l-9-4.5zM3 7.5 12 12l9-4.5M12 12v9',
  fornecedor: 'M2 6h12v10H2zM14 10h4l4 4v2h-8M6.5 19.5a2 2 0 1 0 0-4 2 2 0 0 0 0 4M17.5 19.5a2 2 0 1 0 0-4 2 2 0 0 0 0 4',
} as const

export type NomeIcone = keyof typeof CAMINHOS

export default function Icone({ nome, tamanho = 18 }: { nome: NomeIcone; tamanho?: number }) {
  return (
    <svg
      width={tamanho}
      height={tamanho}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      <path d={CAMINHOS[nome]} />
    </svg>
  )
}
