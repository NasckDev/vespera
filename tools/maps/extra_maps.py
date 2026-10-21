#!/usr/bin/env python3
"""
Importa os mapas novos do dono (imagens 41–50, 58, 60, 62, 64, 66 da leva de 2026-10-20) como CANDIDATOS:
salas extras das 5 Brechas atuais e as Brechas 6–10 (nomes da prancha 52). Sem colisão ainda: nenhum entra no
maps.json; o registro fica em src/Data/maps_extra.json (lido por `ExtraMapCatalog` no Core).

As imagens 58/60/62/64/66 têm DOIS mapas lado a lado (1774×887): são cortadas no vão escuro entre eles
(coluna mais escura do terço central; não é o meio exato).

Uso:
  python -I tools/maps/extra_maps.py --src <pasta com NN.png>      grava assets/maps/extra/<id>/map_<id>.png + maps_extra.json
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.dont_write_bytecode = True

from PIL import Image  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
OUT_DIR = ROOT / "assets" / "maps" / "extra"
OUT_JSON = ROOT / "src" / "Data" / "maps_extra.json"

BRECHAS = {
    6: "Biblioteca Submersa",
    7: "Torre Relógio Mecânica",
    8: "Labirinto Alagado das Catacumbas",
    9: "Templo em Ruínas",
    10: "Observatório Astral",
}

# id, imagem de origem, metade (None = inteira, "L"/"R"), papel, alvo (id de mapa existente ou número da Brecha), nota
MAPS = [
    ("extra_fish2_b", "41", None, "extraRoom", "fish2", "Variação do Mercado de Peixes: canais, barcos e barracas"),
    ("extra_crypt2_b", "42", None, "extraRoom", "crypt2", "Variação do Cemitério das Velas: túmulos e brecha violeta"),
    ("extra_mine_b", "43", None, "extraRoom", "mine", "Variação das Minas da Forja: rios de lava e pontes"),
    ("extra_garden_b", "44", None, "extraRoom", "garden", "Variação da Estufa Silvestre: estufas e jardins"),
    ("extra_cath_b", "45", None, "extraRoom", "cath", "Variação da Catedral do Arauto: nave, cristais magenta"),
    ("extra_garden_c", "66", "R", "extraRoom", "garden", "Estufas de vidro em volta de tanques e cascatas"),
    ("brecha6_library", "46", None, "brecha", 6, "Salas alagadas sobre o vazio, água azul"),
    ("brecha6_library_b", "60", "L", "brecha", 6, "Galerias alagadas com tanques e escadarias"),
    ("brecha7_clock", "47", None, "brecha", 7, "Mostrador gigante no centro, engrenagens e vitrais azuis"),
    ("brecha7_clock_b", "60", "R", "brecha", 7, "Câmara do mecanismo com engrenagens expostas"),
    ("brecha7_clock_c", "64", "L", "brecha", 7, "Torre com relógio central, sino e cascatas azuis"),
    ("brecha8_catacombs", "48", None, "brecha", 8, "Catacumbas alagadas em verde-água, câmara circular"),
    ("brecha8_catacombs_b", "62", "L", "brecha", 8, "Corredores alagados com fontes"),
    ("brecha8_catacombs_c", "62", "R", "brecha", 8, "Salão central alagado com cascatas"),
    ("brecha8_catacombs_d", "66", "L", "brecha", 8, "Canais com vitórias-régias e cascatas"),
    ("brecha9_temple", "49", None, "brecha", 9, "Templo claro com estátuas, fontes e jardins"),
    ("brecha9_temple_b", "64", "R", "brecha", 9, "Nave em ruínas com tapete vermelho e fogo ritual"),
    ("brecha10_astral", "50", None, "brecha", 10, "Plataformas violeta sobre o vazio, observatório no alto"),
    ("brecha10_astral_b", "58", "L", "brecha", 10, "Plataformas flutuantes ligadas por pontes"),
    ("brecha10_astral_c", "58", "R", "brecha", 10, "Anel de salas em volta de um poço de estrelas"),
]


def split_column(im: Image.Image) -> int:
    """Coluna mais escura (média de 9 colunas) no terço central: o vão entre os dois mapas, que não fica no meio exato."""
    col = list(im.convert("L").resize((im.width, 1), Image.BOX).tobytes())
    lo, hi = int(im.width * 0.43), int(im.width * 0.61)
    smooth = [(sum(col[x - 4:x + 5]) / 9, x) for x in range(lo, hi)]
    best = min(v for v, _ in smooth)
    xs = [x for v, x in smooth if v <= best + 1.5]
    return (xs[0] + xs[-1]) // 2


def main() -> None:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--src", required=True)
    args = ap.parse_args()
    src = Path(args.src)
    entries = []
    for mid, num, half, role, target, note in MAPS:
        im = Image.open(src / f"{num}.png").convert("RGB")
        if half:
            cut = split_column(im)
            im = im.crop((0, 0, cut, im.height)) if half == "L" else im.crop((cut, 0, im.width, im.height))
        folder = OUT_DIR / mid
        folder.mkdir(parents=True, exist_ok=True)
        out = folder / f"map_{mid}.png"
        im.save(out, optimize=True)
        e = {
            "id": mid,
            "image": f"res://assets/maps/extra/{mid}/map_{mid}.png",
            "imageSize": [im.width, im.height],
            "source": f"{num}{half or ''}",
            "role": role,
        }
        if role == "extraRoom":
            e["map"] = target
        else:
            e["brecha"] = target
            e["brechaName"] = BRECHAS[target]
        e["collision"] = False
        e["note"] = note
        entries.append(e)
        print(f"{mid}: {im.width}×{im.height} ({out.stat().st_size / 1024:.0f} KiB)")
    doc = {
        "_doc": "Mapas candidatos (sem colisão). Gerado por tools/maps/extra_maps.py. role=extraRoom: sala extra de um mapa de Brecha existente (map); role=brecha: sala de uma Brecha nova 6–10 (nomes da prancha 52). Para jogar, crie layout + colisão com tools/maps/layout.py e mova para maps.json.",
        "brechas": {str(k): v for k, v in BRECHAS.items()},
        "maps": entries,
    }
    OUT_JSON.write_bytes((json.dumps(doc, ensure_ascii=False, indent=1) + "\n").encode("utf-8"))
    contact(entries)


def contact(entries: list[dict]) -> None:
    """docs/design/catalogo/mapas-extra.jpg: miniaturas dos candidatos, para a triagem."""
    from PIL import ImageDraw, ImageFont

    thumb_w, thumb_h, cols, pad, label = 420, 236, 4, 10, 22
    rows = (len(entries) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * (thumb_w + pad) + pad, rows * (thumb_h + label + pad) + pad), (24, 22, 34))
    d = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype(str(ROOT / "assets" / "fonts" / "VT323-Regular.ttf"), 20)
    except OSError:
        font = ImageFont.load_default()
    for i, e in enumerate(entries):
        im = Image.open(ROOT / e["image"].removeprefix("res://")).convert("RGB")
        k = min(thumb_w / im.width, thumb_h / im.height)
        t = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
        x = pad + (i % cols) * (thumb_w + pad)
        y = pad + (i // cols) * (thumb_h + label + pad)
        sheet.paste(t, (x + (thumb_w - t.width) // 2, y))
        role = e.get("map") or f"Brecha {e['brecha']}"
        d.text((x, y + thumb_h + 1), f"{e['id']} · {role} · img {e['source']}", fill=(244, 231, 198), font=font)
    out = ROOT / "docs" / "design" / "catalogo" / "mapas-extra.jpg"
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out, quality=85)


if __name__ == "__main__":
    main()
