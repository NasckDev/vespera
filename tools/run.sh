#!/usr/bin/env bash
# Compila e roda o Véspera. Uso: tools/run.sh [--editor | --smoke]
# Defina GODOT com o caminho do Godot .NET 4.7.2.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
: "${GODOT:?Defina GODOT com o caminho do executável do Godot .NET 4.7.2}"

dotnet build "$root/Vespera.sln"

case "${1:-}" in
  --editor) exec "$GODOT" --path "$root" --editor ;;
  --smoke)  exec "$GODOT" --headless --path "$root" -- --smoke-test ;;
  *)        exec "$GODOT" --path "$root" ;;
esac
