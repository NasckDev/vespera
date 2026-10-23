"""Gera um prompt autônomo por imagem de arte da v2 em docs/v2/prompts/*.md.

Cada prompt traz todo o contexto (mundo, estilo, câmera, formato, proibições) e links diretos
para as referências publicadas em docs/v2/referencias/, para qualquer pessoa ou IA usar sem contexto.
Uso: python -I tools/art/gerar_prompts.py
"""
from pathlib import Path

REPO = "https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/"
OUT = Path(__file__).resolve().parents[2] / "docs" / "v2" / "prompts"

WORLD = ("I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. "
         "Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called "
         "Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. "
         "Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. "
         "Mood: melancholic but hopeful; cozy warm town versus eerie rifts.")
STYLE = ("STYLE: detailed hand-painted pixel art, crisp pixel clusters, dark selective outlines, no blur, no smooth vector "
         "look, no 3D render look. Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, "
         "violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, "
         "water teal #3C7A8A. Light always comes from the TOP-LEFT. Match the attached reference images as closely as possible.")
NEVER = "Do NOT add: text, letters, numbers, logos, watermark, UI, frame or border, grid lines, extra characters."

MAP_REFS = [("estilo_mapa_praca.png", "mapa aprovado (estilo e câmera)"), ("estilo_prancha_mapas.png", "prancha de mapas (estilo)")]
MAP_CAMERA = ("CAMERA: straight top-down view, everything axis-aligned like the attached plaza: walls, streets and exits run "
              "perfectly horizontal or vertical, no diagonal or isometric angle. Walls and objects are seen slightly from the front "
              "(3/4 top-down), the floor from above.")
MAP_FORMAT = ("FORMAT: one single full image, landscape 16:9, the highest resolution you can produce (at least 1600 px wide). "
              "Show only this one map. The walkable floor must be clearly readable and free of characters and creatures. "
              "Exits must be wide openings exactly centered on their wall.")

MAPS = [
    ("A2", "chao_forjas.png", "Bairro das Forjas (rua)",
     "The Forge District street of Lumeira at dusk: a wide cobblestone street running left to right across the image "
     "(walkable), with a raised sidewalk. Along the top edge, soot-darkened stone workshops with chimneys and smoke; in the "
     "top-center a large forge building with a wide double wooden gate (closed) and a glowing furnace window. Barrels, coal "
     "piles and anvils stand against the buildings, never in the middle of the street. Bottom edge: low stone wall with a "
     "sewer grate. Street exits open on the left and right edges, centered vertically."),
    ("A3", "chao_forja_interior.png", "Forja da Odete (interior)",
     "Interior of Odete's smithy, seen from above: stone and wooden-plank floor (walkable) in the middle of the room, back "
     "wall at the top with tool racks, hammers and tongs; a big glowing furnace built into the LEFT wall casting warm orange "
     "light; a workbench and an anvil against the walls; a single exit door centered on the BOTTOM wall. The center of the "
     "room is empty floor."),
    ("A4", "chao_esgoto_corredor.png", "Esgotos do Mercado: corredor",
     "A dungeon room inside a Breach: the 'Market Sewers', an inverted sewer made of things the city threw away. A straight "
     "stone CORRIDOR running left to right through the middle of the image, with open exits centered on the LEFT and RIGHT "
     "edges. Above and below the corridor: channels of glowing green sludge, rusty pipes, broken plates, forks and toys fused "
     "into the walls, thin glowing magenta cracks in the stone. Dim light."),
    ("A5", "chao_esgoto_cruzamento.png", "Esgotos do Mercado: cruzamento",
     "A dungeon room inside the 'Market Sewers' Breach (an inverted sewer made of things the city threw away): a stone "
     "CROSSROAD shaped like a plus sign, with four open exits exactly centered on the top, bottom, left and right edges. In "
     "the four corners: pools of glowing green sludge, rusty pipes, broken plates, cutlery and toys fused into the walls. "
     "Thin glowing magenta cracks in the floor. A round iron drain cover in the center of the crossing."),
    ("A6", "chao_esgoto_tesouro.png", "Esgotos do Mercado: sala do tesouro",
     "A dungeon room inside the 'Market Sewers' Breach: a rectangular TREASURE ROOM with a single open entrance centered on "
     "the BOTTOM edge, and a raised stone dais with steps at the top-center (leave the dais EMPTY, a chest will be placed "
     "there later). Glowing magenta crystals in the corners, sludge channels along the side walls, broken plates and toys "
     "fused into the walls, magenta cracks in the floor."),
    ("A7", "chao_esgoto_arena.png", "Esgotos do Mercado: arena do Guardião",
     "The BOSS ARENA of the 'Market Sewers' Breach: a large circular stone chamber, a wide round walkable floor in the "
     "middle surrounded by a ring of glowing green-and-magenta sludge, broken pipes pouring sludge, piles of plates, cutlery "
     "and toys around the edge. A single open entrance centered on the BOTTOM edge. The center must be completely empty."),
]

CLASSES = {
    "lamina": ("young hunter with messy dark-brown hair (no hood), a short red scarf, dark steel armor with brass trim, a brass "
               "lantern in the left hand, holding a steel sword in the right hand",
               "Lâmina (espada)"),
    "arco": ("hooded hunter with a deep red-violet hood and cape, light leather armor, an old brass lantern on the belt, holding a wooden bow",
             "Arco"),
    "martelo": ("stocky armored hunter with a red cape, heavy leather-and-iron armor, an old brass lantern on the belt, holding a big two-handed war hammer",
                "Martelo"),
}
DIRS = [("baixo", "facing DOWN, towards the camera"), ("cima", "facing UP, back turned to the camera"), ("lado", "facing RIGHT, side view")]

COMMON_ANIMS = [
    ("parado", 4, "idle: standing still and breathing, the cape moving slightly", True),
    ("esquiva", 5, "dodge roll: a quick forward roll and back on the feet", True),
    ("dano", 2, "getting hit: flinches backwards", True),
    ("morte", 7, "death: falls to the ground; in the last frame the lantern rolls away and goes out", False),
]
CLASS_ANIMS = {
    "lamina": [
        ("ataque1", 5, "first sword slash: wind-up, fast horizontal swing leaving a glowing amber arc trail, recovery", True),
        ("ataque2", 5, "second sword slash: backhand diagonal swing with a glowing amber arc trail, recovery", True),
        ("ataque3", 5, "finishing strike: big overhead downward slash with a stronger amber trail and a small impact spark", True),
        ("pesado", 8, "heavy attack: 2 frames charging (the sword glows amber) then 6 frames of a full spinning slash", False),
    ],
    "arco": [
        ("tiro", 6, "shooting: draws the bow, aims, releases an arrow (the arrow leaves the frame), recoil", True),
        ("tiro_carregado", 7, "charged shot: 2 frames pulling the string while the arrow glows amber, then 5 frames releasing a glowing piercing arrow", True),
    ],
    "martelo": [
        ("ataque1", 6, "slow heavy hammer swing from the side, ends hitting the ground with dust", True),
        ("ataque2", 6, "upward hammer swing that lifts dust and small stones", True),
        ("pesado", 9, "heavy attack: 2 frames charging (the hammer head glows amber) then 7 frames of a jump and a huge ground slam with a shockwave ring", False),
    ],
}

MONSTERS = [
    ("lodo", "Lodo (slime comum, rank E)",
     "a small slime made of kitchen scraps: translucent greenish-brown goo with broken plates and spoons stuck inside and one glowing amber core",
     [("parado", 4, "idle wobble"), ("pulo", 6, "crouches and jumps forward"), ("aviso", 3, "warning pose before attacking: squashes down and the core glows bright amber"),
      ("dano", 2, "hit: splashes and flashes"), ("morte", 6, "death: bursts into a puddle, the plates fall out")]),
    ("espectro", "Espectro (fantasma comum, rank E)",
     "a floating ghost shaped like an empty old coat with a torn veil where the face should be, faint violet glow, no legs",
     [("parado", 4, "floating idle, the veil drifting"), ("mover", 6, "chasing forward through the air"), ("aviso", 3, "warning: the veil flares and glows amber"),
      ("ataque", 4, "lunges forward with the coat sleeves"), ("morte", 6, "dissolves into violet mist")]),
    ("lodo_mae", "Lodo-Mãe (Guardião, chefe rank E)",
     "a HUGE mother slime boss, a round gooey mass of sludge like a giant jelly, holding plates, cutlery and children's toys inside, three glowing amber cores. It is a SLIME: NOT roots, NOT tentacles, NOT a tree",
     [("parado", 6, "idle, slowly pulsing"), ("pulo", 8, "big jump: squashes, launches up, lands with a splash"), ("engolir", 8, "swallow attack: opens a huge gooey mouth and lunges"),
      ("dividir", 8, "splits into three smaller slimes"), ("morte", 10, "death: collapses and melts, the toys and plates spill out")]),
]

NPCS = [
    ("odete", "Odete", "a blacksmith woman in her mid-fifties, grey-streaked hair tied back, strong scarred arms, leather apron, soot on her face, she never smiles", ("neutral, stern", "angry")),
    ("benicio", "Mestre Benício", "a very OLD man around 70, master of the Order of the Lantern: bald head with thin white hair at the sides, long white beard, deep wrinkles, his LEFT eye is blind (milky white with a vertical scar), dark grey monk-like hooded robe with a small golden lantern emblem on the chest, formal and severe, NOT a young or middle-aged man, NOT dark hair, NOT fur", ("neutral, composed, looking wise", "stern and severe, frowning, mouth tight")),
    ("ravi", "Ravi", "an 18-year-old rival hunter, messy dark hair, patched clothes, a burned E-shaped seal on his wrist, cocky grin", ("neutral, confident", "laughing")),
    ("lis", "Lis", "an 11-year-old barefoot street girl, a CHILD, short tangled hair, oversized coat, a satchel full of rolled hand-drawn maps", ("neutral, curious", "suspicious, squinting")),
    ("nilza", "Tia Nilza", "a warm middle-aged tavern keeper, round face, headscarf, apron, a wooden spoon in hand, motherly", ("neutral, kind", "big warm smile")),
]


def link(name, label):
    return f"[{label}]({REPO}{name})"


def block(pid, filename, title, refs, body):
    lines = [f"### {pid} · `{filename}` · {title}", "",
             "**Anexe estas referências antes de mandar:** " + " · ".join(link(n, l) for n, l in refs), "",
             "```", *[line for line in body if line is not None], "```", ""]
    return "\n".join(lines)


def sprite_body(subject, anim_desc, frames, facing, extra):
    return [WORLD, "", f"Please create ONE sprite strip for this game. Subject: {subject}.",
            f"Animation: {anim_desc}. Exactly {frames} frames. {facing}.", extra,
            STYLE, "",
            f"FORMAT: the {frames} frames side by side in ONE single horizontal row, every frame the same square size "
            "(256x256 or larger), evenly spaced, the subject centered in each frame with the feet on the same baseline. "
            "Background: flat solid pure green #00FF00 everywhere (it will be removed), no ground shadow. "
            "The SAME character in every frame: same face, clothes, colors, proportions and size; only the pose changes, "
            "so the frames play as smooth animation. SCALE: the character must have exactly the same size as in the attached "
            "reference strips (do not zoom in, even for close actions like getting hit).", NEVER]


def maps():
    out = ["# Parte A · Cenários (6 imagens)", "",
           "Um prompt por imagem. Cole o bloco inteiro e anexe as referências indicadas. A Praça (A1) já está aprovada e serve de modelo.", ""]
    for pid, fn, title, desc in MAPS:
        body = [WORLD, "", "Please create ONE game map background image for this game.", "", "WHAT TO DRAW: " + desc, "",
                STYLE, MAP_CAMERA, MAP_FORMAT, NEVER]
        out.append(block(pid, fn, title, MAP_REFS, body))
    return "\n".join(out)


def hunter(cls):
    subject, label = CLASSES[cls]
    refs = [(f"{cls}_andar_{d}.png", f"caminhada {d}") for d, _ in DIRS] + [("cacador_folha_original.png", "folha original do caçador")]
    out = [f"# Parte B · Caçador {label}, estágio 1", "",
           "Um prompt por imagem. Anexe as tiras de caminhada (mostram o personagem exato) e a folha original.", ""]
    extra = ("The character is shown in the attached walking strips; copy its exact look. "
             "Stage 1 = apprentice hunter, simple worn gear.")
    n = 1
    for anim, frames, desc, all_dirs in CLASS_ANIMS[cls] + COMMON_ANIMS:
        for d, facing in (DIRS if all_dirs else [DIRS[2]]):
            out.append(block(f"B-{cls}-{n:02d}", f"{cls}_e1_{anim}_{d}.png", f"{anim} ({d})", refs,
                             sprite_body(subject, desc, frames, facing, extra)))
            n += 1
    return "\n".join(out)


def hunter_restyle(cls, style_classes):
    """Refaz uma classe no mesmo estilo das outras, para o trio parecer do mesmo jogo."""
    subject, label = CLASSES[cls]
    out = [f"# Caçador {label}, estágio 1 — refazer no estilo do Arco e do Martelo", "",
           "No playtest as 3 classes pareciam de jogos diferentes. Refaça o Lâmina no estilo dos outros dois. "
           "Um prompt por imagem; anexe as 3 referências indicadas (pose, identidade, estilo).", "",
           "> **NÃO aceito montagem por código.** A entrega anterior girou/recortou os pixels da imagem de frente "
           "(\"pixel rig\"): de lado o rosto virou mancha, de costas virou bloco, e a esquiva era a imagem girando. "
           "Cada quadro tem que ser DESENHADO pelo gerador de imagem (GPT/Gemini), como foram o Arco e o Martelo.", ""]
    anims = [("andar", 6, "walking cycle, legs and arms visibly swinging", True)] + CLASS_ANIMS[cls] + COMMON_ANIMS
    n = 1
    for anim, frames, desc, all_dirs in anims:
        for d, facing in (DIRS if all_dirs else [DIRS[2]]):
            pose = f"{cls}_andar_{d}.png" if anim == "andar" else f"{cls}_pose_{anim}_{d}.png"
            style = style_classes[0] if d != "baixo" or len(style_classes) == 1 else style_classes[1]
            refs = [(pose, "1 · guia de pose (tira antiga)"), (f"{cls}_novo_estilo.png", "2 · identidade nova"),
                    (f"{style}_andar_{d}.png", f"3 · estilo ({style}, {d})")]
            extra = ("THREE images are attached. IMAGE 1 is the POSE GUIDE: an older strip of this same animation. Copy its "
                     "number of frames, the order of the poses, the body, arm, leg and sword positions and the motion of every "
                     "frame exactly, but IGNORE its drawing style. IMAGE 2 is the CHARACTER: copy his face, messy dark-brown "
                     "hair (no hood), short red scarf, dark steel armor with brass trim, steel sword and brass lantern exactly. "
                     f"IMAGE 3 is the ART STYLE: the {style} hunter of the same game; same chunky proportions, head size, thick "
                     "dark outline, shading and palette, so the three hunters look like one trio. "
                     "DRAW every frame as new artwork. Do NOT produce frames by rotating, warping, cutting or pasting the pixels "
                     "of the attached images, and do NOT write code to build them: a side or back view must be really drawn "
                     "from that angle (side view shows the profile; back view shows the back of the head and the back of the armor).")
            out.append(block(f"R-{cls}-{n:02d}", f"{cls}_e1_{anim}_{d}.png", f"{anim} ({d})", refs,
                             sprite_body(subject, desc, frames, facing, extra)))
            n += 1
    return "\n".join(out)


def monsters():
    refs = [("estilo_monstros.png", "estilo de monstros"), ("estilo_chefes.png", "estilo de chefes")]
    out = ["# Parte C · Monstros do Ato I", "",
           "Um prompt por imagem. Gere primeiro o `parado` de cada monstro e anexe-o nos pedidos seguintes do mesmo monstro.", ""]
    extra = "If an earlier strip of this same monster is attached, keep its design identical."
    n = 1
    for mid, title, subject, anims in MONSTERS:
        for anim, frames, desc in anims:
            out.append(block(f"C-{n:02d}", f"{mid}_{anim}.png", f"{title}: {anim}", refs,
                             sprite_body(subject, desc, frames, "side view facing RIGHT", extra)))
            n += 1
    return "\n".join(out)


def portraits():
    refs = [("estilo_npcs_proporcao.png", "estilo de personagens"), ("cacador_folha_original.png", "estilo do caçador")]
    out = ["# Parte D1 · Retratos de diálogo dos NPCs", "", "Um prompt por imagem. Respeite a idade descrita (a Lis é criança).", ""]
    n = 1
    for nid, name, desc, exprs in NPCS:
        for i, expr in enumerate(exprs, 1):
            body = [WORLD, "", f"Please create ONE character portrait for this game: {name}, {desc}.",
                    f"Expression: {expr}.", STYLE, "",
                    "FORMAT: bust portrait (head and shoulders), facing slightly to the right, square image 1024x1024 or larger, "
                    "flat solid pure green #00FF00 background (it will be removed). Realistic proportions, NOT chibi, NOT big-headed.",
                    NEVER]
            out.append(block(f"D1-{n:02d}", f"retrato_{nid}_{i}.png", f"{name} ({expr})", refs, body))
            n += 1
    return "\n".join(out)


def npc_walks():
    out = ["# Parte D2 · NPCs andando pela cidade", "",
           "Um prompt por imagem. Anexe o retrato aprovado do NPC (Parte D1) e a tira de caminhada do caçador para a escala.", ""]
    refs = [("lamina_andar_baixo.png", "escala do caçador"), ("estilo_npcs_proporcao.png", "estilo de personagens")]
    n = 1
    for nid, name, desc, _ in NPCS:
        child = "; she is a child, so about 75% of the hunter height" if nid == "lis" else ""
        extra = (f"Same body proportions, size and chunky stylization as the attached hunter (head about one quarter of the "
                 f"total height){child}. If the NPC portrait is attached, copy that face and outfit. In walking frames the "
                 "legs, arms and clothes must visibly move (step, swing, sway); never repeat the same pose.")
        for anim, frames, adesc in [("parado", 4, "idle: standing and breathing"), ("andar", 6, "walking cycle")]:
            for d, facing in DIRS:
                out.append(block(f"D2-{n:02d}", f"npc_{nid}_{anim}_{d}.png", f"{name}: {anim} ({d})", refs,
                                 sprite_body(f"{name}, {desc}", adesc, frames, facing, extra)))
                n += 1
    return "\n".join(out)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    files = {"A_cenarios.md": maps(), "B_lamina.md": hunter("lamina"), "B_arco.md": hunter("arco"),
             "B_martelo.md": hunter("martelo"), "C_monstros.md": monsters(),
             "R_lamina_restyle.md": hunter_restyle("lamina", ["arco", "martelo"]), "D1_retratos.md": portraits(),
             "D2_npcs_andando.md": npc_walks()}
    for name, text in files.items():
        (OUT / name).write_text(text, encoding="utf-8")
        print(name, text.count("### "), "prompts", len(text), "chars")


if __name__ == "__main__":
    main()
