"""Camada CORPO da Lâmina: cabeças (rosto + cabelo), mãos e pés. Desenhado pixel a pixel.

Convenção: desenhos de 'side' olham para a DIREITA; a esquerda é espelhada na exportação.
Pivôs (px, py) indicam o pixel que encaixa na junta da pose.
"""
from pixcore import parse, patch

# ---------------------------------------------------------------- cabeças (11 px de largura, 11 de altura)
# Cabelo cacheado castanho-escuro (f..j), luz de cima-esquerda. pivô: (5, 10) = centro do queixo/pescoço.
_DOWN = parse("""
    ..g.gg.gg..
    .gighjigg..
    gijhijihhg.
    gihjiihjihg
    ghiihhihhgg
    ghhgihghigg
    ghitthgtshg
    ghtftssfshg
    ghtftssfrhg
    .gtttssrsg.
    ...rssrr...
""")
_UP = parse("""
    ..g.gg.gg..
    .gighjigg..
    gijhijihhg.
    gihjiihjihg
    ghiihhihhgg
    ghhijhghigg
    gihhgihhhgg
    ghhihhgihgg
    gghhgghhggg
    .gghhghhgg.
    ...rssrr...
""")
_SIDE = parse("""
    ..g.gg.g...
    .gighjigg..
    gijhijihhg.
    gihjiihjihg
    ghiihhihgig
    ghhigihgtgg
    gihhggtttsg
    ghhhgqtsfsg
    ghhggtsssss
    .gghgrssrs.
    ..g..rrsr..
""")
HEAD = {
    "down": _DOWN,
    "down_blink": patch(_DOWN, "3,7=t; 7,7=s; 3,8=f; 2,8=t; 7,8=f; 8,8=r"),
    "down_hurt": patch(_DOWN, "3,7=f; 7,7=f; 3,8=t; 4,8=f; 6,8=f; 7,8=r; 5,9=q"),
    "down_slump": patch(_DOWN, "3,6=h; 7,6=h; 3,7=h; 7,7=g; 3,8=f; 4,8=f; 6,8=f; 7,8=f; 2,9=g; 9,9=g"),
    "up": _UP,
    "side": _SIDE,
    "side_blink": patch(_SIDE, "8,7=s; 8,8=f; 7,8=f"),
    "side_hurt": patch(_SIDE, "8,7=f; 7,7=f; 8,8=s; 8,9=q"),
    "side_slump": patch(_SIDE, "8,6=h; 9,6=g; 8,7=t; 8,8=f; 7,8=f; 10,8=."),
}
HEAD_PIVOT = (5, 10)

# ---------------------------------------------------------------- mãos (luva de couro), pivô = punho
HAND = {
    "front": parse("""
        JI
        IH
        .G
    """),
    "fist": parse("""
        JIH
        IHG
    """),
    "side": parse("""
        JI.
        IHG
    """),
    "back": parse("""
        IH
        HG
    """),
}
HAND_PIVOT = {"front": (0, 0), "fist": (1, 0), "side": (0, 0), "back": (0, 0)}

# ---------------------------------------------------------------- pés (bota), pivô = tornozelo
FOOT = {
    "down": parse("""
        IIHG
        HHGG
        GFFF
    """),
    "up": parse("""
        IHHG
        HGGG
        .FF.
    """),
    "side": parse("""
        IIHH..
        HHHHHG
        GFFFFF
    """),
    # ponta para baixo (pé de trás saindo do chão / pé no ar)
    "side_toe": parse("""
        IIH.
        HHHG
        .GGF
        ..F.
    """),
    # sola para cima (rolamento/queda)
    "side_heel": parse("""
        .IHG
        HHGF
        GF..
    """),
}
FOOT_PIVOT = {"down": (1, 0), "up": (1, 0), "side": (1, 0), "side_toe": (1, 0), "side_heel": (2, 0)}
