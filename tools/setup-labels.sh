#!/usr/bin/env bash
# Cria/atualiza as labels do repositório (idempotente). Uso: tools/setup-labels.sh [owner/repo]
set -euo pipefail
repo="${1:-NasckDev/vespera}"

label() { gh label create "$1" --repo "$repo" --color "$2" --description "$3" --force >/dev/null && echo "✓ $1"; }

label "bug"              "E64A8E" "Algo não funciona"
label "ideia"            "F2A541" "Proposta de mecânica ou conteúdo"
label "triagem"          "6B6F8A" "Aguardando análise dos mantenedores"
label "good first issue" "52E0C4" "Boa para quem está começando"
label "help wanted"      "5B8DEF" "Precisamos de ajuda aqui"
label "arte"             "9B6BD6" "Sprites, mapas, UI, FX"
label "áudio"            "4A2C6B" "Música e efeitos sonoros"
label "combate"          "E85D5D" "Armas, monstros, Guardiões"
label "cidade"           "5C8C4A" "Lumeira, NPCs, mapas"
label "progressão"       "F4E7C6" "Ranks, XP, forja, itens"
label "ui"               "B4C2D4" "HUD, menus, diálogos"
label "core"             "1B1F3B" "Lógica pura em src/Core"
label "ci/build"         "8A8FA8" "CI, export, instalador"
label "docs"             "3C7A8A" "Documentação"
label "decisão"          "8A5A3C" "Precisa de decisão do time"
