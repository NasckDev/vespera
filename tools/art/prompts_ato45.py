"""Prompts de arte que faltam para fechar os Atos IV (Cemitério das Lanternas) e V (Torre do Sino / Sino Mudo).

Uso: python -I tools/art/prompts_ato45.py  →  docs/v2/prompts/ato4_B_salas.md, ato4_C_monstros.md, ato4_C_velora.md,
ato5_B_salas.md, ato5_C_monstros.md, ato5_C_sineiro_mudo.md (um arquivo por issue de arte).
Conteúdo tirado de docs/v2/CAMPANHA.md (salas, monstros, Guardiões por fase) e do ROTEIRO-V2 §13. Reaproveita os
geradores do Ato I (gerar_prompts.py) e do Ato III (prompts_ato3.py). Todas as referências vêm de docs/v2/referencias no
branch v2 (fora do LFS, links raw).
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))  # -I tira a pasta do script do sys.path
import gerar_prompts as g  # noqa: E402
import prompts_ato3 as a3  # noqa: E402

REF_PRANCHA = (g.REPO + "estilo_prancha_mapas.png", "prancha de mapas (estilo)")
REF_SALA = (g.REPO + "estilo_sala_esgoto.png", "sala de Brecha aprovada do Ato I (corredor)")
a3.REFS_BY_KIND["sala"] = [REF_SALA, REF_PRANCHA]

BREACH = a3.BREACH

INTRO_SALAS = ("As salas aprovadas do Ato I (esgoto) e do Ato II (Pomar Podre) são o modelo: mesma câmera, mesma escala de "
               "porta e de passagem. Cada sala precisa das saídas exatamente onde o texto pede (a caçada encaixa as salas pelas "
               "saídas). O bairro e a arena deste ato já existem (issues #32 e #33); estas são as salas do meio da Brecha.")

SALAS_ATO4 = [
    ("A38", "chao_cemiterio_alameda.png", "Cemitério das Lanternas (Brecha): Alameda dos Ciprestes (combate)", "sala",
     "A dungeon room inside the 'Cemetery of Lanterns' Breach. " + BREACH + "A straight gravel AVENUE running left to right through "
     "the middle of the image between two rows of tall dark cypress trees, open exits centered on the LEFT and RIGHT edges. Behind "
     "the cypresses, small graves with UNLIT lanterns, some tombstones hanging upside down from the dark sky area at the top edge, "
     "candle wax dripping upwards from them. Old names carved on the stones as scratch marks only (no readable letters). Thin "
     "magenta cracks in the gravel. The middle of the avenue is free of objects."),
    ("A39", "chao_cemiterio_ossuario.png", "Cemitério das Lanternas (Brecha): Ossuário das Velas (arena de ondas)", "sala",
     "A dungeon room inside the 'Cemetery of Lanterns' Breach. " + BREACH + "A plus-shaped stone OSSUARY crypt with four open exits "
     "exactly centered on the top, bottom, left and right edges. The walls are niches full of skull-less bone piles and hundreds of "
     "melted candles; on the floor, several flat round PUDDLES of hot melted wax glowing amber at the edges (they burn: keep them "
     "flat and readable, scattered near the corners, never blocking the exits). Wax drips upwards from the ceiling niches. "
     "Cracked flagstones with magenta light. The center of the crossing is free of objects."),
    ("A40", "chao_cemiterio_capela.png", "Cemitério das Lanternas (Brecha): Capela Virada (santuário/descanso)", "sala",
     "A dungeon room inside the 'Cemetery of Lanterns' Breach: a small peaceful SHRINE inside the cemetery chapel turned upside "
     "down: the wooden pews hang from the dark ceiling at the top edge, the round rose window lies on the floor like a rug of "
     "colored glass, fallen hymn books, a few wilted flower crowns. An empty lantern post in the center (leave a flat spot, a "
     "lantern will be placed there later). Soft amber light, calm. Single open entrance centered on the BOTTOM edge, one exit "
     "centered on the TOP edge. Walkable center free of objects."),
    ("A41", "chao_cemiterio_fosso.png", "Cemitério das Lanternas (Brecha): Fosso dos Nomes (tesouro)", "sala",
     "A dungeon room inside the 'Cemetery of Lanterns' Breach: a sunken rectangular PIT whose stone walls are covered from floor "
     "to top with hundreds of small carved name plates (scratch marks only, no readable letters) and tiny unlit lanterns, like a "
     "wall of remembrance. Stone steps lead down from a single open entrance centered on the BOTTOM edge to a raised stone dais "
     "at the top-center (leave the dais EMPTY, a chest will be placed there later). Glowing magenta crystals and old hunter "
     "lanterns lying on the floor near the walls, never in the middle."),
]

SALAS_ATO5 = [
    ("A42", "chao_torre_escada.png", "Sino Mudo (Brecha): Escada em Espiral (combate)", "sala",
     "A dungeon room inside the 'Silent Bell Tower' Breach. " + BREACH + "It is the Order's bell tower on the night of the Lantern "
     "Festival twenty years ago. A wide stone LANDING of an endless spiral staircase seen from above: the walkable stone floor "
     "runs left to right through the middle of the image, with open exits centered on the LEFT and RIGHT edges; above and below "
     "it, the stairs keep curling down into a dark bottomless shaft. Long bell ropes hang from the darkness above, old Order "
     "banners torn, unlit wall lanterns, festival bunting. Archive papers floating weightless in the air. The middle of the "
     "landing is free of objects."),
    ("A43", "chao_torre_arquivo.png", "Sino Mudo (Brecha): Arquivo (encruzilhada)", "sala",
     "A dungeon room inside the 'Silent Bell Tower' Breach. " + BREACH + "The Order's ARCHIVE: a plus-shaped hall with four open "
     "exits exactly centered on the top, bottom, left and right edges. In the four corners, tall wooden shelves full of ledgers "
     "and rolled maps, some shelves hanging upside down from the dark ceiling, thousands of loose papers floating still in the "
     "air and lying on the floor near the walls, an overturned reading desk with an unlit candle. Thin magenta cracks between "
     "the floor tiles. The center of the crossing is free of objects."),
    ("A44", "chao_torre_cordas.png", "Sino Mudo (Brecha): Sala das Cordas (santuário/descanso)", "sala",
     "A dungeon room inside the 'Silent Bell Tower' Breach: a small calm RINGING CHAMBER where dozens of bell ropes hang from the "
     "dark ceiling and end just above the floor, tied with faded festival ribbons; a worn wooden bench, a coil of rope, a "
     "child's paper lantern from the festival. An empty lantern post in the center (leave a flat spot, a lantern will be placed "
     "there later). Soft amber light. Single open entrance centered on the BOTTOM edge, one exit centered on the TOP edge. "
     "Walkable center free of objects."),
    ("A45", "chao_torre_silencio.png", "Sino Mudo (Brecha): Sala do Silêncio (combate sem som)", "sala",
     "A dungeon room inside the 'Silent Bell Tower' Breach: the SILENT ROOM, a wide rectangular stone hall where every sound was "
     "taken away: the walls are lined with hundreds of small bronze bells, all of them with their clappers removed, hanging "
     "still; heavy felt curtains, thick dust, nothing moves. The floor is clean, flat and very readable (fights here are read "
     "only by sight). Pale cold light with faint magenta in the cracks. Single open entrance centered on the BOTTOM edge, one "
     "exit centered on the TOP edge. The middle of the hall is completely free of objects."),
]

MONSTERS_ATO4 = [
    ("velinha", "Velinha (comum, rank B)",
     "a small living CANDLE echo: a short thick white wax candle with stubby wax legs and tiny arms, wax drips running down its "
     "body, a bright flame on top as its head, a long black wick, two small dark holes for eyes melted into the wax. SCALE: "
     "small, about 0.4x the hunter height",
     [("parado", 4, "idle, the flame flickering, wax dripping"), ("mover", 6, "waddling quickly away, the flame trailing behind"),
      ("aviso", 3, "warning: stops, the flame grows tall and bright amber"),
      ("ataque", 4, "releases a ring of warm amber light around itself that heals nearby monsters"),
      ("morte", 6, "melts down into a puddle of wax and the flame goes out with a thin smoke")]),
    ("carpideira", "Carpideira (comum, rank B)",
     "a weeping MOURNER echo: a tall thin figure in a long black mourning dress and a long black lace veil that reaches the "
     "ground, NO face under the veil (only empty darkness), pale thin hands holding a wilted bouquet, the hem of the dress "
     "floating just above the ground, faint magenta tears of light. SCALE: a normal adult (1.0x the hunter height)",
     [("parado", 4, "idle, gliding gently, shoulders shaking as if crying"), ("mover", 6, "gliding forward, the veil and dress trailing"),
      ("aviso", 3, "warning: the veil lifts, the empty face glows bright amber inside"),
      ("ataque", 4, "screams forward: a cone-shaped wave of sound pushes out from the empty face"),
      ("morte", 6, "the dress collapses empty to the ground, the veil floats down last")]),
    ("cao_de_vigilia", "Cão-de-Vigília (comum, rank B)",
     "a gaunt graveyard WATCHDOG echo: a thin tall hound with grey stone-like fur, ribs showing, a heavy iron collar with a small "
     "metal name tag (no readable letters) and a broken chain, eyes glowing lantern amber. SCALE: about 0.6x the hunter height",
     [("parado", 4, "idle, sniffing, the broken chain swinging"), ("mover", 6, "running fast"),
      ("aviso", 3, "warning: raises its head and howls, the eyes and collar glow bright amber"),
      ("ataque", 4, "charges forward in a straight line with its jaws open"),
      ("tonto", 4, "dizzy after crashing into a tombstone: staggering, shaking its head, little stars"),
      ("morte", 6, "lies down, curls up and crumbles into grey dust, the collar stays")]),
]

GUARDIAN_VELORA = [
    ("velora", "Velória (Guardiã, chefe rank B)",
     "a HUGE colossus of melted CANDLE WAX shaped like a hunched mourning woman, made of hundreds of fused candles of every size, "
     "HUNDREDS of small flames burning all over its shoulders, back and head like a crown, wax always dripping, long wax arms. "
     "THREE large black iron CANDELABRAS are fused into its body (one on each shoulder and one on its back), each holding tall "
     "candles; they are breakable parts and must be clearly visible. No real face: a smooth wax mask with two dark holes. "
     "SCALE: about 3x the hunter height",
     [("parado", 6, "idle, swaying, all the small flames flickering, wax dripping"),
      ("andar", 8, "dragging itself forward heavily, leaving wax behind"),
      ("lancar", 8, "pulls candles from its body and throws them forward in an arc"),
      ("derreter", 8, "phase 2: melts down completely into a wide flat puddle of hot wax"),
      ("ressurgir", 8, "rises again out of the wax puddle, reforming its body"),
      ("apagar", 8, "phase 3: all its flames go out at once, only five tall candles stay lit, the body becomes dark"),
      ("morte", 10, "the last flame goes out, the wax body cracks and slowly sinks into a cold puddle, the candelabras fall")]),
]

MONSTERS_ATO5 = [
    ("sineiro", "Sineiro (comum, rank A)",
     "a small stone GARGOYLE echo of a bell-ringer: a hunched grey stone body, two stone bat WINGS (breakable parts), clawed "
     "feet, and instead of a head a small bronze BELL hanging inside its open chest like a heart, a faded festival ribbon tied "
     "on one arm. SCALE: about 0.7x the hunter height",
     [("parado", 4, "idle, wings folded, the bell swaying slowly"), ("mover", 6, "hopping forward with short wing flaps"),
      ("aviso", 3, "warning: spreads its wings, the bell in its chest glows bright amber"),
      ("ataque", 4, "rings the bell: a round shock ring of sound spreads out around it"),
      ("morte", 6, "the bell cracks, the stone body falls apart into pieces")]),
    ("casca", "Casca (comum, rank A)",
     "a heavy armored echo hiding under a big SHELL made of THREE overlapping plates of old bronze bell metal (like a huge bell "
     "cut into three curved plates, green patina, rivets); from the front only the plates show, short thick legs underneath, a "
     "dark body inside. The three plates are breakable parts. SCALE: low and wide, about 0.9x the hunter height",
     [("parado", 4, "idle, closed shell, breathing slowly"), ("mover", 6, "shuffling forward behind its shell"),
      ("aviso", 3, "warning: the shell opens like a flower, the inside glows bright amber"),
      ("ataque", 4, "charges forward shell first, plates slamming"),
      ("morte", 6, "the three plates fall off one by one and the body inside fades")]),
    ("traca_de_arquivo", "Traça-de-Arquivo (comum, rank A)",
     "a SWARM of moths made of torn yellowed ARCHIVE PAPER: dozens of small paper moths with ink-stained wings flying together "
     "as one loose cloud, a few ink drops falling, a faint magenta glow in the middle of the swarm. No single body. SCALE: the "
     "cloud is about 0.6x the hunter height, floating at chest height",
     [("parado", 4, "hovering in a loose cloud, wings fluttering"), ("mover", 6, "the cloud drifts forward quickly"),
      ("aviso", 3, "warning: the moths gather into a tight round ball that glows bright amber"),
      ("ataque", 4, "the ball lunges forward and bursts into a spray of paper moths"),
      ("morte", 6, "the moths fall as torn paper scraps and drift to the ground")]),
]

GUARDIAN_SINEIRO = [
    ("sineiro_mudo", "Sineiro Mudo (Guardião, chefe rank A)",
     "a HUGE stone GARGOYLE as big as a church bell, with NO MOUTH (a smooth stone face with two deep eye holes), two big stone "
     "WINGS (breakable parts), long clawed arms, the frayed end of a thick bell rope tied around its wrist, cracks in the stone "
     "glowing faint magenta, old festival ribbons caught on its horns. It never speaks. SCALE: about 3x the hunter height",
     [("parado", 6, "idle, crouched, wings half open, breathing without sound"),
      ("andar", 8, "walking forward on all fours, wings folded"),
      ("badalar", 8, "slams both fists on the ground like a bell striking: round shock rings spread out"),
      ("voar", 8, "phase 2: beats its wings and lifts off the ground"),
      ("mergulhar", 8, "dives down from the air onto a spot, wings tucked"),
      ("cordas", 8, "pulls bell ropes down from the darkness above with its claws"),
      ("morte", 10, "kneels, the stone cracks all over, it crumbles silently into rubble, the rope falls last")]),
]

INTRO_MONSTROS = ("Um prompt por imagem. Gere primeiro o `parado` de cada monstro e anexe-o nos pedidos seguintes do mesmo "
                  "monstro. Todo golpe forte tem uma tira de `aviso` com brilho âmbar (o jogador lê o golpe pela cor).")


def monsters_md(title, entries, prefix):
    old = a3._swap(MONSTERS=entries)
    try:
        text = g.monsters().replace("# Parte C · Monstros do Ato I", f"# {title}")
    finally:
        a3._swap(**old)
    text = text.replace("Um prompt por imagem. Gere primeiro o `parado` de cada monstro e anexe-o nos pedidos seguintes do mesmo monstro.",
                        INTRO_MONSTROS)
    return text.replace("### C-", f"### {prefix}-")


WHAT = {
    "ato4_B_salas.md": "os 4 mapas das salas internas da Brecha do **Ato IV (Cemitério das Lanternas)**: o cemitério como era antes "
    "do Ano Sem Sol, do lado de baixo, com túmulos pendurados e velas que pingam para cima. Entram no baralho de salas "
    "(CAMPANHA.md, Ato IV): **Alameda dos Ciprestes** (combate, corredor) · **Ossuário das Velas** (arena de ondas, poças de "
    "cera que queimam) · **Capela Virada** (santuário) · **Fosso dos Nomes** (tesouro). O bairro (A19) e a arena da Velória "
    "(A20) já estão na #32. Saída: `chao_<id>.png`, 16:9 (o jogo usa 1672x941).",
    "ato4_C_monstros.md": "os 3 monstros comuns da Brecha do **Ato IV** (CAMPANHA.md): **Velinha** (cura outros monstros e foge; "
    "parte: pavio), **Carpideira** (grito em cone que empurra; parte: véu) e **Cão-de-Vigília** (uiva e investe em linha; bate "
    "em túmulo e fica tonto; parte: coleira). Tiras parado/mover/aviso/ataque/morte (o Cão tem também `tonto`), vistas de lado "
    "olhando para a DIREITA. O jogo espelha para a esquerda.",
    "ato4_C_velora.md": "a Guardiã do **Ato IV**, **Velória** (colosso de cera, centenas de chamas), com uma tira por fase "
    "(CAMPANHA.md): fase 1 *Vigília* (`lancar`: atira velas que viram Velinhas; partes: 3 candelabros), fase 2 *Cera* "
    "(`derreter` + `ressurgir`: vira poça que anda e ressurge), fase 3 *Uma vela* (`apagar`: restam 5 velas; fala única "
    "\"Qual delas é você?\"). Escala ~3x o caçador.",
    "ato5_B_salas.md": "os 4 mapas das salas internas da Brecha do **Ato V (Sino Mudo)**: a Torre na noite do ritual, do lado de "
    "baixo, com escada sem fim, cordas que sobem ao escuro e papéis do arquivo flutuando. Entram no baralho de salas "
    "(CAMPANHA.md, Ato V): **Escada em Espiral** (combate, corredor) · **Arquivo** (encruzilhada) · **Sala das Cordas** "
    "(santuário) · **Sala do Silêncio** (a mecânica nova: sem som, só avisos visuais). O bairro (A21) e a arena do Sineiro "
    "Mudo (A22) já estão na #33. Saída: `chao_<id>.png`, 16:9 (o jogo usa 1672x941).",
    "ato5_C_monstros.md": "os 3 monstros comuns da Brecha do **Ato V** (CAMPANHA.md): **Sineiro** (badalada em área que "
    "atordoa; partes: sino e 2 asas), **Casca** (imune de frente, abre o casco antes de investir; 3 placas) e "
    "**Traça-de-Arquivo** (enxame que encolhe a luz da lanterna; junta numa bola âmbar antes de investir). Tiras "
    "parado/mover/aviso/ataque/morte, vistas de lado olhando para a DIREITA.",
    "ato5_C_sineiro_mudo.md": "o Guardião do **Ato V**, **Sineiro Mudo** (gárgula do tamanho do sino, sem boca), com uma tira "
    "por fase (CAMPANHA.md): fase 1 *FESTA* (`badalar`: anéis de choque; partes: 2 asas), fase 2 *CORDA* (`voar`, "
    "`mergulhar` com sombra no chão, `cordas` que descem do teto), fase 3 *CULPA* (silêncio total: só avisos visuais). A "
    "palavra em pedra de cada fase é texto do jogo, NÃO vai na arte. Escala ~3x o caçador.",
}


def main():
    out = g.OUT
    out.mkdir(parents=True, exist_ok=True)
    files = {
        "ato4_B_salas.md": a3.maps("Ato IV · Salas da Brecha do Cemitério das Lanternas (4 imagens)", INTRO_SALAS, SALAS_ATO4),
        "ato4_C_monstros.md": monsters_md("Ato IV · Monstros do Cemitério das Lanternas: Velinha, Carpideira e Cão-de-Vigília", MONSTERS_ATO4, "C4"),
        "ato4_C_velora.md": monsters_md("Ato IV · Guardiã Velória, uma tira por fase", GUARDIAN_VELORA, "G4"),
        "ato5_B_salas.md": a3.maps("Ato V · Salas da Brecha do Sino Mudo (4 imagens)", INTRO_SALAS, SALAS_ATO5),
        "ato5_C_monstros.md": monsters_md("Ato V · Monstros do Sino Mudo: Sineiro, Casca e Traça-de-Arquivo", MONSTERS_ATO5, "C5"),
        "ato5_C_sineiro_mudo.md": monsters_md("Ato V · Guardião Sineiro Mudo, uma tira por fase", GUARDIAN_SINEIRO, "G5"),
    }
    for name, what in WHAT.items():
        title, rest = files[name].split("\n", 1)
        files[name] = f"{title}\n\n**O que é:** {what}\n{rest}"
    for name, text in files.items():
        (out / name).write_text(text, encoding="utf-8", newline="\n")
        print(name, text.count("### "), "prompts", len(text), "chars")


if __name__ == "__main__":
    main()
