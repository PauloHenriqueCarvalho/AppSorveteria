import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { CHAVE_CONFIGURACAO, obterConfiguracao, salvarConfiguracao } from '../../api/avisos.ts'
import { mensagemDoErro } from '../../api/erros.ts'
import type { ConfiguracaoLojaDto } from '../../api/tipos.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { CabecalhoPagina } from '../../componentes/Ui.tsx'
import { formatarWhatsApp } from '../../estoque.ts'
import LocalClima from './LocalClima.tsx'

/**
 * /configuracoes — RN-AV-10: nome nas mensagens e WhatsApp da dona (para onde vão o urgente e o resumo do dia); RN-CL-01: local
 * da loja para guardar o clima de cada dia.
 */
export default function Configuracoes() {
  const configuracao = useQuery({ queryKey: CHAVE_CONFIGURACAO, queryFn: obterConfiguracao })
  return (
    <>
      <CabecalhoPagina titulo="Configurações" subtitulo="Para onde vão os avisos do balcão e onde fica a loja" />
      <div className="painel">
        <Consulta consulta={configuracao}>{(dados) => <Formulario key={JSON.stringify(dados)} dados={dados} />}</Consulta>
        <LocalClima />
      </div>
    </>
  )
}

function Formulario({ dados }: { dados: ConfiguracaoLojaDto }) {
  const queryClient = useQueryClient()
  const [nome, setNome] = useState(dados.nomeLoja)
  const [whatsApp, setWhatsApp] = useState(dados.whatsAppDona ? formatarWhatsApp(dados.whatsAppDona) : '')

  const salvar = useMutation({
    mutationFn: salvarConfiguracao,
    onSuccess: (salvo) => queryClient.setQueryData(CHAVE_CONFIGURACAO, salvo),
  })

  function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    salvar.mutate({ nomeLoja: nome.trim(), whatsAppDona: whatsApp.trim() || null })
  }

  const teste = dados.whatsAppDona
    ? `https://wa.me/${dados.whatsAppDona.length <= 11 ? `55${dados.whatsAppDona}` : dados.whatsAppDona}?text=${encodeURIComponent(
        `Teste do sistema — ${dados.nomeLoja}. Se chegou, os avisos do balcão vão chegar aqui.`,
      )}`
    : null

  return (
    <form className="secao formulario" onSubmit={enviar}>
      {!dados.whatsAppDona && (
        <p className="alerta alerta-aviso">
          Sem o seu WhatsApp, o tablet abre o WhatsApp e o atendente precisa escolher o contato a cada aviso urgente.
        </p>
      )}
      <div className="grade-campos">
        <label className="campo">
          <span>Nome da loja nas mensagens</span>
          <input required minLength={2} maxLength={60} value={nome} onChange={(e) => setNome(e.target.value)} />
          <span className="suave dica">Aparece em “🔴 URGENTE — {nome || 'Sorveteria'}”.</span>
        </label>
        <label className="campo">
          <span>Seu WhatsApp</span>
          <input type="tel" inputMode="tel" placeholder="(11) 98765-4321" value={whatsApp} onChange={(e) => setWhatsApp(e.target.value)} />
          <span className="suave dica">Recebe o aviso urgente na hora e o resumo do fim do dia.</span>
        </label>
      </div>

      {salvar.isError && <p className="alerta alerta-erro">{mensagemDoErro(salvar.error)}</p>}
      {salvar.isSuccess && <p className="alerta alerta-sucesso">Configuração salva. O tablet usa o número novo na próxima sincronização.</p>}

      <div className="rodape-formulario">
        {teste && (
          <a className="botao botao-secundario" href={teste} target="_blank" rel="noreferrer">
            Enviar mensagem de teste
          </a>
        )}
        <button type="submit" className="botao" disabled={salvar.isPending}>
          {salvar.isPending ? 'Salvando…' : 'Salvar'}
        </button>
      </div>
    </form>
  )
}
