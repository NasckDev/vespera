#!/usr/bin/env python3
"""
Integra a arte aprovada pelo dono (pacotes v1-v4 do designer, issues #2, #3, #5, #6, #7 e #8) e gera os SpriteFrames.

Dois passos, os dois determinísticos:

  copy   --packages <pasta com arte/ arte2/ arte3/ arte4/>
         Copia SÓ os arquivos aprovados (lista APPROVED abaixo) para assets/sprites/..., com a precedência
         v4 > v3 > v2 > v1 para o mesmo arquivo. Os pacotes são dados: nada deles é executado, só PNG lidos pelo Pillow.
         Fica de fora o que foi reprovado: animações de monstro com tremor do v1, retratos do v1 e as poses
         'special' usadas como vista lateral.
  frames
         Lê os PNG já copiados e escreve um SpriteFrames (.tres) por personagem/objeto, com metadados:
           metadata/anchors     = {animação: Vector2(pé em px do PNG)}
           metadata/densities   = {animação: px por u na escala 1 (antes de characterScale/propScale)}
           metadata/scale_class = "character" | "prop" | "fixed"  (qual escala global do rules.json se aplica)
           metadata/used_rects  = {animação: Rect2(x, y, w, h) do recorte opaco do 1º quadro, alfa > 0 como o
                                   Image.get_used_rect do Godot}: o jogo não lê pixels da GPU em tempo de execução
                                   (centro dos estilhaços, casco do barco)
         A densidade é calibrada pela arte que já está no jogo, para nada mudar de tamanho (DECISIONS C2-D1):
           - Lâmina: altura do corpo (pé até a cabeça, sem o cabo da espada) / 32 u, a mesma conta do cut_hunter.py;
           - outras classes: cada vista contra a pose do atlas (c_<classe>_<vista>) de onde o rig v4 saiu;
           - chefes: a pose de frente contra o PNG/atlas atual (cceif_f, mcol_f, grai_f, karr_f; anglerB_f pela largura,
             porque a v3 tirou a coroa que flutuava); Alfa: mesma densidade do wolf_f;
           - objetos: a densidade do objeto do catálogo (objects.json) de onde a arte saiu, ou o recorte do atlas
             (pr_barrel, pr_crate, pr_chest0..4) dividido por propScale.

Uso: python -I tools/sprites/build_art.py copy --packages <pasta>
     python -I tools/sprites/build_art.py frames
"""
from __future__ import annotations

import argparse
import json
import shutil
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SPRITES = ROOT / "assets" / "sprites"
PKG = {"v1": "arte", "v2": "arte2", "v3": "arte3", "v4": "arte4"}
ORDER = ["v4", "v3", "v2", "v1"]  # precedência para o mesmo arquivo
ALPHA = 24
CLASSES = ["sword", "bow", "staff", "hammer", "dag"]
VIEWS = ["front", "back", "side"]
SIX = range(6)


def r6(prefix: str) -> list[str]:
    return [f"{prefix}_{i:02d}.png" for i in SIX]


# --- lista do que foi aprovado: (destino relativo a assets/sprites, [(pacote, origem relativa a assets/sprites)]) ---

def approved() -> list[tuple[str, list[tuple[str, str]]]]:
    out: list[tuple[str, list[tuple[str, str]]]] = []

    def add(dest: str, src: str, pkgs: list[str]):
        out.append((dest, [(p, src) for p in pkgs]))

    # Lâmina (#3): frente v3, costas v3 + 03-05 corrigidos na v4, lado (4 quadros do kit + 2 da v2), esquiva lateral v4,
    # esquiva frente/costas da v3 (provisória).
    sw = "animations/player/sword"
    for f in r6("walk_front"):
        add(f"player/sword/{f}", f"{sw}/{f}", ["v3"])
    for f in r6("walk_back"):
        add(f"player/sword/{f}", f"{sw}/{f}", ["v4", "v3"])
    for f in r6("walk_side"):
        add(f"player/sword/{f}", f"{sw}/{f}", ["v2"])
    for f in r6("dodge_side"):
        add(f"player/sword/{f}", f"{sw}/{f}", ["v4"])
    for view in ("front", "back"):
        for i in range(8):
            f = f"dodge_{view}_{i:02d}.png"
            add(f"player/sword/{f}", f"{sw}/{f}", ["v3"])
    # Arco, Cajado, Martelo, Adagas (#3): caminhada v4, 6 quadros por vista.
    for cls in CLASSES[1:]:
        for view in VIEWS:
            for f in r6(f"walk_{view}"):
                add(f"player/{cls}/{f}", f"animations/player/{cls}/{f}", ["v4"])

    # Chefes (#2): Abissal v3 (frente/costas) + ataque v4 (+9%); os 4 normalizados da v4; Alfa com cristas (v2).
    add("enemies/anim/anglerB/front.png", "animations/enemies/angler/front_00.png", ["v3"])
    add("enemies/anim/anglerB/back.png", "animations/enemies/angler/back_00.png", ["v3"])
    add("enemies/anim/anglerB/attack.png", "animations/enemies/angler/attack_00.png", ["v4"])
    for sprite, folder in (("cceif", "ceifador"), ("mcol", "colosso"), ("grai", "rainha_raizes"), ("karr", "arauto")):
        for pose in ("front", "attack", "special", "death"):
            add(f"enemies/anim/{sprite}/{pose}.png", f"animations/bosses/{folder}/{pose}_00.png", ["v4"])
    add("enemies/anim/wolfE/front.png", "approved/wolfE_review/idle_00.png", ["v2"])
    add("enemies/anim/wolfE/back.png", "approved/wolfE_review/move_back_00.png", ["v2"])

    # Objetos com estados (#5).
    for kind in ("barrel", "crate"):
        for s in ["whole", "cracked", "broken"] + [f"shard_{i}" for i in SIX]:
            add(f"props/breakable/{kind}/{s}.png", f"props/breakable/{kind}/{s}.png", ["v1"])
    for light in ("brazier", "lamp_post"):
        for s in ["off", "on_0", "on_1", "on_2", "on_3"]:
            add(f"props/lights/{light}/{s}.png", f"props/lights/{light}/{s}.png", ["v1"])
    for color in ("blue", "violet", "yellow"):
        for s in ("flowering", "harvested", "sprouting"):
            add(f"props/flowers/{color}/{s}.png", f"props/flowers/{color}/{s}.png", ["v1"])
    for s in ("off", "on"):
        add(f"props/mechanisms/lever/{s}.png", f"props/mechanisms/lever/{s}.png", ["v1"])
    add("props/mechanisms/board/board.png", "props/mechanisms/sign.png", ["v1"])
    for s in ("closed", "open"):
        add(f"props/mechanisms/door/{s}.png", f"props/mechanisms/door/{s}.png", ["v2"])
    for boat in ("raft", "rowboat"):
        for s in ["rest", "rowing_0", "rowing_1", "rowing_2", "rowing_3", "water_reflection"]:
            add(f"props/boats/{boat}/{s}.png", f"props/boats/{boat}/{s}.png", ["v2"])
    for hand in ("left", "right"):
        add(f"props/fishing/rod_hand_{hand}.png", f"props/fishing/rod_hand_{hand}.png", ["v2"])
    for tier in range(5):
        for s in ("closed", "opening_1", "opening_2", "open", "empty"):
            add(f"props/chests/tier_{tier}/{s}.png", f"props/chests/tier_{tier}/{s}.png", ["v4", "v3", "v2"])

    # Bustos neutros (#6) e ícones do HUD (#7, #8).
    for npc in ("bia", "bras", "faisca", "ivo", "lauro", "marta", "odete", "vesna"):
        add(f"npcs/portraits/{npc}.png", f"portraits/npcs/{npc}/neutral_128.png", ["v2"])
    icons = ["dodge", "potion", "eco_abissal", "eco_arauto", "eco_ceifador", "eco_uivo"]
    icons += [f"portrait_rank_{r}" for r in ("E", "D", "C", "B", "A", "S", "SS", "SSS", "SSSS", "ALVOR")]
    icons += [f"status_{s}" for s in ("bleeding", "burning", "invasion", "rupture", "shield", "slow", "speed", "stunned")]
    icons += [f"ultimate_{c}" for c in CLASSES]
    for icon in icons:
        add(f"ui/hud/{icon}.png", f"icons/hud/{icon}.png", ["v4", "v1"])
    return out


def cmd_copy(packages: Path) -> None:
    copied = 0
    for dest, sources in approved():
        src = None
        for pkg in ORDER:
            for p, rel in sources:
                if p == pkg and (packages / PKG[pkg] / "assets" / "sprites" / rel).is_file():
                    src = packages / PKG[pkg] / "assets" / "sprites" / rel
                    break
            if src:
                break
        if src is None:
            sys.exit(f"faltou {dest} ({sources})")
        target = SPRITES / dest
        target.parent.mkdir(parents=True, exist_ok=True)
        # só PNG RGBA válidos entram (o arquivo é aberto e verificado antes de copiar)
        with Image.open(src) as im:
            im.verify()
        shutil.copyfile(src, target)
        copied += 1
    print(f"{copied} arquivos aprovados copiados para {SPRITES}")


# --- medidas ---

def load(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def bbox(im: Image.Image, alpha: int = ALPHA):
    return im.getchannel("A").point(lambda v: 255 if v > alpha else 0).getbbox()


def base_center(im: Image.Image) -> tuple[float, float]:
    """Centro da base do recorte opaco (DECISIONS D10)."""
    x0, _, x1, y1 = bbox(im)
    return ((x0 + x1) / 2, float(y1))


def body_height(im: Image.Image, anchor_x: float, foot_y: float) -> int:
    """Igual ao cut_hunter.py: do pé à primeira linha com >= 8 px opacos numa faixa de ±10% da largura no tronco."""
    a = im.getchannel("A").load()
    w, _ = im.size
    x0, x1 = max(0, int(anchor_x - w * 0.10)), min(w, int(anchor_x + w * 0.10))
    for y in range(int(foot_y)):
        if sum(1 for x in range(x0, x1) if a[x, y] > 128) >= 8:
            return int(foot_y) - y
    return int(foot_y)


class Atlas:
    def __init__(self):
        self.index = json.loads((SPRITES / "atlas" / "atlas.json").read_text(encoding="utf-8"))
        self.index = self.index.get("frames", self.index)
        self.image = load(SPRITES / "atlas" / "atlas.png")

    def size_u(self, key: str) -> tuple[float, float]:
        x, y, w, h, _, _, k = self.index[key]
        b = bbox(self.image.crop((x, y, x + w, y + h)))
        return ((b[2] - b[0]) / k, (b[3] - b[1]) / k)


def loose_size_u(key: str) -> tuple[float, float]:
    meta = json.loads((SPRITES / "enemies" / "meta.json").read_text(encoding="utf-8"))
    b = bbox(load(SPRITES / "enemies" / f"{key}.png"))
    k = meta[key][2]
    return ((b[2] - b[0]) / k, (b[3] - b[1]) / k)


def catalog_density(object_id: str) -> float:
    objs = json.loads((ROOT / "src" / "Data" / "objects.json").read_text(encoding="utf-8"))["objects"]
    items = objs.items() if isinstance(objs, dict) else ((o["id"], o) for o in objs)
    for k, v in items:
        if k == object_id:
            return float(v["density"])
    sys.exit(f"objeto {object_id} não está no objects.json")


def prop_scale() -> float:
    rules = json.loads((ROOT / "src" / "Data" / "rules.json").read_text(encoding="utf-8"))
    return float(rules["world"]["propScale"])


# --- .tres ---

def res(path: Path) -> str:
    return "res://" + path.relative_to(ROOT).as_posix()


def write_tres(path: Path, anims: list[tuple[str, list[Path], float, bool]], anchors: dict, densities: dict,
               scale_class: str, source: str) -> None:
    files: list[Path] = []
    for _, frames, _, _ in anims:
        for f in frames:
            if f not in files:
                files.append(f)
    ids = {f: i + 1 for i, f in enumerate(files)}
    lines = [f'[gd_resource type="SpriteFrames" load_steps={len(files) + 1} format=3]', ""]
    for f in files:
        lines.append(f'[ext_resource type="Texture2D" path="{res(f)}" id="{ids[f]}"]')
    lines += ["", "[resource]"]
    blocks = []
    for name, frames, fps, loop in anims:
        fr = ", ".join('{\n"duration": 1.0,\n"texture": ExtResource("%d")\n}' % ids[f] for f in frames)
        blocks.append('{\n"frames": [%s],\n"loop": %s,\n"name": &"%s",\n"speed": %.2f\n}' % (fr, "true" if loop else "false", name, fps))
    lines.append("animations = [%s]" % ", ".join(blocks))
    lines.append("metadata/anchors = {\n%s\n}" % ",\n".join(f'"{a}": Vector2({x:g}, {y:g})' for a, (x, y) in anchors.items()))
    lines.append("metadata/densities = {\n%s\n}" % ",\n".join(f'"{a}": {d:.4f}' for a, d in densities.items()))
    used = []
    for name, frames, _, _ in anims:
        x0, y0, x1, y1 = load(frames[0]).getchannel("A").getbbox() or (0, 0, 1, 1)  # alfa > 0, igual ao get_used_rect do Godot
        used.append(f'"{name}": Rect2({x0}, {y0}, {x1 - x0}, {y1 - y0})')
    lines.append("metadata/used_rects = {\n%s\n}" % ",\n".join(used))
    lines.append(f'metadata/scale_class = "{scale_class}"')
    lines.append(f'metadata/source = "{source}"')
    path.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    print(f"{path.relative_to(ROOT)}: {len(anims)} animações, {len(files)} quadros")


def frames_of(folder: Path, prefix: str, count: int) -> list[Path]:
    return [folder / f"{prefix}_{i:02d}.png" for i in range(count)]


def build_players(atlas: Atlas) -> None:
    # Lâmina: um pivô (210, 412) e uma densidade para todos os quadros novos; a esquiva lateral dura 0,17 s (6 quadros).
    d = SPRITES / "player" / "sword"
    heights = sorted(body_height(load(f), 210, 412) for f in frames_of(d, "walk_front", 6))
    density = heights[len(heights) // 2] / 32.0
    anims = [
        ("walk_front", frames_of(d, "walk_front", 6), 9.0, True),
        ("walk_back", frames_of(d, "walk_back", 6), 9.0, True),
        ("walk_side", frames_of(d, "walk_side", 6), 9.0, True),
        ("dodge_side", frames_of(d, "dodge_side", 6), 6 / 0.17, False),
        ("dodge_front", frames_of(d, "dodge_front", 8), 8 / 0.17, False),
        ("dodge_back", frames_of(d, "dodge_back", 8), 8 / 0.17, False),
    ]
    names = [a[0] for a in anims]
    write_tres(d / "frames.tres", anims, {n: (210, 412) for n in names}, {n: density for n in names}, "character",
               "pacotes v2/v3/v4 do designer (issue #3): andar frente v3, costas v3+v4, lado kit+v2, esquiva lado v4, frente/costas v3 (provisória)")

    # Outras classes: o rig v4 saiu da pose do atlas de cada vista; a densidade de cada vista faz o quadro bater com ela.
    for cls in CLASSES[1:]:
        d = SPRITES / "player" / cls
        anims, anchors, dens = [], {}, {}
        for view in VIEWS:
            files = frames_of(d, f"walk_{view}", 6)
            b = bbox(load(files[0]))
            aw, ah = atlas.size_u(f"c_{cls}_{view}")
            k = (((b[3] - b[1]) / ah) + ((b[2] - b[0]) / aw)) / 2
            name = f"walk_{view}"
            anims.append((name, files, 9.0, True))
            anchors[name] = (210, 412)
            dens[name] = k
        write_tres(d / "frames.tres", anims, anchors, dens, "character", f"pacote v4 do designer (issue #3): caminhada {cls} 6 quadros por vista")


def build_bosses(atlas: Atlas) -> None:
    def write(sprite: str, poses: list[str], pivot, density: float, source: str):
        d = SPRITES / "enemies" / "anim" / sprite
        anims = [(p, [d / f"{p}.png"], 1.0, False) for p in poses]
        write_tres(d / "frames.tres", anims, {p: pivot for p in poses}, {p: density for p in poses}, "character", source)

    for sprite in ("cceif", "mcol", "grai", "karr"):
        b = bbox(load(SPRITES / "enemies" / "anim" / sprite / "front.png"))
        _, h_u = loose_size_u(f"{sprite}_f")
        write(sprite, ["front", "attack", "special", "death"], (410, 462), (b[3] - b[1]) / h_u,
              "pacote v4 do designer (issue #2): poses autorais normalizadas, tela 820x490, pé (410, 462)")
    b = bbox(load(SPRITES / "enemies" / "anim" / "anglerB" / "front.png"))
    w_u, _ = atlas.size_u("anglerB_f")
    write("anglerB", ["front", "back", "attack"], (310, 445), (b[2] - b[0]) / w_u,
          "pacotes v3 (frente/costas) e v4 (ataque +9%) do designer (issue #2/#8), pé (310, 445)")
    write("wolfE", ["front", "back"], (168, 186), atlas.index["wolf_f"][6],
          "pacote v2 do designer (issue #2): Alfa com cristas, poses estáticas aprovadas")


def build_props(atlas: Atlas) -> None:
    ps = prop_scale()

    def write(folder: Path, states: list[str], density: float, scale_class: str, source: str,
              reference: str | None = None, anims: list[tuple[str, list[str], float, bool]] | None = None):
        ref = load(folder / f"{reference or states[0]}.png")
        ref_anchor = base_center(ref)
        anchors, dens = {}, {}
        if anims is None:
            anims = [(s, [s], 1.0, False) for s in states]
        out = []
        for name, files, fps, loop in anims:
            im = load(folder / f"{files[0]}.png")
            anchors[name] = ref_anchor if im.size == ref.size else base_center(im)
            dens[name] = density
            out.append((name, [folder / f"{f}.png" for f in files], fps, loop))
        write_tres(folder / "frames.tres", out, anchors, dens, scale_class, source)

    def atlas_density(key: str, art: Path) -> float:
        """Recorte novo com a mesma altura do recorte do atlas (que o jogo desenha × propScale)."""
        _, h_u = atlas.size_u(key)
        b = bbox(load(art))
        return (b[3] - b[1]) / h_u

    p = SPRITES / "props"
    shards = [f"shard_{i}" for i in SIX]
    for kind in ("barrel", "crate"):
        folder = p / "breakable" / kind
        write(folder, ["whole", "cracked", "broken"] + shards, atlas_density(f"pr_{kind}", folder / "whole.png"), "prop",
              f"pacote v1 do designer (issue #5): {kind} inteiro/rachado/quebrado + 6 estilhaços")
    for tier in range(5):
        folder = p / "chests" / f"tier_{tier}"
        # o fechado do tier 4 (v4) veio numa tela menor: o pé comum sai dos quadros de abrir (mesma tela de 156×150)
        same = load(folder / "closed.png").size == load(folder / "opening_1.png").size
        write(folder, ["closed", "opening_1", "opening_2", "open", "empty"], atlas_density(f"pr_chest{tier}", folder / "closed.png"),
              "prop", f"pacotes v2/v3/v4 do designer (issue #5/#8): baú tier {tier} fechado/abrindo/aberto/vazio",
              reference=None if same else "opening_1")
    for light, obj in (("lamp_post", "lamp_post_common"), ("brazier", "brazier_stand")):
        write(p / "lights" / light, [], catalog_density(obj), "fixed",
              f"pacote v1 do designer (issue #5): {light} apagado/aceso com chama em 4 quadros", reference="off",
              anims=[("off", ["off"], 1.0, False), ("on", ["on_0", "on_1", "on_2", "on_3"], 8.0, True)])
    for color, obj in (("blue", "flowers_blue"), ("yellow", "flowers_yellow"), ("violet", "flower_violet_glow")):
        write(p / "flowers" / color, ["flowering", "harvested", "sprouting"], catalog_density(obj), "fixed",
              f"pacote v1 do designer (issue #5): flor {color} florida/colhida/brotando")
    write(p / "mechanisms" / "lever", ["off", "on"], catalog_density("lever_red"), "fixed", "pacote v1 do designer (issue #5): alavanca")
    write(p / "mechanisms" / "board", ["board"], catalog_density("notice_board"), "fixed", "pacote v1 do designer (issue #5): quadro de avisos")
    write(p / "mechanisms" / "door", ["closed", "open"], catalog_density("door_arched"), "fixed", "pacote v2 do designer (issue #5): porta fechada/aberta")
    for boat, obj in (("raft", "dock_raft"), ("rowboat", "rowboat")):
        write(p / "boats" / boat, [], catalog_density(obj), "fixed", f"pacote v2 do designer (issue #5): {boat} parado/remando (4 quadros)",
              reference="rest", anims=[("rest", ["rest"], 1.0, False), ("rowing", ["rowing_0", "rowing_1", "rowing_2", "rowing_3"], 6.0, True),
                                       ("reflection", ["water_reflection"], 1.0, False)])
    # Vara de pesca: ~0,75 da altura do caçador (32 u × characterScale) na mão.
    rod = p / "fishing"
    b = bbox(load(rod / "rod_hand_right.png"))
    write(rod, [], (b[3] - b[1]) / 24.0, "character", "pacote v2 do designer (issue #5): vara de pesca nas duas mãos",
          reference="rod_hand_right", anims=[("right", ["rod_hand_right"], 1.0, False), ("left", ["rod_hand_left"], 1.0, False)])
    print(f"propScale {ps}")


def cmd_frames() -> None:
    atlas = Atlas()
    build_players(atlas)
    build_bosses(atlas)
    build_props(atlas)


def main() -> None:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    c = sub.add_parser("copy")
    c.add_argument("--packages", required=True)
    sub.add_parser("frames")
    args = ap.parse_args()
    if args.cmd == "copy":
        cmd_copy(Path(args.packages))
    else:
        cmd_frames()


if __name__ == "__main__":
    main()
