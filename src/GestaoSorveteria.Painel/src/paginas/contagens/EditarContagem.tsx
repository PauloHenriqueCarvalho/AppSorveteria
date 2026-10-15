import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { CHAVE_CONTAGENS, novaContagem, obterContagem, salvarContagem } from '../../api/contagens.ts'
import { mensagemDoErro } from '../../api/erros.ts'
import type { ContagemDto, LinhaContagemDto } from '../../api/tipos.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { CabecalhoPagina, Selo } from '../../componentes/Ui.tsx'
import {
  embalagemNoPlural,
  formatarEmbalagens,
  lerQuantidade,
  nomeDaEmbalagem,
  quando,
  quantidadeParaCampo,
  respeitaPasso,
} from '../../estoque.ts'

/** /contagens/nova e /contagens/:id — contar (rascunho), concluir, ou ver uma contagem concluída. */
export default function EditarContagem() {
  const { id } = useParams()
  const contagem = useQuery({
    queryKey: id ? [...CHAVE_CONTAGENS, id] : [...CHAVE_CONTAGENS, 'nova'],
    queryFn: () => (id ? obterContagem(id) : novaContagem()),
    staleTime: id ? 30_000 : Infinity, // o modelo da contagem nova não muda enquanto a dona digita
  })
  return <Consulta consulta={contagem}>{(dados) => <Planilha key={`${dados.id}-${dados.concluidaEm}`} contagem={dados} nova={!id} />}</Consulta>
}

function Planilha({ contagem, nova }: { contagem: ContagemDto; nova: boolean }) {
  const navegar = useNavigate()
  const queryClient = useQueryClient()
  const concluida = contagem.concluidaEm !== null
  const [valores, setValores] = useState<Record<string, string>>(() =>
    Object.fromEntries(contagem.linhas.map((l) => [l.itemEstoqueId, l.contadoEmbalagens === null ? '' : quantidadeParaCampo(l.contadoEmbalagens)])),
  )
  const [erro, setErro] = useState<string | null>(null)
  const [salvoEm, setSalvoEm] = useState<string | null>(null)

  const salvar = useMutation({
    mutationFn: salvarContagem,
    onSuccess: async (salva, pedido) => {
      await queryClient.invalidateQueries({ queryKey: CHAVE_CONTAGENS })
      if (pedido.concluir || nova) {
        navegar(pedido.concluir ? '/contagens' : `/contagens/${salva.id}`, { replace: nova })
      } else {
        setSalvoEm(new Date().toISOString())
      }
    },
  })

  function enviar(concluir: boolean) {
    setErro(null)
    const linhas: { itemEstoqueId: string; embalagens: number | null }[] = []
    for (const linha of contagem.linhas) {
      const texto = valores[linha.itemEstoqueId]?.trim() ?? ''
      if (texto === '') {
        if (linha.contadoUnidades !== null) {
          linhas.push({ itemEstoqueId: linha.itemEstoqueId, embalagens: null }) // apagou: volta a "não contado"
        }
        continue
      }
      const valor = lerQuantidade(texto)
      if (valor === null || !respeitaPasso(valor, linha.passoEmbalagens)) {
        setErro(
          `${linha.grupoNome} ${linha.itemNome}: ${linha.passoEmbalagens < 1 ? 'use caixas inteiras ou meia (ex.: 2,5)' : 'use número inteiro'}.`,
        )
        return
      }
      linhas.push({ itemEstoqueId: linha.itemEstoqueId, embalagens: valor })
    }
    if (concluir && !linhas.some((l) => l.embalagens !== null) && !contagem.linhas.some((l) => l.contadoUnidades !== null)) {
      setErro('Conte pelo menos um item antes de concluir.')
      return
    }
    salvar.mutate({ id: contagem.id, linhas, concluir })
  }

  function definir(linha: LinhaContagemDto, valor: string) {
    setValores((atuais) => ({ ...atuais, [linha.itemEstoqueId]: valor }))
  }

  function somar(linha: LinhaContagemDto, delta: number) {
    const atual = lerQuantidade(valores[linha.itemEstoqueId] ?? '') ?? 0
    definir(linha, quantidadeParaCampo(Math.max(0, atual + delta)))
  }

  const grupos = [...new Map(contagem.linhas.map((l) => [l.grupoId, l.grupoNome])).entries()]
  const contados = contagem.linhas.filter((l) => (valores[l.itemEstoqueId] ?? '').trim() !== '').length

  return (
    <>
      <p className="voltar">
        <Link to="/contagens">← Contagem semanal</Link>
      </p>
      <CabecalhoPagina
        titulo={nova ? 'Nova contagem' : `Contagem de ${quando(contagem.iniciadaEm)}`}
        subtitulo={`${contagem.contadoPorNome} · ${contagem.origem === 'App' ? 'tablet' : 'painel'} · ${contados} de ${contagem.linhas.length} itens contados`}
        acoes={<Selo tom={concluida ? 'bom' : 'atencao'}>{concluida ? `Concluída ${quando(contagem.concluidaEm!)}` : 'Em andamento'}</Selo>}
      />

      {contagem.linhas.length === 0 && (
        <section className="secao vazio">
          <h2>Nenhum item para contar</h2>
          <p className="suave">
            A contagem usa o catálogo de estoque: os sabores e itens de cada grupo, na ordem da planilha. O catálogo ainda está vazio
            (ou todos os grupos estão como “só aviso”, que não entram na contagem).
          </p>
          <p className="pagina-acoes">
            <Link to="/estoque/importar" className="botao">
              Importar a planilha
            </Link>
            <Link to="/estoque" className="botao botao-secundario">
              Abrir o catálogo
            </Link>
          </p>
        </section>
      )}

      {!concluida && contagem.linhas.length > 0 && (
        <p className="alerta alerta-aviso">
          Conte o que tem no freezer agora. A contagem anterior aparece só como referência; deixe em branco o que não contou e use 0 para
          “não tem nenhum”. Caixas vão de meia em meia (2,5).
        </p>
      )}

      {contagem.linhas.length > 0 && (
        <section className="secao secao-tabela">
          <div className="tabela-caixa">
            <table className="tabela tabela-contagem">
              <thead>
                <tr>
                  <th scope="col">Item</th>
                  <th scope="col" className="numero so-largo">
                    Anterior
                  </th>
                  <th scope="col" className="numero">
                    {concluida ? 'Contado' : 'Agora'}
                  </th>
                </tr>
              </thead>
              {grupos.map(([grupoId, grupoNome]) => (
                <tbody key={grupoId}>
                  <tr className="linha-grupo">
                    <th scope="rowgroup" colSpan={3}>
                      <span className="linha-grupo-conteudo">
                        <strong>{grupoNome}</strong>
                      </span>
                    </th>
                  </tr>
                  {contagem.linhas
                    .filter((l) => l.grupoId === grupoId)
                    .map((l) => (
                      <tr key={l.itemEstoqueId}>
                        <td>
                          <strong>{l.itemNome}</strong>
                          {concluida && l.saldoSistemaUnidades !== null && l.contadoUnidades !== null &&
                            Math.abs(l.contadoUnidades - l.saldoSistemaUnidades) > 1 && (
                              <>
                                {' '}
                                <Selo tom="atencao">
                                  {Math.abs(l.contadoUnidades - l.saldoSistemaUnidades)}{' '}
                                  {l.contadoUnidades > l.saldoSistemaUnidades ? 'a mais' : 'a menos'} que o sistema
                                </Selo>
                              </>
                            )}
                          <span className="suave bloco pequeno">
                            {l.unidadesPorEmbalagem > 1 ? `${l.unidade.toLowerCase()} com ${l.unidadesPorEmbalagem}` : l.unidade.toLowerCase()}
                            {l.anteriorEmbalagens !== null && (
                              <span className="so-estreito">
                                {' '}
                                · antes {formatarEmbalagens(l.anteriorEmbalagens)}
                              </span>
                            )}
                            {/* RN-ES-06: o número do sistema só aparece depois de contar, para não contar "pelo sistema". */}
                            {l.saldoSistemaUnidades !== null && (concluida || l.contadoUnidades !== null) && <> · sistema {l.saldoSistemaUnidades}</>}
                          </span>
                        </td>
                        <td className="numero so-largo suave">
                          {formatarEmbalagens(l.anteriorEmbalagens)}{' '}
                          {l.anteriorEmbalagens !== null && nomeDaEmbalagem(l.unidade, l.anteriorEmbalagens)}
                        </td>
                        <td className="numero">
                          {concluida ? (
                            <span className="forte">
                              {l.contadoEmbalagens === null
                                ? 'não contado'
                                : `${formatarEmbalagens(l.contadoEmbalagens)} ${nomeDaEmbalagem(l.unidade, l.contadoEmbalagens)}`}
                            </span>
                          ) : (
                            <span className="contador-campo">
                              <button type="button" className="botao botao-secundario" aria-label={`Menos ${l.itemNome}`} onClick={() => somar(l, -l.passoEmbalagens)}>
                                −
                              </button>
                              <input
                                inputMode="decimal"
                                aria-label={`${l.itemNome} em ${embalagemNoPlural(l.unidade)}`}
                                placeholder="—"
                                value={valores[l.itemEstoqueId] ?? ''}
                                onChange={(e) => definir(l, e.target.value)}
                              />
                              <button type="button" className="botao botao-secundario" aria-label={`Mais ${l.itemNome}`} onClick={() => somar(l, l.passoEmbalagens)}>
                                +
                              </button>
                            </span>
                          )}
                        </td>
                      </tr>
                    ))}
                </tbody>
              ))}
            </table>
          </div>
        </section>
      )}

      {!concluida && contagem.linhas.length > 0 && (
        <section className="secao rodape-acao">
          {erro && <p className="alerta alerta-erro">{erro}</p>}
          {salvar.isError && <p className="alerta alerta-erro">{mensagemDoErro(salvar.error)}</p>}
          <p className="suave">
            {salvoEm ? `Rascunho salvo ${quando(salvoEm)}. ` : ''}
            Pode salvar no meio e terminar depois. Concluída, a contagem não muda mais.
          </p>
          <span className="pagina-acoes">
            <button type="button" className="botao botao-secundario" disabled={salvar.isPending} onClick={() => enviar(false)}>
              Salvar rascunho
            </button>
            <button type="button" className="botao" disabled={salvar.isPending} onClick={() => enviar(true)}>
              {salvar.isPending ? 'Salvando…' : 'Concluir contagem'}
            </button>
          </span>
        </section>
      )}
    </>
  )
}
