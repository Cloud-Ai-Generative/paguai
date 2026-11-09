#!/usr/bin/env bash
# Verifica se a sua máquina está pronta para os laboratórios da PagUai.
ok=0
check() {
  if command -v "$1" >/dev/null 2>&1; then
    printf '  [OK]    %-16s %s\n' "$1" "$($2 2>/dev/null | head -n 1)"
  else
    printf '  [FALTA] %-16s %s\n' "$1" "$3"; ok=1
  fi
}
echo "PagUai: verificação do ambiente"
check git    "git --version"    "instale em https://git-scm.com"
check docker "docker --version" "instale o Docker Desktop ou o Docker Engine"
if docker compose version >/dev/null 2>&1; then
  printf '  [OK]    %-16s %s\n' "docker compose" "$(docker compose version)"
else
  printf '  [FALTA] %-16s %s\n' "docker compose" "atualize o Docker"; ok=1
fi
if docker info >/dev/null 2>&1; then
  echo "  [OK]    Docker está em execução"
else
  echo "  [FALTA] O Docker está instalado mas não está em execução"; ok=1
fi
check dotnet "dotnet --version" "opcional para rodar fora do Docker: https://dotnet.microsoft.com/download/dotnet/10.0"
if [ -r /proc/meminfo ]; then
  mem=$(awk '/MemTotal/ {printf "%.0f", $2/1024/1024}' /proc/meminfo)
  [ "$mem" -lt 8 ] && echo "  [ATENÇÃO] ${mem} GB de RAM: considere usar o GitHub Codespaces" || echo "  [OK]    ${mem} GB de RAM"
fi
[ $ok -eq 0 ] && echo "Tudo pronto! Rode: docker compose up -d --build" || echo "Corrija os itens marcados com [FALTA]."
exit $ok
