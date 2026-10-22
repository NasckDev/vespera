# tools/art — pipeline de pixel art da v2 (prova: Lâmina aprendiz)

```
python -m venv tools/art/.venv
tools/art/.venv/Scripts/python -m pip install -r tools/art/requirements.txt
tools/art/.venv/Scripts/python -I tools/art/build_lamina.py      # gera assets/v2/characters/lamina/
tools/art/.venv/Scripts/python -I tools/art/verify_palette.py    # todo pixel tem de ser da paleta
```

- `palette.json`: paleta mestre (rampas com desvio de matiz; cores de marca/cena do PLANO §2) e legenda dos mapas de texto.
- `pixcore.py`: mapas de texto -> índices, blit em posição inteira, contorno seletivo, sombra de separação.
- `parts_body.py` (cabeças, mãos, pés), `parts_clothes.py` (tronco do traje + capa procedural),
  `parts_gear.py` (espada em 16 ângulos de linha limpa, lanterna, rastros, faíscas), `parts_poses.py` (bola do rolamento, corpo caído).
- `rig.py`: monta o quadro 48x48 por camadas body/clothes/cape/weapon/lantern/aura/fx. Trocar estágio = trocar o `Costume`.
- `figure.py`/`skeleton.py`: juntas por direção; `anims_*.py`: poses quadro a quadro; `export.py`: folhas, `.tres`, `anims.json`, GIF, contato.

Âncora: pé em (24, 41) da célula (Godot: `centered=false`, `offset=(-24,-41)`). Esquerda = `side` com `flip_h`
(espelho em torno da coluna 24; ver `anchor_note` em `anims.json`). `.lab/` é rascunho local (ignorado).
