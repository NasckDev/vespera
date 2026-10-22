"""Esqueleto de poses: monta um quadro 48x48 colando peças pré-desenhadas em posições inteiras.

Camadas (cada uma vira uma matriz própria antes da composição, para o estágio poder trocar qualquer uma):
  body (cabeça, mãos, pés) · clothes (tronco, mangas, calças, botas) · cape · weapon · lantern · aura · fx
O contorno seletivo é aplicado ao conjunto body+clothes+cape+weapon+lantern; aura e fx ficam por cima sem contorno.
"""
from __future__ import annotations

from dataclasses import dataclass, field

import numpy as np

import parts_body as PB
import parts_gear as PG
from limbs import Segment, render_limb, stamp
from parts_clothes import APRENDIZ, CapeShape, Costume, render_cape
from pixcore import EMPTY, PAL, blit, canvas, neighbours, outline, separate

CELL = 48
# As poses são autoradas com o chão em y=44 ("coordenada de desenho") e o quadro final sobe Y_SHIFT px,
# para sobrar espaço igual acima da cabeça e abaixo dos pés (golpes para baixo cabem na célula).
DESIGN_GROUND = 44
Y_SHIFT = -3
ANCHOR = (24, DESIGN_GROUND + Y_SHIFT)  # (24, 41): pixel do chão sob os pés = origem do nó no Godot
NECK = {"down": (24, 24), "up": (24, 24), "side": (25, 24)}


@dataclass
class Leg:
    hip: tuple[int, int]      # absoluto (já com o deslocamento do corpo)
    knee: tuple[int, int]
    ankle: tuple[int, int]
    foot: str = "down"
    far: bool = False         # perna de trás: um tom mais escura
    z: int = 3


@dataclass
class Arm:
    shoulder: tuple[int, int]
    elbow: tuple[int, int]
    wrist: tuple[int, int]
    hand: str | None = "front"
    far: bool = False
    z: int = 7
    sep: bool = True          # sombra de separação sobre o tronco


@dataclass
class Sword:
    angle: int
    grip: tuple[int, int]     # pixel do punho
    z: int = 9
    blade: int = 10


@dataclass
class Item:
    """Peça desenhada genérica (cabeça alternativa, corpo caído, bola do rolamento...)."""
    img: np.ndarray
    pos: tuple[int, int]      # canto superior esquerdo
    layer: str = "body"
    z: int = 5
    flip: bool = False


@dataclass
class Pose:
    dir: str
    body: tuple[int, int] = (0, 0)
    head: str | None = None           # variante; None = padrão da direção
    head_off: tuple[int, int] = (0, 0)
    torso: bool = True
    torso_off: tuple[int, int] = (0, 0)
    legs: list[Leg] = field(default_factory=list)
    arms: list[Arm] = field(default_factory=list)
    cape: CapeShape | None = None
    cape_z: int = 0
    sword: Sword | None = None
    lantern: tuple[str, tuple[int, int]] | None = None
    lantern_z: int = 6
    items: list[Item] = field(default_factory=list)
    fx: list[np.ndarray] = field(default_factory=list)
    ghosts: list[tuple[int, int, str]] = field(default_factory=list)   # (dx, dy, 'rampa:tom')
    aura: list[np.ndarray] = field(default_factory=list)
    head_z: int = 8
    torso_z: int = 5
    flip: bool = False                # perfil virado para a esquerda (só no giro do pesado)

    def neck(self) -> tuple[int, int]:
        nx, ny = NECK[self.dir]
        return nx + self.body[0], ny + self.body[1]


def _limb_leg(leg: Leg, cos: Costume) -> list[tuple[str, np.ndarray]]:
    m = cos.limbs
    dk = -1 if leg.far else 0
    boot_top = (leg.ankle[0] + (leg.knee[0] - leg.ankle[0]) // 2, leg.ankle[1] + (leg.knee[1] - leg.ankle[1]) // 2)
    pants = render_limb([
        Segment(leg.hip, leg.knee, m.leg_w, m.pants[0], m.pants[1] + dk),
        Segment(leg.knee, boot_top, m.leg_w, m.pants[0], m.pants[1] + dk),
    ], (CELL, CELL))
    boot = render_limb([Segment(boot_top, leg.ankle, m.leg_w - 1 if m.leg_w > 3 else m.leg_w,
                                m.boot[0], m.boot[1] + dk)], (CELL, CELL))
    # punho da bota: um tom mais claro na primeira fileira
    ys, xs = np.nonzero(boot >= 0)
    if len(ys):
        top = ys.min()
        for x in xs[ys == top]:
            boot[top, x] = PAL.shift(int(boot[top, x]), 1)
    feet = canvas(CELL, CELL)
    if leg.foot:
        foot = PB.FOOT[leg.foot]
        if leg.far:
            foot = np.where(foot >= 0, np.vectorize(lambda i: PAL.shift(int(i), -1))(foot), EMPTY)
        stamp(feet, foot, leg.ankle, PB.FOOT_PIVOT[leg.foot])
    return [("clothes", pants), ("clothes", boot), ("body", feet)]


def _limb_arm(arm: Arm, cos: Costume) -> list[tuple[str, np.ndarray]]:
    m = cos.limbs
    dk = -1 if arm.far else 0
    sleeve = render_limb([Segment(arm.shoulder, arm.elbow, m.arm_w, m.sleeve[0], m.sleeve[1] + dk)], (CELL, CELL))
    glove = render_limb([Segment(arm.elbow, arm.wrist, m.glove_w, m.glove[0], m.glove[1] + dk)], (CELL, CELL))
    hand = canvas(CELL, CELL)
    if arm.hand:
        stamp(hand, PB.HAND[arm.hand], arm.wrist, PB.HAND_PIVOT[arm.hand])
    out = canvas(CELL, CELL)
    for part in (sleeve, glove, hand):
        blit(out, part, 0, 0)
    return [("clothes", out)]


def compose(pose: Pose, cos: Costume = APRENDIZ) -> dict:
    """Retorna {'image': matriz final, 'layers': {nome: matriz}} de um quadro."""
    stack: list[tuple[int, str, np.ndarray, bool]] = []  # (z, camada, imagem, separa)
    nx, ny = pose.neck()
    d = pose.dir
    if pose.torso:
        t = canvas(CELL, CELL)
        piv = cos.torso_pivot[d]
        blit(t, cos.torso[d], nx - piv[0] + pose.torso_off[0], ny - piv[1] + pose.torso_off[1])
        stack.append((pose.torso_z, "clothes", t, False))
    if pose.head is not False:
        h = canvas(CELL, CELL)
        key = pose.head or d
        hx, hy = PB.HEAD_PIVOT
        blit(h, PB.HEAD[key], nx - hx + pose.head_off[0], ny - hy + pose.head_off[1])
        stack.append((pose.head_z, "body", h, False))
    for leg in pose.legs:
        for i, (layer, img) in enumerate(_limb_leg(leg, cos)):
            stack.append((leg.z + i * 0.01, layer, img, False))
    for arm in pose.arms:
        for layer, img in _limb_arm(arm, cos):
            stack.append((arm.z, layer, img, arm.sep))
    if pose.cape is not None:
        stack.append((pose.cape_z, "cape", render_cape(pose.cape, (CELL, CELL), cos.cape_ramp), False))
    if pose.sword is not None:
        s = canvas(CELL, CELL)
        img, piv = PG.sword(pose.sword.angle, pose.sword.blade)
        stamp(s, img, pose.sword.grip, piv)
        stack.append((pose.sword.z, "weapon", s, True))
    if pose.lantern is not None:
        lt = canvas(CELL, CELL)
        name, at = pose.lantern
        stamp(lt, PG.LANTERN[name], at, PG.LANTERN_PIVOT[name])
        stack.append((pose.lantern_z, "lantern", lt, False))
    for it in pose.items:
        im = canvas(CELL, CELL)
        blit(im, it.img[:, ::-1] if it.flip else it.img, it.pos[0], it.pos[1])
        if it.layer == "auto":
            for k, (name, part) in enumerate(split_by_ramp(im).items()):
                stack.append((it.z + k * 0.001, name, part, False))
        else:
            stack.append((it.z, it.layer, im, False))

    stack.sort(key=lambda s: s[0])
    layers: dict[str, np.ndarray] = {}
    comp = canvas(CELL, CELL)
    for _, name, img, sep in stack:
        if sep:
            comp = separate(comp, img, 1)
        blit(comp, img, 0, 0)
        blit(layers.setdefault(name, canvas(CELL, CELL)), img, 0, 0)
    final = outline(comp)
    if pose.flip:
        final = flip_x(final)
        layers = {k: flip_x(v) for k, v in layers.items()}
    # aura (rastros, imagens-fantasma) fica por baixo da figura; fx (faíscas) por cima. Ambos em coordenada de tela.
    under = canvas(CELL, CELL)
    for dx, dy, ref in pose.ghosts:
        blit(under, ghost(final, ref), dx, dy)
    for a in pose.aura:
        blit(under, a, 0, 0)
    if (under >= 0).any():
        layers["aura"] = under.copy()
        blit(under, final, 0, 0)
        final = under
    for f in pose.fx:
        blit(final, f, 0, 0)
        blit(layers.setdefault("fx", canvas(CELL, CELL)), f, 0, 0)
    final = shift_y(final, Y_SHIFT)
    layers = {k: shift_y(v, Y_SHIFT) for k, v in layers.items()}
    return {"image": final, "layers": layers}


RAMP_LAYER = {"red": "cape", "skin": "body", "hair": "body", "cloth": "clothes", "cream": "clothes",
              "leather": "clothes", "steel": "weapon", "amber": "lantern", "breach": "lantern"}


def split_by_ramp(img: np.ndarray) -> dict[str, np.ndarray]:
    """Separa um desenho de corpo inteiro nas camadas do rig pela rampa de cada pixel (mantém a ordem de pintura)."""
    out: dict[str, np.ndarray] = {}
    for idx in np.unique(img[img >= 0]):
        name = RAMP_LAYER.get(PAL.ramp_of[int(idx)], "body")
        layer = out.setdefault(name, canvas(CELL, CELL))
        m = img == idx
        layer[m] = img[m]
    return out


def ghost(img: np.ndarray, ref: str) -> np.ndarray:
    """Silhueta chapada de uma cor (imagem-fantasma da investida/rolamento), borda um tom mais escura."""
    out = canvas(CELL, CELL)
    m = img >= 0
    col = PAL.ref(ref)
    out[m] = col
    edge = m & ~(neighbours(m, 1, 0) & neighbours(m, -1, 0) & neighbours(m, 0, 1) & neighbours(m, 0, -1))
    out[edge] = PAL.shift(col, -1)
    return out


def shift_y(img: np.ndarray, dy: int) -> np.ndarray:
    if dy == 0:
        return img
    out = canvas(CELL, CELL)
    if dy < 0:
        out[:dy] = img[-dy:]
    else:
        out[dy:] = img[:-dy]
    return out


def flip_x(img: np.ndarray) -> np.ndarray:
    """Espelha em torno da coluna do pé (ANCHOR x), não do meio da célula: a coluna 24 continua 24."""
    out = canvas(CELL, CELL)
    m = img[:, ::-1]
    out[:, 1:] = m[:, :-1]
    return out
