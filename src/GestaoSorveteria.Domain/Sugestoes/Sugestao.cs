using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Domain.Sugestoes;

/// <summary>RN-SG-01: o que a dona está pedindo.</summary>
public enum TipoSugestao
{
    Ideia = 1,
    Mudanca = 2,
    Problema = 3,
    Duvida = 4,
}

/// <summary>RN-SG-01: em que parte do sistema.</summary>
public enum AreaSugestao
{
    Balcao = 1,
    Painel = 2,
    Estoque = 3,
    Relatorios = 4,
    Outro = 5,
}

/// <summary>RN-SG-01: quanto faz falta para a loja.</summary>
public enum ImportanciaSugestao
{
    Baixa = 1,
    Media = 2,
    Alta = 3,
}

/// <summary>RN-SG-03: andamento. Nova → Em análise → Planejada → Feita; ou Recusada (com o motivo na conversa).</summary>
public enum StatusSugestao
{
    Nova = 1,
    EmAnalise = 2,
    Planejada = 3,
    Feita = 4,
    Recusada = 5,
}

/// <summary>
/// RN-SG-01..05: sugestão de mudança ou de funcionalidade feita pela dona no painel, com a conversa com o Paulo.
/// Nunca é apagada; as imagens ficam em <see cref="SugestaoImagem"/> (outra tabela, carregadas só quando pedidas).
/// </summary>
public sealed class Sugestao : Entity
{
    public const int TituloMaximo = 120;
    public const int DescricaoMaxima = 4000;
    public const int ComentarioMaximo = 2000;
    public const int ImagensMaximo = 5;

    private readonly List<SugestaoComentario> _comentarios = new();

    public string Titulo { get; private set; }
    public string Descricao { get; private set; }
    public TipoSugestao Tipo { get; private set; }
    public AreaSugestao Area { get; private set; }
    public ImportanciaSugestao Importancia { get; private set; }
    public StatusSugestao Status { get; private set; }
    public Guid CriadaPorUsuarioId { get; private set; }
    public DateTime CriadaEm { get; private set; }
    public DateTime AtualizadaEm { get; private set; }

    public IReadOnlyCollection<SugestaoComentario> Comentarios => _comentarios.AsReadOnly();

    /// <summary>RN-SG-02: Feita e Recusada estão encerradas: o texto não muda mais (a conversa continua).</summary>
    public bool Encerrada => Status is StatusSugestao.Feita or StatusSugestao.Recusada;

    // EF Core
    private Sugestao()
    {
        Titulo = string.Empty;
        Descricao = string.Empty;
    }

    private Sugestao(Guid? id) : base(id)
    {
        Titulo = string.Empty;
        Descricao = string.Empty;
    }

    public static Sugestao Criar(
        string titulo,
        string descricao,
        TipoSugestao tipo,
        AreaSugestao area,
        ImportanciaSugestao importancia,
        Guid usuarioId,
        DateTime agoraUtc,
        Guid? id = null)
    {
        Guard.NaoVazio(usuarioId, "quem fez a sugestão");
        Guard.Utc(agoraUtc, "a data da sugestão");
        var sugestao = new Sugestao(id)
        {
            Status = StatusSugestao.Nova,
            CriadaPorUsuarioId = usuarioId,
            CriadaEm = agoraUtc,
        };
        sugestao.Editar(titulo, descricao, tipo, area, importancia, agoraUtc);
        return sugestao;
    }

    /// <summary>RN-SG-02: muda o texto e as escolhas enquanto não está encerrada.</summary>
    public void Editar(string titulo, string descricao, TipoSugestao tipo, AreaSugestao area, ImportanciaSugestao importancia, DateTime agoraUtc)
    {
        Guard.Contra(Encerrada, "Esta sugestão já foi encerrada e não pode mais ser alterada. Escreva na conversa ou faça uma nova.");
        var tituloValido = Guard.Texto(titulo, "o título", 3, TituloMaximo);
        var descricaoValida = Guard.Texto(descricao, "a descrição", 1, DescricaoMaxima);
        Guard.Contra(!Enum.IsDefined(tipo), "Tipo de sugestão inválido.");
        Guard.Contra(!Enum.IsDefined(area), "Parte do sistema inválida.");
        Guard.Contra(!Enum.IsDefined(importancia), "Importância inválida.");
        Guard.Utc(agoraUtc, "a data da alteração");

        Titulo = tituloValido;
        Descricao = descricaoValida;
        Tipo = tipo;
        Area = area;
        Importancia = importancia;
        AtualizadaEm = agoraUtc;
    }

    /// <summary>RN-SG-04: conversa entre a dona e o Paulo. Vale também depois de encerrada.</summary>
    public SugestaoComentario Comentar(string texto, Guid usuarioId, DateTime agoraUtc)
    {
        Guard.NaoVazio(usuarioId, "quem escreveu");
        Guard.Utc(agoraUtc, "a data do comentário");
        var comentario = new SugestaoComentario(Id, usuarioId, Guard.Texto(texto, "o comentário", 1, ComentarioMaximo), agoraUtc);
        _comentarios.Add(comentario);
        AtualizadaEm = agoraUtc;
        return comentario;
    }

    /// <summary>
    /// RN-SG-03: muda o andamento, com um comentário opcional (obrigatório para recusar: a dona precisa saber o porquê).
    /// A mudança fica registrada na conversa.
    /// </summary>
    public void AlterarStatus(StatusSugestao novo, string? comentario, Guid usuarioId, DateTime agoraUtc)
    {
        Guard.Contra(!Enum.IsDefined(novo), "Andamento inválido.");
        Guard.Contra(novo == Status, "A sugestão já está nesse andamento.");
        Guard.Contra(novo == StatusSugestao.Recusada && string.IsNullOrWhiteSpace(comentario),
            "Explique por que a sugestão não vai ser feita.");
        var texto = Guard.TextoOpcional(comentario, "o comentário", ComentarioMaximo - 60);

        var registro = $"Andamento: {NomeDoStatus(Status)} → {NomeDoStatus(novo)}." + (texto is null ? string.Empty : $"\n{texto}");
        Comentar(registro, usuarioId, agoraUtc);
        Status = novo;
    }

    public static string NomeDoStatus(StatusSugestao status) => status switch
    {
        StatusSugestao.Nova => "Nova",
        StatusSugestao.EmAnalise => "Em análise",
        StatusSugestao.Planejada => "Planejada",
        StatusSugestao.Feita => "Feita",
        StatusSugestao.Recusada => "Não vai ser feita",
        _ => status.ToString(),
    };
}

/// <summary>RN-SG-04: uma mensagem da conversa. Nunca é apagada.</summary>
public sealed class SugestaoComentario : Entity
{
    public Guid SugestaoId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string Texto { get; private set; }
    public DateTime Em { get; private set; }

    // EF Core
    private SugestaoComentario()
    {
        Texto = string.Empty;
    }

    internal SugestaoComentario(Guid sugestaoId, Guid usuarioId, string texto, DateTime em) : base(null)
    {
        SugestaoId = sugestaoId;
        UsuarioId = usuarioId;
        Texto = texto;
        Em = em;
    }
}

/// <summary>
/// RN-SG-05: imagem anexada (print da tela, foto do papel). Até <see cref="Sugestao.ImagensMaximo"/> por sugestão, JPEG,
/// PNG ou WebP de até <see cref="TamanhoMaximoBytes"/> (o painel reduz a foto antes de enviar). Pode ser tirada enquanto a
/// sugestão não está encerrada.
/// </summary>
public sealed class SugestaoImagem : Entity
{
    public const int TamanhoMaximoBytes = 2 * 1024 * 1024;
    public const int NomeMaximo = 120;

    public static readonly IReadOnlyList<string> TiposAceitos = ["image/jpeg", "image/png", "image/webp"];

    public Guid SugestaoId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string NomeArquivo { get; private set; }
    public string TipoConteudo { get; private set; }
    public int TamanhoBytes { get; private set; }
    public byte[] Conteudo { get; private set; }
    public DateTime EnviadaEm { get; private set; }

    // EF Core
    private SugestaoImagem()
    {
        NomeArquivo = string.Empty;
        TipoConteudo = string.Empty;
        Conteudo = [];
    }

    private SugestaoImagem(Guid? id) : base(id)
    {
        NomeArquivo = string.Empty;
        TipoConteudo = string.Empty;
        Conteudo = [];
    }

    /// <param name="imagensJaAnexadas">Quantas a sugestão já tem (o limite é por sugestão).</param>
    public static SugestaoImagem Anexar(
        Sugestao sugestao, int imagensJaAnexadas, string? nomeArquivo, string? tipoConteudo, byte[] conteudo, Guid usuarioId,
        DateTime agoraUtc, Guid? id = null)
    {
        Guard.Contra(sugestao is null, "Informe a sugestão.");
        Guard.Contra(sugestao!.Encerrada, "Esta sugestão já foi encerrada: não dá para anexar imagens.");
        Guard.Contra(imagensJaAnexadas >= Sugestao.ImagensMaximo, $"Cada sugestão aceita até {Sugestao.ImagensMaximo} imagens.");
        var tipo = (tipoConteudo ?? string.Empty).Trim().ToLowerInvariant();
        Guard.Contra(!TiposAceitos.Contains(tipo), "Envie uma imagem JPG, PNG ou WebP.");
        Guard.Contra(conteudo is null || conteudo.Length == 0, "A imagem está vazia.");
        Guard.Contra(conteudo!.Length > TamanhoMaximoBytes, "A imagem passa de 2 MB. Tire um print menor ou reduza a foto.");
        Guard.NaoVazio(usuarioId, "quem enviou");
        Guard.Utc(agoraUtc, "a data da imagem");

        var nome = string.IsNullOrWhiteSpace(nomeArquivo) ? "imagem" : Path.GetFileName(nomeArquivo.Trim());
        if (nome.Length > NomeMaximo)
        {
            nome = nome[^NomeMaximo..];
        }

        return new SugestaoImagem(id)
        {
            SugestaoId = sugestao.Id,
            UsuarioId = usuarioId,
            NomeArquivo = nome,
            TipoConteudo = tipo,
            TamanhoBytes = conteudo.Length,
            Conteudo = conteudo,
            EnviadaEm = agoraUtc,
        };
    }
}

/// <summary>Imagem sem o conteúdo, para a tela da sugestão.</summary>
public sealed record SugestaoImagemInfo(Guid Id, Guid SugestaoId, string NomeArquivo, string TipoConteudo, int TamanhoBytes, DateTime EnviadaEm);
