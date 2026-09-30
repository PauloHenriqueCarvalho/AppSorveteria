using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Comandas;
using GestaoSorveteria.Domain.Repositories;
using GestaoSorveteria.Domain.Usuarios;

namespace GestaoSorveteria.Application.Comandas;

/// <summary>
/// RN-CM-09: estorno de comanda fechada pelo painel. Não mexe em caixa nenhum (caixa fechado é imutável, RN-CX-07;
/// dinheiro devolvido ao cliente é uma sangria no caixa aberto, pelo app).
/// </summary>
public sealed class EstornoService
{
    private readonly IComandaRepository _comandas;
    private readonly IUsuarioRepository _usuarios;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public EstornoService(IComandaRepository comandas, IUsuarioRepository usuarios, IUnitOfWork uow, IClock clock)
    {
        _comandas = comandas;
        _usuarios = usuarios;
        _uow = uow;
        _clock = clock;
    }

    public async Task<EstornoResponse> EstornarAsync(Guid comandaId, EstornarComandaRequest request, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        // RN-US-01: só a dona. O controller já exige o perfil; aqui é a regra, para qualquer outra porta de entrada.
        var usuario = await _usuarios.ObterPorIdAsync(usuarioId, cancellationToken);
        if (usuario is not { Ativo: true, Perfil: PerfilUsuario.Admin })
        {
            throw new AcessoNegadoException("Só a administradora pode estornar uma venda.");
        }

        var comanda = await _comandas.ObterPorIdAsync(comandaId, cancellationToken)
            ?? throw new EntidadeNaoEncontradaException("Comanda", comandaId);

        comanda.Estornar(request.Motivo, usuarioId, _clock.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);

        return new EstornoResponse(comanda.Id, comanda.Status.ToString(), comanda.EstornadaEm!.Value, comanda.MotivoEstorno!);
    }
}
