#!/usr/bin/env python3
"""Regrava a seção "musica" de data/audio/sons.json a partir dos mapas existentes em data/levels.

Uso (da raiz do repositório):  python -I tools/audio/mapear_musica.py

Trilha original gerada com ACE-Step 1.5 (MIT) em assets/v2/audio/musica_original/ (ver LEIA-ME.md lá).
Regra: bairro → tema do bairro; sala de Brecha → "brecha"; arena (`*_arena`) → "guardiao"; final → "final".
Faixa que ainda não existe na trilha original cai na antiga (assets/v2/audio/musica/, Ninja Adventure CC0).
Rode de novo sempre que um mapa novo entrar.
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SONS = ROOT / "data" / "audio" / "sons.json"
LEVELS = ROOT / "data" / "levels"
NOVA = "res://assets/v2/audio/musica_original/"
ANTIGA = "res://assets/v2/audio/musica/"

# nome do mapa -> faixa
BAIRROS = {
    "estrada": "praca", "praca": "praca", "taverna": "praca", "quarto": "praca",
    "forjas": "forjas", "forja_interior": "forjas",
    "pomar_alto": "pomar", "mares": "cais", "cemiterio": "cemiterio", "torre": "torre",
    "ruas_baixas": "ruas", "praca_baixo": "final",
}
TELAS = {
    "title": "titulo", "options": "titulo", "controls": "titulo", "help": "titulo",
    "slots_new": "titulo", "slots_continue": "titulo", "credits": "final",
    "tensao": "brecha", "melancolia": "ruas", "vitoria": "final", "misterio": "cemiterio",
}


def faixa(nome: str) -> str:
    original = ROOT / "assets" / "v2" / "audio" / "musica_original" / f"{nome}.ogg"
    return NOVA + f"{nome}.ogg" if original.exists() else ANTIGA + f"{nome}.ogg"


def faixa_do_mapa(mapa: str) -> str:
    if mapa in BAIRROS:
        return faixa(BAIRROS[mapa])
    if mapa.endswith("_arena"):
        return faixa("guardiao")
    return faixa("brecha")


def main() -> None:
    sons = json.loads(SONS.read_text(encoding="utf-8"))
    musica = {"intro": "", "prologue": ""}
    musica.update({tela: faixa(nome) for tela, nome in TELAS.items()})
    for arquivo in sorted(LEVELS.glob("*.json")):
        if arquivo.stem != "props":
            musica[arquivo.stem] = faixa_do_mapa(arquivo.stem)
    sons["musica"] = musica
    SONS.write_text(json.dumps(sons, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{len(musica)} entradas de música")


if __name__ == "__main__":
    main()
