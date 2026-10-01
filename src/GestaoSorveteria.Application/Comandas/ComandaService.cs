using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Comandas;
using GestaoSorveteria.Domain.Repositories;
using GestaoSorveteria.Domain.Usuarios;

namespace GestaoSorveteria.Application.Comandas;

/// <summary>Ações do painel sobre vendas já recebidas. Fase 1: estorno (RN-CM-09).</summary>
public sealed class ComandaService
{
    private readonly IComandaRepository _comandas;
    private readonly IUsuarioRepository _usuarios;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ComandaConsultaService _consulta;

    public ComandaService(
        IComandaRepository comandas,
        IUsuarioRepository usuarios,
        IUnitOfWork uow,
        IClock clock,
        ComandaConsultaService consulta)
    {
        _comandas = comandas;
        _usuarios = usuarios;
        _uow = uow;
        _clock = clock;
        _consulta = consulta;
    }

    /// <summary>
    /// RN-CM-09: só Admin ativo, só comanda fechada, motivo obrigatório. Não altera caixa nenhum (RN-CX-07):
    /// dinheiro devolvido ao cliente é uma sangria registrada no caixa aberto, pelo app.
    /// </summary>
    public async Task<ComandaDetalheDto> EstornarAsync(Guid id, EstornarComandaRequest request, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarios.ObterPorIdAsync(usuarioId, cancellationToken);
        if (usuario is not { Ativo: true, Perfil: PerfilUsuario.Admin })
        {
            throw new AcessoNegadoException("Só a administração da loja pode estornar uma venda.");
        }

        var comanda = await _comandas.ObterPorIdAsync(id, cancellationToken)
            ?? throw new EntidadeNaoEncontradaException("Venda não encontrada.");

        comanda.Estornar(request.Motivo, usuario.Id, _clock.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);

        return await _consulta.ObterAsync(id, cancellationToken);
    }
}
