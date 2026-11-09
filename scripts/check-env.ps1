# Verifica se a sua máquina (Windows) está pronta para os laboratórios da PagUai.
$falhou = $false
function Testar($nome, $comando, $dica) {
    if (Get-Command $nome -ErrorAction SilentlyContinue) {
        $versao = (& $nome $comando 2>$null | Select-Object -First 1)
        Write-Host ("  [OK]    {0,-16} {1}" -f $nome, $versao)
    } else {
        Write-Host ("  [FALTA] {0,-16} {1}" -f $nome, $dica) -ForegroundColor Yellow
        $script:falhou = $true
    }
}
Write-Host "PagUai: verificação do ambiente"
Testar "git" "--version" "instale em https://git-scm.com"
Testar "docker" "--version" "instale o Docker Desktop"
docker compose version *> $null
if ($LASTEXITCODE -eq 0) { Write-Host "  [OK]    docker compose" } else { Write-Host "  [FALTA] docker compose" -ForegroundColor Yellow; $falhou = $true }
docker info *> $null
if ($LASTEXITCODE -eq 0) { Write-Host "  [OK]    Docker está em execução" } else { Write-Host "  [FALTA] Abra o Docker Desktop" -ForegroundColor Yellow; $falhou = $true }
Testar "dotnet" "--version" "opcional para rodar fora do Docker"
$mem = [math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB)
if ($mem -lt 8) { Write-Host "  [ATENÇÃO] $mem GB de RAM: considere usar o GitHub Codespaces" } else { Write-Host "  [OK]    $mem GB de RAM" }
if ($falhou) { Write-Host "Corrija os itens marcados com [FALTA]."; exit 1 } else { Write-Host "Tudo pronto! Rode: docker compose up -d --build" }
