import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { mensagemDoErro } from '../../api/erros.ts'
import { anexarImagem, CHAVE_SUGESTOES, criarSugestao, type SalvarSugestaoRequest } from '../../api/sugestoes.ts'
import { CabecalhoPagina } from '../../componentes/Ui.tsx'
import { prepararImagem } from '../../sugestoes.ts'
import EscolherImagens from './EscolherImagens.tsx'
import FormularioSugestao from './FormularioSugestao.tsx'

/** /sugestoes/nova — a dona pede uma mudança ou funcionalidade (RN-SG-01), com imagens (RN-SG-05). */
export default function NovaSugestao() {
  const navegar = useNavigate()
  const queryClient = useQueryClient()
  const [arquivos, setArquivos] = useState<File[]>([])
  const [etapa, setEtapa] = useState<string | null>(null)

  const salvar = useMutation({
    mutationFn: async (dados: SalvarSugestaoRequest) => {
      setEtapa('Salvando a sugestão…')
      const criada = await criarSugestao(dados)
      const falhas: string[] = []
      for (const [i, arquivo] of arquivos.entries()) {
        setEtapa(`Enviando imagem ${i + 1} de ${arquivos.length}…`)
        try {
          const pronta = await prepararImagem(arquivo)
          await anexarImagem(criada.id, pronta.conteudo, pronta.nome)
        } catch (erro) {
          falhas.push(`${arquivo.name}: ${mensagemDoErro(erro)}`)
        }
      }
      return { criada, falhas }
    },
    onSuccess: async ({ criada, falhas }) => {
      await queryClient.invalidateQueries({ queryKey: CHAVE_SUGESTOES })
      navegar(`/sugestoes/${criada.id}`, { state: { falhasImagens: falhas } })
    },
    onSettled: () => setEtapa(null),
  })

  return (
    <>
      <p className="voltar">
        <Link to="/sugestoes">← Sugestões</Link>
      </p>
      <CabecalhoPagina
        titulo="Nova sugestão"
        subtitulo="Conte o que quer mudar ou ganhar no sistema. O Paulo responde por aqui e você acompanha o andamento."
      />
      <FormularioSugestao
        salvando={salvar.isPending}
        erro={salvar.isError ? mensagemDoErro(salvar.error) : null}
        textoBotao={etapa ?? 'Enviar sugestão'}
        aoSalvar={(dados) => salvar.mutate(dados)}
        aoCancelar={() => navegar('/sugestoes')}
        extra={<EscolherImagens arquivos={arquivos} aoMudar={setArquivos} />}
      />
    </>
  )
}
