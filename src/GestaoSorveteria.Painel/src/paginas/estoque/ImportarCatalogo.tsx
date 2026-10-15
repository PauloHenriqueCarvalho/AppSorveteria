import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type ChangeEvent } from 'react'
import { Link } from 'react-router'
import { mensagemDoErro } from '../../api/erros.ts'
import { CHAVE_CATALOGO_ESTOQUE, importarCatalogo } from '../../api/estoque.ts'
import type { ImportarCatalogoResponse, LinhaImportacaoDto } from '../../api/tipos.ts'
import { CabecalhoPagina, Indicador, Secao, Selo } from '../../componentes/Ui.tsx'
import { COLUNAS_CSV, decodificarCsv, TAMANHO_MAXIMO_CSV } from '../../estoque.ts'
import { baixarCsv, montarCsv } from '../../exportar.ts'

interface Arquivo {
  nome: string
  conteudo: string
}

/**
 * /estoque/importar — RN-ES-10. Escolher o arquivo → prévia (nada gravado) → confirmar.
 * A importação só cria o que falta; o que já está no catálogo fica como está.
 */
export default function ImportarCatalogo() {
  const queryClient = useQueryClient()
  const [arquivo, setArquivo] = useState<Arquivo | null>(null)
  const [erroArquivo, setErroArquivo] = useState<string | null>(null)

  const previa = useMutation({ mutationFn: (conteudo: string) => importarCatalogo(conteudo, true) })
  const confirmar = useMutation({
    mutationFn: (conteudo: string) => importarCatalogo(conteudo, false),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: CHAVE_CATALOGO_ESTOQUE }),
  })

  async function escolher(evento: ChangeEvent<HTMLInputElement>) {
    const escolhido = evento.target.files?.[0]
    evento.target.value = '' // escolher o mesmo arquivo de novo (depois de corrigir) dispara outra prévia
    previa.reset()
    confirmar.reset()
    setArquivo(null)
    setErroArquivo(null)
    if (!escolhido) {
      return
    }
    if (escolhido.size > TAMANHO_MAXIMO_CSV) {
      setErroArquivo('O arquivo passa de 1 MB. Confira se é mesmo a planilha do catálogo, salva como CSV.')
      return
    }
    const conteudo = decodificarCsv(await escolhido.arrayBuffer())
    setArquivo({ nome: escolhido.name, conteudo })
    previa.mutate(conteudo)
  }

  function baixarModelo() {
    baixarCsv(
      'modelo-catalogo-estoque.csv',
      montarCsv([...COLUNAS_CSV], [['Copos', 'Copo 300 ml', 'Pacote', 50, '12,90', 'Descartáveis Silva']]),
    )
  }

  const resultado = confirmar.data
  const dados = previa.data

  return (
    <>
      <p className="voltar">
        <Link to="/estoque">← Catálogo de estoque</Link>
      </p>
      <CabecalhoPagina
        titulo="Importar planilha"
        subtitulo="Cria no catálogo os grupos, fornecedores e itens que ainda não existem. Nada do que já está cadastrado é alterado."
      />

      {resultado ? (
        <Concluido resultado={resultado} />
      ) : (
        <>
          <Secao titulo="1. Escolha o arquivo">
            <p className="suave">
              Arquivo CSV com as colunas <code>{COLUNAS_CSV.join(', ')}</code>. Pode ser o que veio da planilha da dona ou um salvo pelo
              Excel (“CSV UTF-8” ou “CSV separado por vírgulas”).
            </p>
            <div className="acoes acoes-inicio">
              <label className="botao">
                <input type="file" accept=".csv,text/csv" className="escondido" onChange={(e) => void escolher(e)} />
                {arquivo ? 'Escolher outro arquivo' : 'Escolher arquivo'}
              </label>
              <button type="button" className="botao botao-secundario" onClick={baixarModelo}>
                Baixar modelo
              </button>
            </div>
            {arquivo && <p className="suave">Arquivo: {arquivo.nome}</p>}
            {erroArquivo && <p className="alerta alerta-erro">{erroArquivo}</p>}
          </Secao>

          {previa.isPending && <p className="secao suave">Conferindo o arquivo…</p>}
          {previa.isError && <p className="alerta alerta-erro">{mensagemDoErro(previa.error)}</p>}

          {dados && arquivo && (
            <Previa
              dados={dados}
              confirmando={confirmar.isPending}
              erroConfirmar={confirmar.isError ? mensagemDoErro(confirmar.error) : null}
              aoConfirmar={() => confirmar.mutate(arquivo.conteudo)}
            />
          )}
        </>
      )}
    </>
  )
}

function Previa({
  dados,
  confirmando,
  erroConfirmar,
  aoConfirmar,
}: {
  dados: ImportarCatalogoResponse
  confirmando: boolean
  erroConfirmar: string | null
  aoConfirmar: () => void
}) {
  const nada = dados.itensNovos === 0
  return (
    <>
      <div className="grade-indicadores">
        <Indicador rotulo="Itens novos" valor={dados.itensNovos} detalhe="serão criados" tom="marca" />
        <Indicador rotulo="Já cadastrados" valor={dados.itensJaCadastrados} detalhe="ficam como estão" />
        <Indicador
          rotulo="Diferenças"
          valor={dados.avisos.length}
          detalhe="planilha diferente do catálogo"
          tom={dados.avisos.length > 0 ? 'atencao' : 'neutro'}
        />
        <Indicador rotulo="Linhas com erro" valor={dados.erros.length} detalhe="ficam de fora" tom={dados.erros.length > 0 ? 'atencao' : 'neutro'} />
      </div>

      {(dados.gruposNovos.length > 0 || dados.fornecedoresNovos.length > 0) && (
        <Secao titulo="2. Confira o que será criado">
          {dados.gruposNovos.length > 0 && (
            <p>
              <strong>Grupos novos:</strong>{' '}
              <span className="selos">
                {dados.gruposNovos.map((g) => (
                  <Selo key={g} tom="marca">
                    {g}
                  </Selo>
                ))}
              </span>
            </p>
          )}
          {dados.fornecedoresNovos.length > 0 && (
            <p>
              <strong>Fornecedores novos:</strong>{' '}
              <span className="selos">
                {dados.fornecedoresNovos.map((f) => (
                  <Selo key={f} tom="marca">
                    {f}
                  </Selo>
                ))}
              </span>
            </p>
          )}
          <p className="suave">
            Se algum desses já existe com outro nome (você renomeou no painel), corrija o nome na planilha antes de importar, senão ele
            aparece duas vezes. Fornecedor novo começa com pedido na segunda e entrega em 2 dias; ajuste em Fornecedores.
          </p>
        </Secao>
      )}

      {dados.erros.length > 0 && <TabelaLinhas titulo="Linhas com erro (ficam de fora)" linhas={dados.erros} />}
      {dados.avisos.length > 0 && <TabelaLinhas titulo="Diferenças com o catálogo (o catálogo é mantido)" linhas={dados.avisos} />}

      <section className="secao rodape-acao">
        {erroConfirmar && <p className="alerta alerta-erro">{erroConfirmar}</p>}
        <p className="suave">
          {nada
            ? 'Não há item novo para importar: tudo o que está no arquivo já está no catálogo ou tem erro.'
            : dados.erros.length > 0
              ? 'Você pode importar agora (as linhas com erro ficam de fora) ou corrigir o arquivo e escolher de novo.'
              : 'Tudo certo com o arquivo.'}
        </p>
        <button type="button" className="botao" disabled={nada || confirmando} onClick={aoConfirmar}>
          {confirmando ? 'Importando…' : `Importar ${dados.itensNovos} ${dados.itensNovos === 1 ? 'item' : 'itens'}`}
        </button>
      </section>
    </>
  )
}

function TabelaLinhas({ titulo, linhas }: { titulo: string; linhas: LinhaImportacaoDto[] }) {
  return (
    <Secao titulo={titulo}>
      <div className="tabela-caixa">
        <table className="tabela">
          <thead>
            <tr>
              <th scope="col" className="numero">
                Linha
              </th>
              <th scope="col">O que aconteceu</th>
            </tr>
          </thead>
          <tbody>
            {linhas.map((l) => (
              <tr key={`${l.linha}-${l.mensagem}`}>
                <td className="numero forte">{l.linha}</td>
                <td>{l.mensagem}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Secao>
  )
}

function Concluido({ resultado }: { resultado: ImportarCatalogoResponse }) {
  return (
    <section className="secao">
      <p className="alerta alerta-sucesso">
        {resultado.itensNovos} {resultado.itensNovos === 1 ? 'item importado' : 'itens importados'}
        {resultado.gruposNovos.length > 0 &&
          ` em ${resultado.gruposNovos.length} ${resultado.gruposNovos.length === 1 ? 'grupo novo' : 'grupos novos'}`}
        .
      </p>
      {resultado.fornecedoresNovos.length > 0 && (
        <p>
          Confira os dias de pedido e o prazo de entrega de <strong>{resultado.fornecedoresNovos.join(', ')}</strong> em Fornecedores.
        </p>
      )}
      <div className="acoes acoes-inicio">
        <Link to="/estoque" className="botao">
          Ver o catálogo
        </Link>
        <Link to="/fornecedores" className="botao botao-secundario">
          Fornecedores
        </Link>
      </div>
    </section>
  )
}
