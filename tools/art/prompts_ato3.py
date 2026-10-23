"""Prompts de arte do Ato III (Mercado das Marés / Cais Afogado) e cenários (hub + arena) dos Atos IV, V e VI.

Uso: python -I tools/art/prompts_ato3.py  →  docs/v2/prompts/ato3_*.md, ato4_A_cenarios.md, ato5_A_cenarios.md, ato6_A_cenarios.md
Conteúdo tirado de docs/v2/CAMPANHA.md (bíblia da Parte 1). Reaproveita o gerador do Ato I (gerar_prompts.py).
Os cenários anexam, além das referências da v2, mapas aprovados do Ato I reduzidos (publicados no branch campanha).
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))  # -I tira a pasta do script do sys.path
import gerar_prompts as g  # noqa: E402

REPO_CAMPANHA = "https://raw.githubusercontent.com/NasckDev/vespera/campanha/docs/v2/referencias/"

# Referências de cenário: (url, rótulo). As da v2 usam o link do branch v2; as novas, o do branch campanha.
REF_PRACA = (g.REPO + "estilo_mapa_praca.png", "mapa aprovado (estilo e câmera)")
REF_PRANCHA = (g.REPO + "estilo_prancha_mapas.png", "prancha de mapas (estilo)")
REF_BAIRRO = (REPO_CAMPANHA + "estilo_bairro_forjas.png", "bairro aprovado do Ato I (Forjas)")
REF_SALA = (REPO_CAMPANHA + "estilo_sala_esgoto.png", "sala de Brecha aprovada do Ato I (corredor)")
REF_ARENA = (REPO_CAMPANHA + "estilo_arena_esgoto.png", "arena aprovada do Ato I (Lodo-Mãe)")
REFS_BY_KIND = {"hub": [REF_PRACA, REF_BAIRRO], "sala": [REF_SALA, REF_PRANCHA], "arena": [REF_ARENA, REF_PRANCHA]}

BREACH = ("Inside a Breach the Underside copies this place exactly as it was BEFORE the Year Without Dawn, twenty years ago, "
          "but kept too long: rotten, partly inverted, lit by faint magenta rift light. ")

# (id, arquivo, título, tipo, descrição)
MAPS_ATO3 = [
    ("A13", "chao_mares.png", "Mercado das Marés (bairro)", "hub",
     "The Tide Market harbour neighborhood of Lumeira at dusk, in sea fog: wooden piers and a cobblestone quay (walkable) crossing "
     "the image left to right. Along the top edge: a fish market with canvas stalls and crates of fish, a harbour tavern with a "
     "warm window, and an old boat shed with nets hanging to dry. In the top-right corner, on a rocky point, a tall stone "
     "LIGHTHOUSE whose lamp is DARK (unlit for twenty years). Along the bottom edge: moored fishing boats on teal water and a long "
     "stone breakwater wall with many small names carved into it (no readable letters, just scratch marks). Lantern posts with "
     "amber light along the quay. Quay exits centered on the LEFT and RIGHT edges. The middle of the quay is free of objects."),
    ("A14", "chao_cais_pier.png", "Cais Afogado: Píer das Bandeirolas", "sala",
     "A dungeon room inside the 'Drowned Docks' Breach. " + BREACH + "It is the harbour on the night of the Lantern Festival, the "
     "minute the great tide rose: a straight flooded wooden PIER running left to right through the middle, open exits centered on "
     "the LEFT and RIGHT edges. Above and below the pier: dark sea water with floating unlit paper lanterns, festival bunting "
     "hanging soaked from poles, puddles on the planks, mooring posts, broken crates. Upside-down boats float high in the dark sky "
     "area at the top edge. Rain drops rising upwards. Dim cold light."),
    ("A15", "chao_cais_mercado.png", "Cais Afogado: Mercado Submerso", "sala",
     "A dungeon room inside the 'Drowned Docks' Breach. " + BREACH + "A plus-shaped crossing of wet cobblestone and planks with four "
     "open exits exactly centered on the top, bottom, left and right edges. In the four corners: tipped-over fish market stalls "
     "with torn canvas, fish frozen in mid-air above them, tangled nets, baskets, sea water pooling. A round iron mooring bollard "
     "in the center of the crossing. Thin magenta cracks between the stones."),
    ("A16", "chao_cais_barco.png", "Cais Afogado: Barco Virado (descanso)", "sala",
     "A dungeon room inside the 'Drowned Docks' Breach: a small peaceful REST room INSIDE the hull of a huge capsized fishing boat, "
     "seen from above: curved wooden ribs of the hull form the walls, dry planks as the floor, an old hanging net, a small stool "
     "and a child's drawing of a lighthouse pinned to a beam. An empty lantern post in the center (leave a flat spot). Soft amber "
     "light breaking through gaps in the planks. Single open entrance centered on the BOTTOM edge, one exit centered on the TOP edge. "
     "Walkable center free of objects."),
    ("A17", "chao_cais_quebramar.png", "Cais Afogado: Quebra-mar dos Postes (tesouro)", "sala",
     "A dungeon room inside the 'Drowned Docks' Breach: a wide stone BREAKWATER leading up to a raised platform at the top-center "
     "(leave the platform EMPTY, a chest will be placed there later). Both sides of the breakwater are lined with rows of tall "
     "iron lantern posts, all UNLIT, dozens of them. Rough sea crashing on both sides, foam, seaweed on the stones, a few magenta "
     "glowing crystals between the rocks. Single open entrance centered on the BOTTOM edge."),
    ("A18", "chao_cais_arena.png", "Cais Afogado: arena do Farol Afogado", "arena",
     "The BOSS ARENA of the 'Drowned Docks' Breach: a large round ROCKY PLATFORM at the foot of an enormous dark lighthouse that "
     "rises at the top edge of the image (only its base and lower tower visible, huge, cracked, covered in barnacles). Stormy sea "
     "all around the platform, waves breaking on the rim, broken boat planks and festival bunting caught on the rocks, magenta "
     "light in the cracks. A wide flat walkable center (completely empty) and a single entrance (a short plank bridge) centered "
     "on the BOTTOM edge."),
]

MAPS_ATO4 = [
    ("A19", "chao_cemiterio.png", "Cemitério das Lanternas (bairro)", "hub",
     "The Cemetery of Lanterns of Lumeira at dusk: a gentle hill of graves where EVERY grave has its own small lit lantern, hundreds "
     "of warm amber lights. Gravel paths (walkable) climb through the middle of the image. At the top-center a small stone chapel "
     "with a closed door and, next to it, an old wooden bench under a cypress (the Watcher's bench). Low stone walls, cypress "
     "trees, iron gates, wilted flowers. A small Order kiosk with a lantern sign near the left path. Path exits centered on the "
     "LEFT, RIGHT and BOTTOM edges. The middle of the paths is free of objects."),
    ("A20", "chao_cemiterio_arena.png", "Cemitério Invertido: arena da Velória", "arena",
     "The BOSS ARENA of the 'Inverted Cemetery' Breach. " + BREACH + "A large round stone ALTAR courtyard covered in thousands of "
     "melted candles of every size, wax dripping UPWARDS, puddles of hardened wax on the floor, hanging upside-down tombstones "
     "and cypress roots growing from the dark sky area at the top edge. A ring of tall candelabras around the edge. A wide flat "
     "walkable center (completely empty) and a single entrance centered on the BOTTOM edge."),
]

MAPS_ATO5 = [
    ("A21", "chao_torre.png", "Torre do Sino (bairro)", "hub",
     "The Bell Tower district of Lumeira at dusk, headquarters of the Order of the Lantern: a large stone courtyard (walkable) with "
     "a training yard (wooden dummies and racks of practice weapons against the walls). At the top-center a tall stone BELL "
     "TOWER with a huge bronze bell visible in its open belfry (the bell hangs still, silent). To the left, a stately archive "
     "building with tall windows; to the right, a smaller office with an iron-bound treasury door. Order banners with a small "
     "golden lantern emblem (no letters). Lantern posts with amber light. Exits centered on the LEFT, RIGHT and BOTTOM edges. "
     "The middle of the courtyard is free of objects."),
    ("A22", "chao_torre_arena.png", "Torre do Sino Mudo: arena do Sineiro Mudo", "arena",
     "The BOSS ARENA of the 'Silent Bell Tower' Breach. " + BREACH + "The inside of the belfry at the top of the tower on the dawn "
     "after the great storm: a large round stone floor ringed by broken stone arches open to a dark sky, long bell ropes "
     "hanging from the darkness above, archive papers floating weightless in the air, stone gargoyle perches on the rim, the "
     "shadow of a giant bell on the floor. A wide flat walkable center (completely empty) and a single entrance (a stair "
     "opening) centered on the BOTTOM edge."),
]

MAPS_ATO6 = [
    ("A23", "chao_ruas_baixas.png", "Ruas Baixas (bairro)", "hub",
     "The Lower Streets of Lumeira at dusk, a poor but lively neighborhood: narrow alleys (walkable) between wooden houses on "
     "stilts, clotheslines with colorful laundry crossing above the alleys, potted plants, crates, a stray cat. At the top-center "
     "Aunt Nilza's tavern: a warm two-storey wooden house with a big lit window, a hanging soup-pot sign (no letters) and benches "
     "outside. On the right side an EMPTY dirt lot with the stone outlines of demolished houses still visible on the ground. "
     "Paper lanterns with amber light. Exits centered on the LEFT, RIGHT and BOTTOM edges. The middle of the alleys is free of objects."),
    ("A24", "chao_ruas_arena.png", "Ruas Invertidas: arena do Inquilino", "arena",
     "The BOSS ARENA of the 'Inverted Streets' Breach. " + BREACH + "The empty lot of the Lower Streets, but with the demolished "
     "houses standing again around it as broken ghostly walls, doors opening onto the dark sky, clotheslines stretched in every "
     "direction above, scattered furniture (chairs, a crib, drawers) on the ground near the edges, deep stilt holes in the dirt. "
     "A wide flat walkable center (completely empty) and a single entrance centered on the BOTTOM edge."),
]

MONSTERS_ATO3 = [
    ("afogado", "Afogado (comum, rank C)",
     "a drowned sailor echo: a big heavy figure in a rusty brass diving helmet with a cracked glass window, a soaked rotten sea "
     "coat, a thick rope tied around the waist, seaweed and barnacles on the shoulders, sea water always dripping. SCALE: a bit "
     "taller and much bulkier than a person (about 1.1x the hunter height)",
     [("parado", 4, "idle, heavy breathing, water dripping"), ("mover", 6, "slow heavy walk forward, dragging its feet"),
      ("aviso", 3, "warning: stops dripping, raises both arms high, the helmet window glows bright amber"),
      ("ataque", 4, "double-fist slam forward with a splash of water"), ("morte", 6, "falls to its knees and collapses into a puddle, the helmet rolls away")]),
    ("lanterna_afogada", "Lanterna-Afogada (comum, rank C)",
     "a festival paper lantern echo: a round paper lantern full of murky sea water, the paper soaked and torn, a small candle "
     "flame still burning inside the water, a dangling string, floating in the air with no body. SCALE: small, about the size "
     "of a person's head, floating at shoulder height (about 0.3x the hunter height)",
     [("parado", 4, "floating idle, bobbing up and down"), ("mover", 6, "drifting quickly through the air"),
      ("aviso", 3, "warning: the candle inside glows bright amber and the paper swells with water"),
      ("ataque", 4, "tips over and pours a heavy circle of sea water downwards"), ("morte", 6, "the paper bursts, the water splashes out and the flame goes out")]),
    ("ancoreiro", "Ancoreiro (comum, rank C)",
     "a big crab echo with a rusty iron ANCHOR instead of its right claw, the anchor attached to its body by a heavy chain, "
     "dark barnacled shell, small claw on the left. SCALE: low and wide, about 0.6x the hunter height",
     [("parado", 4, "idle, clicking the small claw"), ("mover", 6, "scuttling sideways"),
      ("aviso", 3, "warning: raises the anchor, the chain rattles and the anchor glows amber"),
      ("ataque", 4, "throws the anchor forward on its chain in a straight line and pulls it back"), ("morte", 6, "flips onto its back, the anchor drops with a thud")]),
    ("farol_afogado", "Farol Afogado (Guardião, chefe rank C)",
     "a HUGE lighthouse-keeper boss made of green-stained BRONZE, a giant humanoid in a bronze sea coat with two big shoulder "
     "pads, and instead of a head it has the glass LAMP ROOM of a lighthouse with a big lens inside, barnacles and seaweed on "
     "its legs, sea water pouring from its joints. SCALE: about 3x the hunter height",
     [("parado", 6, "idle, the lens glowing faintly, water pouring"), ("feixe", 8, "sweeps a long amber light beam from its head lamp across the ground"),
      ("pisada", 8, "raises a huge foot and stomps, a ring of water splashes out"),
      ("chamar", 8, "raises both arms and calls drowned sailors up from the water around it"),
      ("apagar", 8, "phase 2: its head lamp flickers and goes completely dark, only a faint glow in the lens remains"),
      ("morte", 10, "kneels, the head lamp lights up warm and bright again, then it slowly sinks and crumbles")]),
]

NPCS_ATO3 = [
    ("severo", "Capitão Severo",
     "a grumpy harbour master around 60, the old lighthouse keeper: a thick grey-white beard, weathered sunburned face, a "
     "dark blue wool sailor coat with brass buttons and a knitted cap, rope-scarred calloused hands, smells of salt; a normal "
     "adult man (1.0x the hunter height)", ("neutral, grumpy and closed", "pained, guilty, eyes wet")),
    ("iara", "Iara",
     "a fishmonger woman around 32, Severo's daughter: dark hair tied up under a headscarf, strong arms, a worn leather apron "
     "over rolled-up sleeves, a fish knife on the belt, a tired and ironic look; a normal adult woman (1.0x the hunter height)",
     ("neutral, tired and ironic", "singing softly, eyes closed")),
]


def block(pid, filename, title, refs, body):
    """Mesmo formato de gerar_prompts.block, mas com URLs completas (referências em mais de um branch)."""
    lines = [f"### {pid} · `{filename}` · {title}", "",
             "**Anexe estas referências antes de mandar:** " + " · ".join(f"[{label}]({url})" for url, label in refs), "",
             "```", *body, "```", ""]
    return "\n".join(lines)


def maps(title, intro, entries):
    out = [f"# {title}", "", "Um prompt por imagem. Cole o bloco inteiro e anexe as referências indicadas. " + intro, ""]
    for pid, fn, name, kind, desc in entries:
        body = [g.WORLD, "", "Please create ONE game map background image for this game.", "", "WHAT TO DRAW: " + desc, "",
                g.STYLE, g.MAP_CAMERA, g.MAP_FORMAT, g.NEVER]
        out.append(block(pid, fn, name, REFS_BY_KIND[kind], body))
    return "\n".join(out)


def _swap(**values):
    old = {k: getattr(g, k) for k in values}
    for k, v in values.items():
        setattr(g, k, v)
    return old


INTRO_MAPS = ("Os mapas do Ato I (Praça, Forjas, esgoto) estão aprovados e são o modelo de estilo; as referências anexadas "
              "mostram o padrão de bairro (hub), de sala de Brecha e de arena.")


def main():
    out = g.OUT
    out.mkdir(parents=True, exist_ok=True)
    old = _swap(MONSTERS=MONSTERS_ATO3, NPCS=NPCS_ATO3)
    try:
        files = {
            "ato3_A_cenarios.md": maps("Ato III · Cenários do Mercado das Marés e do Cais Afogado (6 imagens)", INTRO_MAPS, MAPS_ATO3),
            "ato3_C_monstros.md": g.monsters().replace("Monstros do Ato I", "Monstros do Ato III (Cais Afogado)"),
            "ato3_D_severo_iara.md": g.portraits().replace("Retratos de diálogo dos NPCs", "Capitão Severo e Iara: retratos")
                                       .replace("(a Lis é criança)", "(Severo tem ~60 anos; Iara, ~32)")
                                     + "\n\n" + g.npc_walks().replace("NPCs andando pela cidade", "Capitão Severo e Iara andando"),
            "ato4_A_cenarios.md": maps("Ato IV · Cenários do Cemitério das Lanternas (2 imagens: bairro e arena)", INTRO_MAPS, MAPS_ATO4),
            "ato5_A_cenarios.md": maps("Ato V · Cenários da Torre do Sino (2 imagens: bairro e arena)", INTRO_MAPS, MAPS_ATO5),
            "ato6_A_cenarios.md": maps("Ato VI · Cenários das Ruas Baixas (2 imagens: bairro e arena)", INTRO_MAPS, MAPS_ATO6),
        }
    finally:
        _swap(**old)
    for name, text in files.items():
        (out / name).write_text(text, encoding="utf-8")
        print(name, text.count("### "), "prompts", len(text), "chars")


if __name__ == "__main__":
    main()
