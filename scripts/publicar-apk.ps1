<#
.SYNOPSIS
    Gera o APK assinado do app do atendente (Android) para instalar no celular da sorveteria.

.DESCRIPTION
    1. Localiza o keytool (JDK) e, se o keystore ainda não existir e -CriarKeystore for informado, cria um.
    2. Roda "dotnet publish -f net10.0-android -c Release" assinando com o keystore.
    3. Copia o APK para -Saida com o nome "sorveteria-<versão>.apk" e mostra os próximos passos
       (publicar o link e atualizar App__VersaoAtual / App__LinkApk no Render — docs/07, seção "APK assinado").

    A senha do keystore NUNCA fica neste arquivo, no Git nem na linha de comando: vem da variável de
    ambiente SORVETERIA_KEYSTORE_SENHA ou de uma pergunta na tela (sem mostrar o que é digitado), e é
    passada ao keytool e ao MSBuild pelo ambiente ("-storepass:env", "env:VARIAVEL").

    O keystore fica FORA do repositório (padrão: %USERPROFILE%\.sorveteria\sorveteria.keystore).
    Guarde uma cópia dele e da senha em lugar seguro: sem o mesmo keystore não dá para atualizar o app
    instalado — só desinstalando (e perdendo o que ainda não foi sincronizado).

.PARAMETER ApiUrl
    URL da API publicada (https, sem barra no fim), ex.: https://sorveteria-api.onrender.com. Fica gravada no APK
    (-p:ApiUrlPadrao): no Release o app não tem campo de servidor e só fala https (docs/07, "App do atendente").
.PARAMETER Versao
    Versão mostrada ao usuário e comparada com GET /api/versao (ApplicationDisplayVersion), ex.: 1.1.
    Padrão: a do .csproj.
.PARAMETER Build
    Número de build do Android (ApplicationVersion / versionCode). Tem que ser MAIOR que o do APK
    anterior instalado, senão o celular recusa a atualização. Padrão: o do .csproj.
.PARAMETER Keystore
    Caminho do keystore. Padrão: %USERPROFILE%\.sorveteria\sorveteria.keystore.
.PARAMETER Alias
    Nome da chave dentro do keystore. Padrão: sorveteria.
.PARAMETER Saida
    Pasta onde o APK final é copiado. Padrão: %USERPROFILE%\Builds\sorveteria.
.PARAMETER CriarKeystore
    Cria o keystore (keytool -genkeypair, RSA 2048, válido por 30 anos) se ele ainda não existir.
.PARAMETER Keytool
    Caminho do keytool.exe, se o script não achar sozinho (JAVA_HOME, PATH, JDK do Android Studio, JDK da Microsoft).

.EXAMPLE
    .\scripts\publicar-apk.ps1 -ApiUrl https://sorveteria-api.onrender.com -CriarKeystore        # primeira vez
    .\scripts\publicar-apk.ps1 -ApiUrl https://sorveteria-api.onrender.com -Versao 1.1 -Build 2   # versões seguintes
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^https://[^\s/]+$')][string]$ApiUrl,
    [ValidatePattern('^\d+\.\d+$')][string]$Versao = '',
    [ValidateRange(1, 2100000000)][int]$Build = 0,
    [string]$Keystore = (Join-Path $HOME '.sorveteria\sorveteria.keystore'),
    [string]$Alias = 'sorveteria',
    [string]$Saida = (Join-Path $HOME 'Builds\sorveteria'),
    [switch]$CriarKeystore,
    [string]$Keytool = ''
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
$csproj = Join-Path $raiz 'src\GestaoSorveteria.Mobile\GestaoSorveteria.Mobile.csproj'
if (-not (Test-Path $csproj)) { throw "Não achei o projeto do app em $csproj. Rode o script de dentro do repositório." }

# ---------- 1. Versão e build (padrão: o que está no .csproj) ----------
[xml]$projeto = Get-Content $csproj
$grupo = $projeto.Project.PropertyGroup | Where-Object { $_.ApplicationDisplayVersion } | Select-Object -First 1
if (-not $Versao) { $Versao = [string]$grupo.ApplicationDisplayVersion }
if ($Build -eq 0) { $Build = [int]$grupo.ApplicationVersion }
if (-not $Versao -or $Build -eq 0) { throw 'Não consegui ler ApplicationDisplayVersion/ApplicationVersion do .csproj. Informe -Versao e -Build.' }

# ---------- 2. keytool ----------
function Resolve-Keytool {
    if ($Keytool) {
        if (Test-Path $Keytool) { return $Keytool }
        throw "Não achei keytool em '$Keytool'."
    }
    $candidatos = @()
    if ($env:JAVA_HOME) { $candidatos += Join-Path $env:JAVA_HOME 'bin\keytool.exe' }
    $noPath = Get-Command keytool -ErrorAction SilentlyContinue
    if ($noPath) { $candidatos += $noPath.Source }
    $candidatos += 'C:\Program Files\Android\Android Studio\jbr\bin\keytool.exe'
    $candidatos += Get-ChildItem 'C:\Program Files\Microsoft', 'C:\Program Files (x86)\Android\openjdk', 'C:\Program Files\Java' -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like 'jdk*' } | Sort-Object Name -Descending | ForEach-Object { Join-Path $_.FullName 'bin\keytool.exe' }
    $achado = $candidatos | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if ($achado) { return $achado }
    throw 'Não achei o keytool (vem com qualquer JDK: Android Studio, Microsoft OpenJDK ou Visual Studio com a carga do MAUI). Informe -Keytool "C:\...\bin\keytool.exe".'
}

# ---------- 3. Senha (só em memória e no ambiente dos processos filhos) ----------
if (-not $env:SORVETERIA_KEYSTORE_SENHA) {
    $segura = Read-Host -AsSecureString 'Senha do keystore (não aparece na tela)'
    $env:SORVETERIA_KEYSTORE_SENHA = [System.Net.NetworkCredential]::new('', $segura).Password
    $senhaVeioDaTela = $true
}
if ($env:SORVETERIA_KEYSTORE_SENHA.Length -lt 6) { throw 'A senha do keystore precisa ter pelo menos 6 caracteres (exigência do keytool).' }

try {
    # ---------- 4. Keystore ----------
    if (-not (Test-Path $Keystore)) {
        if (-not $CriarKeystore) {
            throw "Keystore não encontrado em '$Keystore'. Primeira vez? Rode com -CriarKeystore. Já tem um? Informe -Keystore <caminho> (ou copie-o para a pasta padrão)."
        }
        $keytoolExe = Resolve-Keytool
        New-Item -ItemType Directory -Force -Path (Split-Path $Keystore -Parent) | Out-Null
        Write-Host "Criando o keystore em $Keystore (keytool: $keytoolExe)"
        & $keytoolExe -genkeypair -v -keystore $Keystore -alias $Alias -keyalg RSA -keysize 2048 -validity 10950 `
            -storepass:env SORVETERIA_KEYSTORE_SENHA -keypass:env SORVETERIA_KEYSTORE_SENHA `
            -dname 'CN=Gestao Sorveteria, OU=App do atendente, O=Sorveteria, C=BR'
        if ($LASTEXITCODE -ne 0) { throw "keytool terminou com código $LASTEXITCODE." }
        Write-Host 'Keystore criado. FAÇA UMA CÓPIA dele e guarde a senha (gerenciador de senhas / pendrive).' -ForegroundColor Yellow
    }

    # ---------- 5. Publicar (Release, Android, assinado) ----------
    $publicacao = Join-Path $raiz 'src\GestaoSorveteria.Mobile\bin\publish-android'
    if (Test-Path $publicacao) { Remove-Item $publicacao -Recurse -Force }
    Write-Host "Publicando versão $Versao (build $Build) assinada com '$Alias', API em $ApiUrl..."
    & dotnet publish $csproj -f net10.0-android -c Release -p:TargetFrameworks=net10.0-android `
        -p:ApplicationDisplayVersion=$Versao -p:ApplicationVersion=$Build -p:ApiUrlPadrao=$ApiUrl `
        -p:AndroidPackageFormats=apk -p:AndroidKeyStore=true `
        -p:AndroidSigningKeyStore="$Keystore" -p:AndroidSigningKeyAlias=$Alias `
        -p:AndroidSigningKeyPass=env:SORVETERIA_KEYSTORE_SENHA -p:AndroidSigningStorePass=env:SORVETERIA_KEYSTORE_SENHA `
        -o $publicacao
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish terminou com código $LASTEXITCODE." }
}
finally {
    if ($senhaVeioDaTela) { Remove-Item Env:\SORVETERIA_KEYSTORE_SENHA -ErrorAction SilentlyContinue }
}

# ---------- 6. Copiar o APK assinado com nome amigável ----------
$apk = Get-ChildItem $publicacao -Filter '*-Signed.apk' | Select-Object -First 1
if (-not $apk) { throw "O publish terminou, mas não achei *-Signed.apk em $publicacao." }
New-Item -ItemType Directory -Force -Path $Saida | Out-Null
$destino = Join-Path $Saida "sorveteria-$Versao.apk"
Copy-Item $apk.FullName $destino -Force
$tamanhoMb = [math]::Round((Get-Item $destino).Length / 1MB, 1)

Write-Host ''
Write-Host "APK assinado: $destino ($tamanhoMb MB)" -ForegroundColor Green
Write-Host 'Próximos passos (docs/07, seção "APK assinado"):'
Write-Host '  1. Instalar no celular (link ou cabo USB) e abrir o app para conferir a versão.'
Write-Host "  2. Publicar o arquivo no link fixo e, no Render, colocar App__VersaoAtual=$Versao e App__LinkApk=<link>."
Write-Host "  3. Se o app antigo não puder mais ser usado, App__VersaoMinima=$Versao."
Write-Host "  4. Commitar no .csproj: ApplicationDisplayVersion=$Versao e ApplicationVersion=$Build."
