"""Prompts de arte do Ato II (Pomar Alto / Pomar Podre), reaproveitando o gerador do Ato I.

Uso: python -I tools/art/prompts_ato2.py  →  docs/v2/prompts/ato2_*.md
Conteúdo tirado do GDD §2–§4: Dona Celeste, Carrapicho, Rato-Lanterna e o Guardião Macieira Oca.
"""
import gerar_prompts as g

MAPS_ATO2 = [
    ("A8", "chao_pomar_alto.png", "Pomar Alto (bairro)",
     "The Orchard Hill neighborhood of Lumeira at dusk: a gentle hill of apple trees and vegetable gardens with stone-bordered "
     "dirt paths (walkable) crossing the image, small wooden fences, beehives and garden beds along the edges, Dona Celeste's "
     "herbalist cottage with a small glass greenhouse at the top-center (closed door), one special apple tree next to it that "
     "still bears ripe red fruit. Paths exit centered on the left, right and bottom edges. The middle of the paths is free of objects."),
    ("A9", "chao_pomar_corredor.png", "Pomar Podre: corredor",
     "A dungeon room inside a Breach: the 'Rotten Orchard', a twisted copy of the orchard as it was before the Year Without Dawn. "
     "A straight dirt-and-stone path running left to right through the middle, open exits centered on the LEFT and RIGHT edges. "
     "Above and below the path: dead apple trees with hollow trunks, rotten purple apples on the ground, thorny roots, faint "
     "magenta cracks in the soil, pale fog. Dim light."),
    ("A10", "chao_pomar_cruzamento.png", "Pomar Podre: cruzamento",
     "A dungeon room inside the 'Rotten Orchard' Breach: a plus-shaped crossing of dirt paths with four open exits exactly centered "
     "on the top, bottom, left and right edges; in the four corners dense rotten trees, thorn bushes and fallen apple crates; an old "
     "dry stone well in the center of the crossing; magenta cracks in the soil."),
    ("A11", "chao_pomar_descanso.png", "Pomar Podre: clareira de descanso",
     "A dungeon room inside the 'Rotten Orchard' Breach: a small peaceful clearing with a single open entrance centered on the BOTTOM "
     "edge and one exit centered on the TOP edge, an old stone bench and an empty lantern post in the center (leave a flat spot), "
     "soft amber light breaking the fog, a few healthy flowers fighting the rot. Walkable center free of objects."),
    ("A12", "chao_pomar_arena.png", "Pomar Podre: arena do Guardião",
     "The BOSS ARENA of the 'Rotten Orchard' Breach: a large round clearing ringed by enormous twisted roots that rise from the ground "
     "like a wall, rotten apples and broken baskets around the edge, magenta glowing sap in the root cracks. A wide flat walkable "
     "center (completely empty) and a single entrance centered on the BOTTOM edge."),
]

MONSTERS_ATO2 = [
    ("carrapicho", "Carrapicho (planta comum, rank D)",
     "a creeping plant monster made of uprooted garden weeds: a bulb body with thorny vines as legs, a stem with a closed bud as head, "
     "brown-green with sickly magenta veins",
     [("parado", 4, "idle swaying"), ("mover", 6, "crawling forward on its vines"), ("aviso", 3, "warning: the bud opens and glows amber"),
      ("ataque", 4, "lashes roots forward to grab"), ("morte", 6, "withers and falls apart into dry leaves")]),
    ("rato_lanterna", "Rato-Lanterna (enxame, rank D)",
     "a small sewer rat that ate glowing crystal shards: thin grey fur, a faint magenta glow inside its belly and eyes, long tail",
     [("parado", 4, "idle sniffing"), ("correr", 6, "running fast"), ("aviso", 3, "warning: crouches, belly glows bright"),
      ("ataque", 4, "leaping bite"), ("morte", 6, "flips over, the glow fades out")]),
    ("macieira_oca", "Macieira Oca (Guardião, chefe rank D)",
     "a HUGE hollow apple tree boss: a cracked bark face with dark hollow eyes and mouth, two big branch arms, roots as legs, "
     "rotten purple apples hanging from the branches, magenta sap glowing in the cracks. It is a TREE monster, about three times "
     "taller than a person",
     [("parado", 6, "idle, branches creaking and swaying"), ("lancar", 8, "throws exploding rotten apples with a branch arm"),
      ("raizes", 8, "slams its roots into the ground so roots burst up ahead"), ("andar", 8, "uproots itself and walks forward heavily"),
      ("morte", 10, "cracks, splits open and collapses, the apples rot away")]),
]

NPCS_ATO2 = [
    ("celeste", "Dona Celeste",
     "an herbalist woman of unclear age, long silver-green braid with small leaves in it, calm knowing eyes, layered green and brown "
     "shawls, a basket of herbs, slightly otherworldly", ("neutral, serene", "mysterious half smile")),
]


def _swap(**values):
    old = {k: getattr(g, k) for k in values}
    for k, v in values.items():
        setattr(g, k, v)
    return old


def main():
    out = g.OUT
    out.mkdir(parents=True, exist_ok=True)
    old = _swap(MAPS=MAPS_ATO2, MONSTERS=MONSTERS_ATO2, NPCS=NPCS_ATO2)
    try:
        files = {
            "ato2_A_cenarios.md": g.maps().replace("Parte A · Cenários (6 imagens)", "Ato II · Cenários do Pomar (5 imagens)")
                                           .replace("A Praça (A1) já está aprovada e serve de modelo.",
                                                    "Os mapas do Ato I (Praça, Forjas, esgoto) estão aprovados e são o modelo de estilo."),
            "ato2_C_monstros.md": g.monsters().replace("Monstros do Ato I", "Monstros do Ato II (Pomar Podre)"),
            "ato2_D_celeste.md": g.portraits().replace("Retratos de diálogo dos NPCs", "Dona Celeste: retratos")
                                 + "\n\n" + g.npc_walks().replace("NPCs andando pela cidade", "Dona Celeste andando"),
        }
    finally:
        _swap(**old)
    for name, text in files.items():
        (out / name).write_text(text, encoding="utf-8")
        print(name, text.count("### "), "prompts", len(text), "chars")


if __name__ == "__main__":
    main()
