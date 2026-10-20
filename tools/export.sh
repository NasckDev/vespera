#!/usr/bin/env bash
# Exporta as 3 plataformas para build/. Requer os export templates .NET da mesma versão do Godot.
# Uso: tools/export.sh [windows|linux|mac]   (sem argumento exporta todas)
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
: "${GODOT:?Defina GODOT com o caminho do executável do Godot .NET 4.7.2}"

export_one() {
  local preset="$1" output="$2"
  mkdir -p "$root/$(dirname "$output")"
  echo "→ $preset"
  "$GODOT" --headless --path "$root" --export-release "$preset" "$root/$output"
}

"$GODOT" --headless --path "$root" --import

case "${1:-all}" in
  windows) export_one "Windows Desktop" build/windows/Vespera.exe ;;
  linux)   export_one "Linux" build/linux/Vespera.x86_64 ;;
  mac)     export_one "macOS" build/macos/Vespera.zip ;;
  all)
    export_one "Windows Desktop" build/windows/Vespera.exe
    export_one "Linux" build/linux/Vespera.x86_64
    export_one "macOS" build/macos/Vespera.zip
    ;;
  *) echo "Plataforma desconhecida: $1" >&2; exit 1 ;;
esac
