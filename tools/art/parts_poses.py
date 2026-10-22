"""Poses especiais desenhadas inteiras (rolamento e corpo caído), pixel a pixel.

O esqueleto não consegue 'deitar' peças desenhadas em pé sem girar pixel, então estas poses são desenhos próprios.
Para continuarem trocáveis por estágio, o rig separa os pixels em camadas pela rampa (ver rig.split_by_ramp).
Todas olham para a DIREITA; a base do desenho encosta no chão.
"""
from pixcore import parse

# bola do rolamento: 1) encolhido, cabeça à frente  2) costas/capa para cima  3) botas por cima
BALL = {
    "tuck": parse("""
        .....BCDDCB.....
        ...BCDEEEDCB....
        ..BCDDEDDDCCgg..
        .BCDDDDDCCCghig.
        .CDDDDCCCCghiihg
        BCDDDCCCCBghjihg
        BCDDCCCCBghhiihg
        BCDCCCCBBghttshg
        BCCCCCBBAMgtssqg
        ACCCCBBAMMLGIJHg
        ACCCBBAMNMMLIJIH
        .ACBBAKMMMLLGIIH
        ..AAAKLLLHHIIHG.
        ....FGHIIHHGGF..
        .....FFGGGFF....
    """),
    "cape": parse("""
        .....BCDDCB.....
        ...BCDEEEDDCB...
        ..BCDEEDDDDCCB..
        .BCDDEDDDCCCCBA.
        .CDDDDDCCCCCBBA.
        BCDDDDCCCBCCBBA.
        BCDDDCCCBCCCBBAA
        BCDDCCCBCCCBBBA.
        BCCCCCBCCCBBBAA.
        ACCCCBCCCBBBAA..
        ACCCBBCCBBBAAgg.
        .ACBBBBBBAAghhig
        ..AAABBAAghhiig.
        ....FGHHGFgggg..
        .....FGGGF......
    """),
    "feet": parse("""
        .......GHIJH....
        ......GHIJJIH...
        .....GHHIIIHG...
        ...BCMLLHHHGG...
        ..BCDMNNMLLKB...
        .BCDDCMMNMLKCB..
        BCDDDCCMMLKCCBA.
        BCDDCCCBLKCCCBA.
        BCDCCCBBBCCCBBA.
        BCCCCBBgggCCBBA.
        ACCCBghhiigCBA..
        .ACBghijihhgA...
        ..AAghiihhhg....
        ....ggghhgg.....
        .....gggg.......
    """),
}

# poeira do passo/rolamento/queda (rampa pedra)
DUST = {
    "small": parse("""
        .21.
        2112
        .32.
    """, {"1": "stone:5", "2": "stone:4", "3": "stone:3"}),
    "big": parse("""
        ..21..2.
        .21112..
        2111123.
        .3223.3.
    """, {"1": "stone:5", "2": "stone:4", "3": "stone:3"}),
}

# caído de bruços, cabeça para a direita (morte)
FALLEN = parse("""
    ..........BCDDCB.............
    .......BCDDEEDDCCB...gghhg...
    .....ABCDDDDEDDCCBBAghijihg..
    ...AABCDDDDDDDCCCBBghiiihhg..
    FGG.ABCCDCCDCCBBBAAghhihhhg..
    GHIHKMMLLBCCCBBAAKLghhgttsgg.
    GHIIHMMMLLLLKKQRRQPgrtsssrIJH
    .FGGKKLLLKKK..OPQPPOqrrrrqHIH
    ..FF...................FGGF..
""")
