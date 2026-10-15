namespace GestaoSorveteria.Application.Clima;

/// <summary>
/// Estado da coleta do clima enquanto a API está ligada (singleton): última tentativa, última que deu certo e o erro, para o
/// painel mostrar. Também garante uma coleta por vez (RN-CL-04): o serviço em segundo plano e o "Atualizar agora" não correm juntos.
/// </summary>
public sealed class SituacaoColetaClima
{
    private readonly object _trava = new();

    internal SemaphoreSlim UmaPorVez { get; } = new(1, 1);

    public DateTime? UltimaTentativaEm { get; private set; }

    public DateTime? UltimaColetaEm { get; private set; }

    public string? UltimoErro { get; private set; }

    public (DateTime? UltimaTentativaEm, DateTime? UltimaColetaEm, string? UltimoErro) Ler()
    {
        lock (_trava)
        {
            return (UltimaTentativaEm, UltimaColetaEm, UltimoErro);
        }
    }

    internal void Registrar(DateTime emUtc, string? erro)
    {
        lock (_trava)
        {
            UltimaTentativaEm = emUtc;
            UltimoErro = erro;
            if (erro is null)
            {
                UltimaColetaEm = emUtc;
            }
        }
    }
}
