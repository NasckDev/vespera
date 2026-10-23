#!/usr/bin/env python3
"""Pipeline de importação das tiras de sprite da v2: chroma-key, fatia por largura igual, escala por personagem,
alinhamento dos pés, atlas PNG + SpriteFrames .tres + JSON.

Uso (da raiz do repositório):
  python tools/art/import_strips.py hunters  --walk-dir <pasta com *_andar_*_6q_REFERENCIA.png>
  python tools/art/import_strips.py monsters
  python tools/art/import_strips.py all --walk-dir <pasta>

Regras (ver docs/v2/PLANO-V2.md §2, caixa de revisão):
  * Fatia SEMPRE por largura igual (a contagem de quadros vem das tabelas abaixo), nunca por componentes conexos.
    Pedaços destacados do corpo que tocam a borda esquerda/direita da fatia (rastro do quadro vizinho) são apagados.
  * Escala por PERSONAGEM: a altura em pé (mediana dos quadros de 'parado', só o corpo = maior componente conexo)
    vira HUNTER_STAND_HEIGHT px. A mesma escala vale para todas as tiras da classe, exceto as tiras cuja altura no
    primeiro quadro difere >15% da de 'parado' (ex.: 'dano' desenhado ~45% maior): essas são reescaladas para casar.
  * Pés: linha de base = última linha opaca do corpo no 1º quadro; âncora X = centroide das pernas no 1º quadro.
    Ambos ficam fixos durante a tira (não recentraliza por quadro, senão a animação treme).
  * Caçadores: célula 256x256, pés em (128, 232). Andar vem das tiras já transparentes (192x192, pés em (96,176)),
    reescaladas pelo mesmo critério de altura e com âncora fixa (96,176) da fonte.
  * Monstros: Lodo ~48 px, Espectro ~96 px, Lodo-Mãe ~260 px de altura do corpo em 'parado'; células 128/192/384.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

sys.path.insert(0, str(Path(__file__).resolve().parent))
from chroma_key import chroma_key  # noqa: E402

ALPHA_MIN = 24
JOIN_PX = 2  # dilatação para juntar fios de cabelo/brilho ao corpo ao rotular componentes
SHADOW_GREEN_MARGIN = 25
SHADOW_MAX_RED_BLUE = 60
FEET_FRACTION = 0.15  # fração de baixo do corpo usada para achar o x dos pés (parado)
LEGS_FRACTION = 0.35  # centroide medido no terço de baixo do corpo
DEVIATION_LIMIT = 0.15

HUNTER_STAND_HEIGHT = 112
HUNTER_CELL = 256
HUNTER_FEET = (128, 232)
WALK_CELL = 192
WALK_FEET = (96, 176)

ROOT = Path(__file__).resolve().parents[2]
GERADO = ROOT / "assets" / "v2" / "gerado"
SPRITES = ROOT / "assets" / "v2" / "sprites"
RES_PREFIX = "res://assets/v2/sprites"

HUNTER_FRAMES = {
    "lamina": {"parado": 4, "andar": 6, "ataque1": 5, "ataque2": 5, "ataque3": 5, "pesado": 8, "esquiva": 5, "dano": 2, "morte": 7},
    "arco": {"parado": 4, "tiro": 6, "tiro_carregado": 7, "esquiva": 5, "dano": 2, "morte": 7},
    "martelo": {"parado": 4, "ataque1": 6, "ataque2": 6, "pesado": 9, "esquiva": 5, "dano": 2, "morte": 7},
}
DIRECTIONS = ("baixo", "cima", "lado")

# alvo de altura do corpo em 'parado' e célula (px) por monstro
MONSTERS = {
    "lodo": {"height": 48, "cell": 128},
    "espectro": {"height": 96, "cell": 192},
    "lodo_mae": {"height": 260, "cell": 384},
}
MONSTER_MARGIN = {"lodo": 8, "espectro": 12, "lodo_mae": 16}  # folga entre o pé e a borda de baixo da célula

PLANTED_ANIMS = {"parado", "dano"}  # pés no mesmo lugar: realinha o x por quadro (a fonte desloca os quadros dentro da fatia)
LOOP_ANIMS = {"parado", "andar", "mover"}
HIT_ANIMS = re.compile(r"^(ataque\d*|pesado|tiro|tiro_carregado|engolir|dividir)$")


def fps_for(anim: str) -> int:
    """Tabela de fps. aviso/pulo/engolir/dividir não constavam na especificação: 8/10/10/10 é suposição documentada."""
    if anim == "parado":
        return 6
    if anim in ("andar", "mover"):
        return 10
    if anim.startswith("ataque"):
        return 14
    if anim == "pesado":
        return 12
    if anim == "esquiva":
        return 14
    if anim == "dano":
        return 10
    if anim == "morte":
        return 8
    if anim.startswith("tiro"):
        return 12
    if anim == "aviso":
        return 8
    return 10


@dataclass
class FrameInfo:
    image: Image.Image  # quadro limpo (RGBA) numa tela comum a todos os quadros da tira, na resolução da fonte
    top: int
    bottom: int
    centroid_x: float  # coords da tela
    feet_x: float  # centroide dos 15% de baixo do corpo (pés), coords da tela
    body_area: int
    total_area: int
    borrowed: int  # efeitos que cruzam a fronteira da fatia e foram atribuídos a este quadro
    cut: bool  # corpo fundido ao do quadro vizinho: o que passa da fatia foi cortado

    @property
    def height(self) -> int:
        return self.bottom - self.top + 1


@dataclass
class Strip:
    anim: str
    direction: str
    source: str
    frames: list[FrameInfo]
    scale: float = 1.0
    note: str = ""
    fixed_anchor: tuple[float, float] | None = None  # (x, y) em coords da tela; None = medido no 1º quadro
    clean_green: bool = False  # caçadores: sem verde legítimo na paleta
    per_frame_feet: bool = False  # parado: pés plantados, realinha o x por quadro (a fonte desloca os quadros na fatia)
    origin_x: int = 0  # x da tela relativo ao início da fatia (negativo = tela estendida à esquerda)

    @property
    def anchor(self) -> tuple[float, float]:
        if self.fixed_anchor is not None:
            return self.fixed_anchor
        first = self.frames[0]
        return first.centroid_x, float(first.bottom)


def slot_bounds(width: int, count: int) -> list[tuple[int, int]]:
    edges = [round(i * width / count) for i in range(count + 1)]
    return list(zip(edges[:-1], edges[1:]))


def analyze_strip(rgba: np.ndarray, count: int) -> tuple[list[FrameInfo], int]:
    """Fatia por largura igual, mas sem cortar efeitos no meio.

    O corpo de cada quadro é o maior componente dentro da sua fatia. Componentes que não são corpo (rastro de flecha,
    brilho, faíscas) pertencem à fatia onde têm mais pixels e vão inteiros para aquele quadro, mesmo passando da fronteira
    da fatia; as outras fatias não os recebem. Se dois quadros têm o MESMO componente como corpo (encostaram), o que passa
    da fatia é cortado nos dois."""
    height, width = rgba.shape[:2]
    mask = rgba[..., 3] >= ALPHA_MIN
    joined = ndimage.binary_dilation(mask, structure=np.ones((2 * JOIN_PX + 1,) * 2))
    labels, total = ndimage.label(joined)
    bounds = slot_bounds(width, count)
    per_slot = np.zeros((count, total + 1), dtype=np.int64)
    for i, (x0, x1) in enumerate(bounds):
        sub = labels[:, x0:x1][mask[:, x0:x1]]
        per_slot[i] = np.bincount(sub, minlength=total + 1)
    per_slot[:, 0] = 0
    main = per_slot.argmax(axis=1)
    owner = per_slot.argmax(axis=0)
    main_counts = {int(m): int((main == m).sum()) for m in set(main.tolist())}
    kept: list[np.ndarray] = []
    meta = []
    for i, (x0, x1) in enumerate(bounds):
        own = (owner == i)
        own[0] = False
        for j, label_j in enumerate(main):
            own[label_j] = (j == i) or (main_counts[int(label_j)] > 1 and label_j == main[i])
        own[main[i]] = True
        shared = main_counts[int(main[i])] > 1
        keep = own[labels] & (rgba[..., 3] > 0)
        if shared:
            inside = np.zeros_like(keep)
            inside[:, x0:x1] = True
            keep &= inside | (labels != main[i])
        borrowed = sum(
            1 for label_id in np.flatnonzero(own)
            if label_id != main[i] and (per_slot[:, label_id] > 0).sum() > 1
        )
        kept.append(keep)
        meta.append((borrowed, shared))
    # tela comum: de XMIN a XMAX em coords relativas à fatia
    rel_min, rel_max = 0, bounds[0][1] - bounds[0][0]
    for (x0, x1), keep in zip(bounds, kept):
        cols = np.flatnonzero(keep.any(axis=0))
        if cols.size:
            rel_min = min(rel_min, int(cols[0]) - x0)
            rel_max = max(rel_max, int(cols[-1]) + 1 - x0, x1 - x0)
    infos = []
    for i, ((x0, x1), keep) in enumerate(zip(bounds, kept)):
        canvas = np.zeros((height, rel_max - rel_min, 4), dtype=np.uint8)
        src_l, src_r = max(x0 + rel_min, 0), min(x0 + rel_max, width)
        dst_l = src_l - (x0 + rel_min)
        piece = rgba[:, src_l:src_r].copy()
        piece[~keep[:, src_l:src_r]] = 0
        canvas[:, dst_l : dst_l + (src_r - src_l)] = piece
        body = mask & keep & (labels == main[i])
        if not body.any():
            raise ValueError("quadro sem corpo")
        rows = np.flatnonzero(body.any(axis=1))
        top, bottom = int(rows[0]), int(rows[-1])
        low_start = max(top, bottom - max(1, int((bottom - top + 1) * LEGS_FRACTION)))
        legs = body[low_start : bottom + 1]
        weights = legs.sum(axis=0)
        centroid_global = float((np.arange(width) * weights).sum() / weights.sum())
        feet_start = max(top, bottom - max(1, int((bottom - top + 1) * FEET_FRACTION)))
        feet_w = body[feet_start : bottom + 1].sum(axis=0)
        feet_global = float((np.arange(width) * feet_w).sum() / feet_w.sum())
        final_mask = canvas[..., 3] >= ALPHA_MIN
        infos.append(FrameInfo(
            image=Image.fromarray(canvas, "RGBA"), top=top, bottom=bottom,
            centroid_x=centroid_global - (x0 + rel_min), feet_x=feet_global - (x0 + rel_min), body_area=int(body.sum()),
            total_area=int(final_mask.sum()), borrowed=meta[i][0], cut=meta[i][1],
        ))
    return infos, rel_min


def remove_green_cast(rgba: Image.Image) -> Image.Image:
    """Só para os caçadores (paleta vermelho/laranja/roxo/marrom, nunca verde): apaga a sombra de chão esverdeada que o
    gerador desenha sob os pés e limita G a max(R,B) em todo pixel, o que tira o halo lima do brilho amarelo sem
    mexer em amarelo/laranja de verdade (R >= G)."""
    data = np.array(rgba).astype(np.int16)
    r, g, b = data[..., 0], data[..., 1], data[..., 2]
    top = np.maximum(r, b)
    shadow = (g > top + SHADOW_GREEN_MARGIN) & (top < SHADOW_MAX_RED_BLUE)
    data[shadow] = 0
    data[..., 1] = np.minimum(data[..., 1], np.maximum(data[..., 0], data[..., 2]))
    return Image.fromarray(data.astype(np.uint8), "RGBA")


def load_strip(path: Path, frames: int, anim: str, direction: str, clean_green: bool = False) -> Strip:
    image = Image.open(path)
    rgba = image.convert("RGBA")
    has_alpha = np.asarray(rgba.getchannel("A")).min() < 250
    if not has_alpha:
        rgba = chroma_key(image.convert("RGB"))
    if clean_green:
        rgba = remove_green_cast(rgba)
    try:
        infos, origin_x = analyze_strip(np.array(rgba), frames)
    except ValueError as error:
        raise ValueError(f"{path.name}: {error}") from error
    return Strip(anim=anim, direction=direction, source=path.name, frames=infos, origin_x=origin_x, clean_green=clean_green)


# ---------------------------------------------------------------- escala

def median_height(strips: list[Strip]) -> float:
    return float(np.median([f.height for s in strips for f in s.frames]))


def reference_height(strip: Strip) -> int:
    """Altura de pé do corpo medida na tira. Golpes começam e terminam em guarda, mas o 1º quadro pode ter a arma
    erguida (infla a altura): para ataques/tiros vale o menor entre o primeiro e o último quadro."""
    first, last = strip.frames[0].height, strip.frames[-1].height
    if strip.anim.startswith(("ataque", "tiro")):
        return min(first, last)
    return first


def resolve_hunter_scales(strips: list[Strip], walks: list[Strip], tolerance: float) -> dict:
    """tolerance: desvio (vs. parado) a partir do qual a tira é reescalada para casar. 0 = toda tira é normalizada
    pela própria altura de guarda (lâmina/arco: as tiras foram desenhadas em escalas diferentes entre si)."""
    standing = [s for s in strips if s.anim == "parado"]
    stand_height = median_height(standing)
    class_scale = HUNTER_STAND_HEIGHT / stand_height
    report = {"stand_height_src": stand_height, "class_scale": class_scale, "tolerance": tolerance}
    for strip in strips:
        ref = reference_height(strip)
        deviation = ref / stand_height - 1.0
        if abs(deviation) > tolerance:
            strip.scale = HUNTER_STAND_HEIGHT / ref
            strip.note = f"reescalada: guarda {deviation:+.0%} vs parado"
        else:
            strip.scale = class_scale
    if walks:
        walk_height = median_height(walks)
        walk_scale = HUNTER_STAND_HEIGHT / walk_height
        for strip in walks:
            strip.scale = walk_scale
            strip.fixed_anchor = (float(WALK_FEET[0] - strip.origin_x), float(np.median([f.bottom for f in strip.frames])))
        report["walk_height_src"] = walk_height
        report["walk_scale"] = walk_scale
    return report


# ---------------------------------------------------------------- composição

def resize_rgba(image: Image.Image, size: tuple[int, int], clamp_green: bool) -> Image.Image:
    """Reduz/amplia em float com alfa pré-multiplicado (o resize do Pillow em RGBA gera cor errada em alfa baixo,
    inclusive pixels verdes). Com clamp_green, G <= max(R,B) de novo depois da interpolação."""
    data = np.asarray(image).astype(np.float32) / 255.0
    alpha = data[..., 3:4]
    premult = np.concatenate([data[..., :3] * alpha, alpha], axis=-1)
    channels = [
        np.asarray(Image.fromarray(premult[..., i], "F").resize(size, Image.LANCZOS)) for i in range(4)
    ]
    out = np.stack(channels, axis=-1)
    new_alpha = np.clip(out[..., 3:4], 0.0, 1.0)
    color = np.clip(out[..., :3], 0.0, 1.0)
    color = np.where(new_alpha > 1e-4, np.minimum(color / np.maximum(new_alpha, 1e-4), 1.0), 0.0)
    if clamp_green:
        color[..., 1] = np.minimum(color[..., 1], np.maximum(color[..., 0], color[..., 2]))
    result = np.concatenate([color, new_alpha], axis=-1)
    return Image.fromarray((result * 255.0 + 0.5).astype(np.uint8), "RGBA")


def render_strip(strip: Strip, cell: int, feet: tuple[int, int]) -> tuple[list[Image.Image], int]:
    """Devolve as células (RGBA cell x cell) e quantos quadros tiveram pixel cortado pela borda da célula."""
    anchor_x, anchor_y = strip.anchor
    off_y = feet[1] - round(anchor_y * strip.scale)
    cells, clipped = [], 0
    for frame in strip.frames:
        frame_anchor_x = frame.feet_x if strip.per_frame_feet else anchor_x
        off_x = feet[0] - round(frame_anchor_x * strip.scale)
        size = (max(1, round(frame.image.width * strip.scale)), max(1, round(frame.image.height * strip.scale)))
        scaled = resize_rgba(frame.image, size, strip.clean_green)
        canvas = Image.new("RGBA", (cell, cell), (0, 0, 0, 0))
        left, upper = max(off_x, 0), max(off_y, 0)
        src_box = (max(-off_x, 0), max(-off_y, 0), min(size[0], cell - off_x), min(size[1], cell - off_y))
        if src_box[2] > src_box[0] and src_box[3] > src_box[1]:
            canvas.alpha_composite(scaled, (left, upper), src_box)
        full = np.asarray(scaled.getchannel("A")) >= ALPHA_MIN
        kept = np.asarray(canvas.getchannel("A")) >= ALPHA_MIN
        if int(full.sum()) - int(kept.sum()) > 0:
            clipped += 1
        cells.append(canvas)
    return cells, clipped


@dataclass
class Animation:
    name: str
    fps: int
    loop: bool
    cells: list[Image.Image]
    scale: float
    source: str
    note: str
    hit_frame: int | None
    areas: list[int]
    clipped_frames: int
    border_removed: int
    touching_border: int
    feet_drift_px: float
    row: int = 0
    extra: dict = field(default_factory=dict)


def make_animation(name: str, anim: str, strip: Strip, cell: int, feet: tuple[int, int]) -> Animation:
    cells, clipped = render_strip(strip, cell, feet)
    areas = [int(np.count_nonzero(np.asarray(c.getchannel("A")) >= ALPHA_MIN)) for c in cells]
    hit = int(np.argmax(areas)) if HIT_ANIMS.match(anim) else None
    drift = max(abs(f.bottom - strip.frames[0].bottom) for f in strip.frames) * strip.scale
    return Animation(
        name=name, fps=fps_for(anim), loop=anim in LOOP_ANIMS, cells=cells, scale=strip.scale, source=strip.source,
        note=strip.note, hit_frame=hit, areas=areas, clipped_frames=clipped,
        border_removed=sum(f.borrowed for f in strip.frames),
        touching_border=sum(f.cut for f in strip.frames), feet_drift_px=round(drift, 1),
    )


def write_outputs(key: str, out_dir: Path, animations: list[Animation], meta: dict, cell: int) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    columns = max(len(a.cells) for a in animations)
    atlas = Image.new("RGBA", (columns * cell, len(animations) * cell), (0, 0, 0, 0))
    for row, animation in enumerate(animations):
        animation.row = row
        for column, image in enumerate(animation.cells):
            atlas.paste(image, (column * cell, row * cell))
    atlas_path = out_dir / f"{key}_atlas.png"
    atlas.save(atlas_path, optimize=True)
    (out_dir / f"{key}.tres").write_text(spriteframes_text(key, animations, cell), encoding="utf-8", newline="\n")
    sidecar = dict(meta)
    sidecar["atlas"] = atlas_path.name
    sidecar["cell"] = [cell, cell]
    sidecar["animations"] = {
        a.name: {
            "row": a.row, "frames": len(a.cells), "fps": a.fps, "loop": a.loop, "scale": round(a.scale, 4),
            "source": a.source, "hit_frame": a.hit_frame,
            "opaque_area_per_frame": a.areas, "note": a.note or None,
        }
        for a in animations
    }
    (out_dir / f"{key}.json").write_text(json.dumps(sidecar, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")


def spriteframes_text(key: str, animations: list[Animation], cell: int) -> str:
    texture_path = f"{RES_PREFIX}/{out_subdir(key)}/{key}_atlas.png"
    sub_resources, entries = [], []
    index = 0
    for animation in animations:
        frames = []
        for column in range(len(animation.cells)):
            sub_id = f"r{index}"
            index += 1
            sub_resources.append(
                f'[sub_resource type="AtlasTexture" id="{sub_id}"]\natlas = ExtResource("1")\n'
                f"region = Rect2({column * cell}, {animation.row * cell}, {cell}, {cell})\n"
            )
            frames.append(f'{{\n"duration": 1.0,\n"texture": SubResource("{sub_id}")\n}}')
        entries.append(
            "{\n\"frames\": [" + ", ".join(frames) + "],\n"
            f"\"loop\": {'true' if animation.loop else 'false'},\n\"name\": &\"{animation.name}\",\n\"speed\": {float(animation.fps)}\n}}"
        )
    head = f'[gd_resource type="SpriteFrames" load_steps={len(sub_resources) + 2} format=3]\n\n'
    head += f'[ext_resource type="Texture2D" path="{texture_path}" id="1"]\n\n'
    return head + "\n".join(sub_resources) + "\n[resource]\nanimations = [" + ", ".join(entries) + "]\n"


def out_subdir(key: str) -> str:
    if key.endswith("_e1"):
        return key.split("_")[0]
    return f"monstros/{key}"


# ---------------------------------------------------------------- caçadores

def parse_hunter_name(cls: str, path: Path) -> tuple[str, str] | None:
    match = re.fullmatch(rf"{cls}_e1_(.+)_({'|'.join(DIRECTIONS)})", path.stem)
    return (match.group(1), match.group(2)) if match else None


HUNTER_TOLERANCE = {"lamina": 0.0, "arco": 0.0, "martelo": 0.25}


def build_hunter(cls: str, walk_dir: Path | None) -> dict:
    table = HUNTER_FRAMES[cls]
    strips: list[Strip] = []
    for path in sorted((GERADO / cls).glob(f"{cls}_e1_*.png")):
        parsed = parse_hunter_name(cls, path)
        if parsed is None or parsed[0] not in table:
            print(f"  ignorado: {path.name}")
            continue
        anim, direction = parsed
        strip = load_strip(path, table[anim], anim, direction, clean_green=True)
        strip.per_frame_feet = anim in PLANTED_ANIMS
        strips.append(strip)
    walks: list[Strip] = []
    if walk_dir is not None:
        for path in sorted(walk_dir.glob(f"{cls}/{cls}_e1_andar_*_6q_REFERENCIA.png")):
            direction = path.stem.split("_")[3]
            width, _ = Image.open(path).size
            walks.append(load_strip(path, width // WALK_CELL, "andar", direction, clean_green=True))
    report = resolve_hunter_scales(strips, walks, HUNTER_TOLERANCE[cls])
    animations = [
        make_animation(f"{s.anim}_{s.direction}", s.anim, s, HUNTER_CELL, HUNTER_FEET) for s in strips + walks
    ]
    animations.sort(key=lambda a: (list(table).index(a.name.rsplit("_", 1)[0]) if a.name.rsplit("_", 1)[0] in table else -1, a.name))
    meta = {
        "character": cls, "stage": "e1", "anchor": list(HUNTER_FEET), "stand_height_px": HUNTER_STAND_HEIGHT,
        "facing": "lado olha para a direita; para a esquerda use flip_h",
        "scale_class": round(report["class_scale"], 4), "stand_height_source_px": report["stand_height_src"],
        "hit_frame_method": "quadro de maior área opaca (índice base 0), só para ataques",
        "frame_counts": table,
    }
    write_outputs(f"{cls}_e1", SPRITES / cls, animations, meta, HUNTER_CELL)
    return {"report": report, "animations": animations}


# ---------------------------------------------------------------- monstros

def fit_cell(strips: list[Strip], minimum: int, margin: int) -> int:
    """Menor célula (múltiplo de 32, >= minimum) onde cabem todos os quadros com os pés em (cell/2, cell-margin)."""
    half_width, up = 0.0, 0.0
    for strip in strips:
        ax, ay = strip.anchor
        for frame in strip.frames:
            box = frame.image.getchannel("A").point(lambda v: 255 if v >= ALPHA_MIN else 0).getbbox()
            half_width = max(half_width, (ax - box[0]) * strip.scale, (box[2] - ax) * strip.scale)
            up = max(up, (ay - box[1]) * strip.scale)
    needed = max(2 * half_width, up + margin) + 2
    return max(minimum, int(-(-needed // 32) * 32))


def monster_of(file_stem: str) -> tuple[str, str]:
    for monster in sorted(MONSTERS, key=len, reverse=True):
        if file_stem.startswith(monster + "_"):
            return monster, file_stem[len(monster) + 1 :]
    raise ValueError(file_stem)


def build_monsters() -> dict:
    spec = json.loads((GERADO / "monstros" / "animacoes.json").read_text(encoding="utf-8"))
    grouped: dict[str, list[Strip]] = {name: [] for name in MONSTERS}
    for entry in spec["animations"]:
        path = GERADO / "monstros" / entry["file"]
        monster, anim = monster_of(path.stem)
        strip = load_strip(path, entry["frames"], anim, "lado")
        grouped[monster].append(strip)
    result = {}
    for monster, strips in grouped.items():
        config = MONSTERS[monster]
        standing = [s for s in strips if s.anim == "parado"]
        stand = median_height(standing)
        scale = config["height"] / stand
        for strip in strips:
            strip.scale = scale
        cell = fit_cell(strips, config["cell"], MONSTER_MARGIN[monster])
        feet = (cell // 2, cell - MONSTER_MARGIN[monster])
        animations = [make_animation(s.anim, s.anim, s, cell, feet) for s in strips]
        meta = {
            "character": monster, "anchor": list(feet), "stand_height_px": config["height"],
            "facing": "olha para a direita; para a esquerda use flip_h",
            "scale_class": round(scale, 4), "stand_height_source_px": stand,
            "hit_frame_method": "quadro de maior área opaca (índice base 0), só para ataques",
            "frame_counts": {a.name: len(a.cells) for a in animations},
        }
        write_outputs(monster, SPRITES / "monstros" / monster, animations, meta, cell)
        result[monster] = {"scale": scale, "animations": animations}
    return result


def print_report(title: str, animations: list[Animation]) -> None:
    print(f"== {title}")
    for a in animations:
        flags = []
        if a.note:
            flags.append(a.note)
        if a.clipped_frames:
            flags.append(f"CORTADO pela célula em {a.clipped_frames} quadros")
        if a.border_removed:
            flags.append(f"{a.border_removed} efeito(s) cruzando a fatia remanejados")
        if a.touching_border:
            flags.append(f"{a.touching_border} quadro(s) fundidos ao vizinho (cortados)")
        if a.feet_drift_px > 4:
            flags.append(f"base varia {a.feet_drift_px}px")
        print(f"  {a.name:24s} esc={a.scale:.4f} n={len(a.cells)} fps={a.fps} hit={a.hit_frame} {'; '.join(flags)}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("target", choices=["hunters", "monsters", "all"])
    parser.add_argument("--walk-dir", type=Path, help="pasta com lamina/arco/martelo/*_andar_*_6q_REFERENCIA.png")
    args = parser.parse_args()
    if args.target in ("hunters", "all"):
        for cls in HUNTER_FRAMES:
            result = build_hunter(cls, args.walk_dir)
            print(f"{cls}: escala da classe {result['report']['class_scale']:.4f}"
                  f" (altura em pé na fonte {result['report']['stand_height_src']:.0f}px)"
                  + (f", andar {result['report'].get('walk_scale', 0):.4f}" if "walk_scale" in result["report"] else ""))
            print_report(cls, result["animations"])
    if args.target in ("monsters", "all"):
        for monster, result in build_monsters().items():
            print(f"{monster}: escala {result['scale']:.4f}")
            print_report(monster, result["animations"])
    return 0


if __name__ == "__main__":
    sys.exit(main())
