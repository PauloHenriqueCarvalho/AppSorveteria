using System.ComponentModel.DataAnnotations;

namespace GestaoSorveteria.Contracts.Sugestoes;

/// <summary>
/// POST /api/sugestoes e PUT /api/sugestoes/{id} (Admin) — RN-SG-01/02. <paramref name="Tipo"/>: Ideia, Mudanca, Problema
/// ou Duvida; <paramref name="Area"/>: Balcao, Painel, Estoque, Relatorios ou Outro; <paramref name="Importancia"/>: Baixa,
/// Media ou Alta.
/// </summary>
public sealed record SalvarSugestaoRequest(
    [Required(ErrorMessage = "Escreva um título.")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "O título deve ter de 3 a 120 caracteres.")]
    string Titulo,
    [Required(ErrorMessage = "Descreva a sugestão.")]
    [StringLength(4000, ErrorMessage = "A descrição deve ter no máximo 4000 caracteres.")]
    string Descricao,
    [Required(ErrorMessage = "Escolha o tipo.")] string Tipo,
    [Required(ErrorMessage = "Escolha a parte do sistema.")] string Area,
    [Required(ErrorMessage = "Escolha a importância.")] string Importancia);

/// <summary>POST /api/sugestoes/{id}/comentarios (Admin) — RN-SG-04.</summary>
public sealed record ComentarSugestaoRequest(
    [Required(ErrorMessage = "Escreva a mensagem.")]
    [StringLength(2000, ErrorMessage = "A mensagem deve ter no máximo 2000 caracteres.")]
    string Texto);

/// <summary>
/// POST /api/sugestoes/{id}/status (Admin) — RN-SG-03. <paramref name="Status"/>: Nova, EmAnalise, Planejada, Feita ou
/// Recusada (que pede o <paramref name="Comentario"/>).
/// </summary>
public sealed record AlterarStatusSugestaoRequest(
    [Required(ErrorMessage = "Escolha o andamento.")] string Status,
    [StringLength(1900, ErrorMessage = "O comentário deve ter no máximo 1900 caracteres.")] string? Comentario);

public sealed record SugestaoResumoDto(
    Guid Id,
    string Titulo,
    string Tipo,
    string Area,
    string Importancia,
    string Status,
    string CriadaPorNome,
    DateTime CriadaEm,
    DateTime AtualizadaEm,
    int Comentarios,
    int Imagens);

public sealed record SugestaoComentarioDto(Guid Id, Guid UsuarioId, string UsuarioNome, string Texto, DateTime Em);

/// <summary>Imagem sem o conteúdo: o conteúdo vem de GET /api/sugestoes/{id}/imagens/{imagemId}.</summary>
public sealed record SugestaoImagemDto(Guid Id, string NomeArquivo, string TipoConteudo, int TamanhoBytes, DateTime EnviadaEm);

public sealed record SugestaoDto(
    Guid Id,
    string Titulo,
    string Descricao,
    string Tipo,
    string Area,
    string Importancia,
    string Status,
    bool Encerrada,
    Guid CriadaPorUsuarioId,
    string CriadaPorNome,
    DateTime CriadaEm,
    DateTime AtualizadaEm,
    IReadOnlyList<SugestaoComentarioDto> Comentarios,
    IReadOnlyList<SugestaoImagemDto> Imagens);
