<#
.SYNOPSIS
    Backup do banco da sorveteria (PostgreSQL no Neon) para uma pasta local, com pg_dump.

.DESCRIPTION
    Gera um arquivo "sorveteria-AAAA-MM-DD-HHMM.dump" (formato custom do pg_dump, restaurável com
    pg_restore), confere que o arquivo abre e apaga os mais antigos além de -Manter.
    Como restaurar: docs/07-hospedagem.md, seção "Backup no piloto".

    A string de conexão NUNCA fica neste arquivo nem no Git. Ela vem, nesta ordem:
      1. parâmetro -Url;
      2. variável de ambiente SORVETERIA_DB_URL (definida só no PC do Paulo);
      3. pergunta na tela, sem mostrar o que é digitado.
    Aceita o formato URL (postgresql://usuario:senha@host/banco?sslmode=require, copiado do Neon)
    ou o formato Npgsql (Host=...;Username=...;Password=...;Database=...;SSL Mode=Require).
    No Neon, use o endpoint DIRETO (sem "-pooler" no host): o pg_dump não funciona bem pelo pooler.

.PARAMETER Pasta
    Onde guardar os backups. Padrão: $HOME\Backups\sorveteria. Guarde também fora do PC (pendrive/nuvem pessoal).
.PARAMETER Manter
    Quantos arquivos manter na pasta (os mais antigos são apagados). Padrão: 12 (~3 meses de backup semanal).
.PARAMETER PgBin
    Pasta do pg_dump, se não estiver no PATH. Padrão: procura no PATH e em C:\Program Files\PostgreSQL\<versão>\bin.
.PARAMETER Url
    String de conexão (ver acima). Prefira a variável de ambiente para não deixar a senha no histórico do terminal.

.EXAMPLE
    .\scripts\backup.ps1
    .\scripts\backup.ps1 -Pasta D:\Backups\sorveteria -Manter 20
#>
[CmdletBinding()]
param(
    [string]$Pasta = (Join-Path $HOME 'Backups\sorveteria'),
    [ValidateRange(1, 1000)][int]$Manter = 12,
    [string]$PgBin = '',
    [string]$Url = ''
)

$ErrorActionPreference = 'Stop'

# ---------- 1. Localizar o pg_dump / pg_restore ----------
function Resolve-PgTool([string]$nome) {
    if ($PgBin) {
        $caminho = Join-Path $PgBin "$nome.exe"
        if (Test-Path $caminho) { return $caminho }
        throw "Não achei $nome.exe em '$PgBin'."
    }
    $noPath = Get-Command $nome -ErrorAction SilentlyContinue
    if ($noPath) { return $noPath.Source }
    # Versão mais nova instalada (pasta C:\Program Files\PostgreSQL\<versão>\bin).
    $instalado = Get-ChildItem 'C:\Program Files\PostgreSQL' -Directory -ErrorAction SilentlyContinue |
        Sort-Object { [int]$_.Name } -Descending |
        ForEach-Object { Join-Path $_.FullName "bin\$nome.exe" } |
        Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($instalado) { return $instalado }
    throw "Não achei $nome. Instale o PostgreSQL (só as 'Command Line Tools' bastam) ou informe -PgBin 'C:\Program Files\PostgreSQL\16\bin'."
}

$pgDump = Resolve-PgTool 'pg_dump'
$pgRestore = Resolve-PgTool 'pg_restore'

# ---------- 2. Obter a string de conexão (sem gravar em lugar nenhum) ----------
if (-not $Url) { $Url = $env:SORVETERIA_DB_URL }
if (-not $Url) {
    $segura = Read-Host -AsSecureString 'Cole a string de conexão do Neon (não aparece na tela)'
    $Url = [System.Net.NetworkCredential]::new('', $segura).Password
}
if (-not $Url) { throw 'String de conexão vazia.' }

# ---------- 3. Separar host, porta, usuário, senha e banco ----------
$con = @{ Host = ''; Port = '5432'; User = ''; Password = ''; Database = ''; SslMode = 'require' }
if ($Url -match '^postgres(ql)?://') {
    $uri = [Uri]$Url
    $con.Host = $uri.Host
    if ($uri.Port -gt 0) { $con.Port = "$($uri.Port)" }
    $userInfo = $uri.UserInfo -split ':', 2
    $con.User = [Uri]::UnescapeDataString($userInfo[0])
    if ($userInfo.Length -gt 1) { $con.Password = [Uri]::UnescapeDataString($userInfo[1]) }
    $con.Database = [Uri]::UnescapeDataString($uri.AbsolutePath.TrimStart('/'))
    if ($uri.Query -match 'sslmode=([^&]+)') { $con.SslMode = $Matches[1] }
}
else {
    foreach ($par in ($Url -split ';')) {
        if (-not $par.Trim()) { continue }
        $chave, $valor = $par -split '=', 2
        switch -Regex ($chave.Trim()) {
            '^(host|server)$'      { $con.Host = $valor.Trim() }
            '^port$'               { $con.Port = $valor.Trim() }
            '^(username|user id|user|uid)$' { $con.User = $valor.Trim() }
            '^(password|pwd)$'     { $con.Password = $valor.Trim() }
            '^database$'           { $con.Database = $valor.Trim() }
            '^ssl ?mode$'          { $con.SslMode = $valor.Trim().ToLowerInvariant() }
        }
    }
}
foreach ($campo in 'Host', 'User', 'Database') {
    if (-not $con[$campo]) { throw "Não consegui ler '$campo' da string de conexão. Confira o formato (URL do Neon ou Npgsql)." }
}
if ($con.Host -like '*-pooler.*') {
    Write-Warning 'O host é o pooler do Neon. Para backup use o endpoint direto (sem "-pooler").'
}

# ---------- 4. Gerar o arquivo ----------
New-Item -ItemType Directory -Force -Path $Pasta | Out-Null
$arquivo = Join-Path $Pasta ("sorveteria-{0}.dump" -f (Get-Date -Format 'yyyy-MM-dd-HHmm'))

# A senha vai só no ambiente do processo filho, nunca na linha de comando (ficaria visível no Gerenciador de Tarefas).
$env:PGPASSWORD = $con.Password
$env:PGSSLMODE = $con.SslMode
try {
    Write-Host "Fazendo backup de '$($con.Database)' em $($con.Host) -> $arquivo"
    & $pgDump --host $con.Host --port $con.Port --username $con.User --dbname $con.Database `
        --format=custom --no-owner --no-privileges --file $arquivo
    if ($LASTEXITCODE -ne 0) { throw "pg_dump terminou com código $LASTEXITCODE." }
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:\PGSSLMODE -ErrorAction SilentlyContinue
}

# ---------- 5. Conferir que o arquivo abre (lista o conteúdo sem restaurar) ----------
$tabelas = (& $pgRestore --list $arquivo | Select-String ' TABLE DATA ').Count
if ($LASTEXITCODE -ne 0 -or $tabelas -eq 0) {
    throw "O arquivo gerado não passou na conferência (pg_restore --list). Não confie neste backup."
}
$tamanhoKb = [math]::Round((Get-Item $arquivo).Length / 1KB, 1)
Write-Host "OK: $tabelas tabela(s) com dados, $tamanhoKb KB." -ForegroundColor Green

# ---------- 6. Apagar os mais antigos além de -Manter ----------
$antigos = Get-ChildItem $Pasta -Filter 'sorveteria-*.dump' | Sort-Object Name -Descending | Select-Object -Skip $Manter
foreach ($velho in $antigos) {
    Remove-Item $velho.FullName
    Write-Host "Apagado backup antigo: $($velho.Name)"
}
Write-Host "Backups na pasta: $((Get-ChildItem $Pasta -Filter 'sorveteria-*.dump').Count) (máximo $Manter)."
