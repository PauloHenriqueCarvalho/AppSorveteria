using GestaoSorveteria.Domain.Pix;

namespace GestaoSorveteria.Infrastructure.Pix;

/// <summary>
/// RN-PX-01: configuração do Pix integrado (seção <c>Pix</c>; no Render, <c>Pix__Provedor</c>, <c>Pix__ClientId</c>… — docs/07).
/// Tudo aqui é segredo do servidor: nunca vai para o app, o painel, o Git ou o log.
/// </summary>
public sealed class PixOptions
{
    public const string Secao = "Pix";

    public const string ProvedorInter = "Inter";
    public const string ProvedorSimulado = "Simulado";

    /// <summary>"Inter", "Simulado" (só em Development) ou vazio = desligado.</summary>
    public string Provedor { get; set; } = string.Empty;

    /// <summary>"Producao" ou "Sandbox" (o sandbox do Inter tem credenciais próprias).</summary>
    public string Ambiente { get; set; } = "Producao";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Conteúdo do arquivo .crt da integração (PEM, "-----BEGIN CERTIFICATE-----…"). Vale 1 ano.</summary>
    public string CertificadoPem { get; set; } = string.Empty;

    /// <summary>Conteúdo do arquivo .key da integração (PEM, "-----BEGIN PRIVATE KEY-----…").</summary>
    public string ChavePrivadaPem { get; set; } = string.Empty;

    /// <summary>Número da conta corrente (opcional; só quando a integração enxerga mais de uma conta).</summary>
    public string ContaCorrente { get; set; } = string.Empty;

    /// <summary>Chave Pix que recebe (CNPJ, e-mail, telefone ou aleatória) — a da conta da integração.</summary>
    public string Chave { get; set; } = string.Empty;

    public int ExpiracaoSegundos { get; set; } = CobrancaPix.ExpiracaoPadraoSegundos;

    /// <summary>Simulado: a cobrança é dada como paga depois destes segundos (para testar o app sem banco).</summary>
    public int SimuladoPagaEmSegundos { get; set; } = 8;

    /// <summary>O que <see cref="Normalizar"/> corrigiu ou desligou (vai para o log ao subir, sem segredos).</summary>
    public IReadOnlyList<string> Avisos { get; private set; } = [];

    public bool Sandbox => string.Equals(Ambiente, "Sandbox", StringComparison.OrdinalIgnoreCase);

    /// <summary>Aviso descoberto depois de <see cref="Normalizar"/> (ex.: certificado que não abriu); desliga o Inter.</summary>
    public void RegistrarAviso(string aviso)
    {
        Avisos = [.. Avisos, aviso];
        Provedor = string.Empty;
    }

    /// <summary>
    /// O Pix integrado é opcional: configuração errada não pode impedir a API de subir nem a venda (o app volta ao Pix à mão).
    /// Simulado só em Development (lista de permissão: um "Staging" ou nome errado no ambiente não liga) — ele "confirma"
    /// pagamentos que não existem.
    /// </summary>
    public IReadOnlyList<string> Normalizar(bool permitirSimulado)
    {
        var avisos = new List<string>();
        Provedor = (Provedor ?? string.Empty).Trim();
        if (ExpiracaoSegundos is < CobrancaPix.ExpiracaoMinimaSegundos or > CobrancaPix.ExpiracaoMaximaSegundos)
        {
            avisos.Add($"Pix:ExpiracaoSegundos fora de {CobrancaPix.ExpiracaoMinimaSegundos}–{CobrancaPix.ExpiracaoMaximaSegundos}; usando {CobrancaPix.ExpiracaoPadraoSegundos}.");
            ExpiracaoSegundos = CobrancaPix.ExpiracaoPadraoSegundos;
        }

        if (SimuladoPagaEmSegundos is < 1 or > 300)
        {
            SimuladoPagaEmSegundos = 8;
        }

        if (string.Equals(Provedor, ProvedorSimulado, StringComparison.OrdinalIgnoreCase))
        {
            Provedor = ProvedorSimulado;
            if (!permitirSimulado)
            {
                avisos.Add("Pix:Provedor = Simulado só é aceito em Development; Pix integrado desligado.");
                Provedor = string.Empty;
            }
        }
        else if (string.Equals(Provedor, ProvedorInter, StringComparison.OrdinalIgnoreCase))
        {
            Provedor = ProvedorInter;
            var faltando = new[]
            {
                (nameof(ClientId), ClientId), (nameof(ClientSecret), ClientSecret), (nameof(CertificadoPem), CertificadoPem),
                (nameof(ChavePrivadaPem), ChavePrivadaPem), (nameof(Chave), Chave),
            }.Where(c => string.IsNullOrWhiteSpace(c.Item2)).Select(c => "Pix:" + c.Item1).ToList();
            if (faltando.Count > 0)
            {
                avisos.Add($"Pix integrado (Inter) desligado: falta {string.Join(", ", faltando)}.");
                Provedor = string.Empty;
            }
        }
        else if (Provedor.Length > 0)
        {
            avisos.Add($"Pix:Provedor = '{Provedor}' desconhecido (use Inter ou Simulado); Pix integrado desligado.");
            Provedor = string.Empty;
        }

        Avisos = [.. Avisos, .. avisos];
        return avisos;
    }
}
