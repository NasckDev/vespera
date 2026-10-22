"""Poses especiais desenhadas inteiras (rolamento e corpo caído), pixel a pixel.

O esqueleto não consegue 'deitar' peças desenhadas em pé sem girar pixel, então estas poses são desenhos próprios.
Para continuarem trocáveis por estágio, o rig separa os pixels em camadas pela rampa (ver rig.split_by_ramp).
Todas olham para a DIREITA; a base do desenho encosta no chão.
"""
from pixcore import parse

# bola do rolamento: 1) encolhido, cabeça à frente  2) costas/capa para cima  3) botas por cima
BALL = {
    "tuck": parse("""
        ....BCDDC.....
        ..BCDEEDDCg...
        .BCDDDDCCghg..
        .CDDDCCCghihg.
        BCDCCCBghiihg.
        BCCCBBghhtsg..
        BCCBAMMghssIH.
        ACBAMNMMLqIJH.
        .ABKMMMLLGIIH.
        ..AKLLHIIHHG..
        ...FGHIIHGG...
        ....FGGGFF....
    """),
    "cape": parse("""
        ....BCDDCB....
        ..BCDEEDDCB...
        .BCDEDDDCCCB..
        .CDDDDCCCCCBA.
        BCDDCCCBCCBBA.
        BCDCCBCCCBBBA.
        BCCCBCCCBBBA..
        ABCCBCCBBBAg..
        .ABBCCBBAAghg.
        ..AABBAAghhg..
        ....HIIHggg...
        .....GGGF.....
    """),
    "feet": parse("""
        ......HIJH....
        .....GHIIHG...
        ..BCLMMLHHG...
        .BCDMNNMLKB...
        BCDDCMMLKCBA..
        BCDCCBLKCCBA..
        BCCCBBCCCBBA..
        ACCBghgCBBA...
        .ABghiihgBA...
        ..ghijhhhg....
        ...ghhhgg.....
    """),
}

# caído de bruços, cabeça para a direita (morte)
FALLEN = parse("""
    ........BCDDCB..........
    .....BCDDEEDDCCB...gghg.
    ...ABCDDDDDDCCCBBAghiihg
    FG.ABCCDCCDCCBBBAgghiihg
    GHIHKMMLLBCCBAAKLLghttsg
    GHIHKMMMLLLKKGHHIIqrsss.
    .FGGKKLLLKK..FGHHG.rrr..
""")
