import { useQuery } from '@tanstack/react-query'
import { CHAVE_CATALOGO_ESTOQUE, obterCatalogoEstoque } from '../../api/estoque.ts'

/** Catálogo de estoque inteiro (fornecedores, grupos e itens). Todas as telas de estoque leem desta consulta. */
export function useCatalogoEstoque() {
  return useQuery({ queryKey: CHAVE_CATALOGO_ESTOQUE, queryFn: obterCatalogoEstoque })
}
