# Roteiro de prompts para o Gemini (arte da v2)

## Como usar

O Gemini faz uma coisa por vez e precisa de bastante contexto. Então:

1. Abra uma conversa nova e cole o **Bloco 0 (bíblia do mundo)** uma vez.
2. Anexe as 2 referências de estilo: `Gemini_Generated_Image_kqodeykqodeykqod.jpg` (Praça) e `docs/brand/key-art.png`.
   Nos pedidos de personagem, anexe também a tira do caçador (ver Parte B).
3. Mande **um pedido por mensagem**, na ordem. Se sair bom, siga. Se não, peça "same image, fix: ..." antes de passar para o próximo.
4. Salve com o nome indicado e coloque em `assets/v2/gemini/` (ou anexe na issue).

**Fundo verde:** o Gemini não gera transparência. Em todo sprite (personagem, monstro, objeto), peça fundo **verde chapado
`#00FF00`**. Eu removo o verde por script. Não use magenta, porque é a cor da Brecha.

**O que já temos, não peça de novo:**
- caminhada de 6 quadros das 3 classes (GPT #11)
- 58 objetos (GPT #10)
- kit de interface (GPT #14)
- vídeo de abertura

---

## Bloco 0: bíblia do mundo (colar uma vez)

```
You are the art director of VÉSPERA, a top-down 3/4-view pixel-art action RPG for PC.
WORLD: Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year ("the Year Without Dawn").
Since then, every dusk ("véspera") magenta rifts called Breaches open into the Underside: an inverted Lumeira made of everything
the city lost or threw away. Monsters there are "echoes" of human things (a slime full of broken plates, a ghost shaped like an
empty coat). Hunters of the Order of the Lantern enter the Breaches with lanterns. Tone: melancholic but hopeful, cozy town vs. eerie rifts.
STYLE (must match the attached references exactly): detailed painted pixel art, crisp pixel clusters, no blur, no anti-aliased
vector look, warm dusk palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B,
cream #F4E7C6 highlights, moss #5C8C4A, earth #8A5A3C, stone #6B6F8A, water #3C7A8A. Light comes from the TOP-LEFT in every image.
CAMERA: top-down 3/4 view (ground seen from above, walls and objects seen from the front), same as the attached plaza reference.
RULES FOR EVERY IMAGE: no text, no letters, no UI, no watermark, no frame/border. Never crop objects at the image edge.
Acknowledge with "ok" and wait for my requests, one at a time.
```

---

## Parte A: cenários (chão limpo onde o jogador anda)

Formato: **16:9, o maior possível**. O chão não pode ter nada em pé no meio (sem personagens, árvores, postes ou casas
soltas), porque esses itens viram sprites separados. As bordas do mapa (muros, prédios, cercas, água) podem ficar pintadas.

**A1. `chao_praca.png`: Praça da Lanterna**
```
Request A1 - game map background. The Lantern Square, center of Lumeira, at dusk. Large open cobblestone plaza (walkable),
a round stone platform in the exact center where a giant bronze lantern will be placed later (leave the platform EMPTY),
flower beds at the corners, exits (streets) on the 4 sides, town house facades and low walls closing the edges of the map.
Leave the middle of the plaza completely free of objects, characters, trees and lamp posts. No rift/Breach. Same style as reference.
```

**A2. `chao_forjas.png`: Bairro das Forjas**
```
Request A2 - game map background. The Forge District street of Lumeira: a narrow cobblestone street with sidewalks running
left-to-right, soot-darkened workshop facades along the top edge (one big forge facade with a wide wooden door in the center),
smoke stains, glowing ember cracks between stones. Walkable street fully free of objects and characters. Dusk light, same style.
```

**A3. `chao_forja_interior.png`: Forja da Odete (interior)**
```
Request A3 - game map background, interior. Inside Odete's smithy: stone and wooden-plank floor seen from above, back wall with
tool racks painted on it, warm orange glow from the left where a furnace will be placed (leave that spot as empty floor),
exit door at the bottom center. Floor fully free of furniture and characters. Same style.
```

**A4 a A7. Esgotos do Mercado (Brecha de rank E):** 4 imagens, uma por mensagem, trocando o texto entre colchetes.
```
Request A4 - game map background, dungeon room. "Market Sewers" inside a Breach: an INVERTED sewer made of things the city
threw away. Stone sewer floor (walkable) with shallow sludge channels along the edges, broken plates, forks and toys fused into
the walls, thin magenta rift cracks glowing in the stone, dim light. Room type: [CORRIDOR running left-to-right with exits on
both ends]. Floor free of standalone objects and creatures. Same style.
```
- **A5:** `[CROSSROAD with exits on 4 sides]`, arquivo `chao_esgoto_cruzamento.png`
- **A6:** `[TREASURE ROOM, one entrance at the bottom, a raised dais at the top (leave it empty)]`, arquivo `chao_esgoto_tesouro.png`
- **A7:** `[BOSS ARENA, large circular chamber, sludge pool ring around the edge, one entrance at the bottom]`, arquivo `chao_esgoto_arena.png`

O A4 sai como `chao_esgoto_corredor.png`.

---

## Parte B: o caçador (Lâmina, estágio 1)

Anexe a tira `lamina_e1_andar_baixo_6q_REFERENCIA.png` (do pacote do GPT) e a folha
`Imagem do ChatGPT 20 de out. de 2026, 15_29_00-1.png`.

Modelo de pedido (troque os colchetes):
```
Request B[n] - sprite strip of THIS EXACT character (attached: young hunter, short red cape, cloth tunic, old lantern on the
belt, sword). [N] frames in ONE horizontal row, all frames the same size and evenly spaced, flat pure green #00FF00 background,
no ground shadow. Animation: [ANIMATION]. Facing: [DIRECTION]. Keep the same face, clothes, proportions, size and lighting
(top-left) in every frame. Feet at the same height in every frame. No text, no grid lines.
```

| # | Arquivo | [N] | [ANIMATION] | [DIRECTION] |
|---|---|---|---|---|
| B1–B3 | `lamina_e1_parado_<dir>.png` | 4 | idle breathing, cape moving slightly | down (towards camera) / up (back to camera) / right side |
| B4–B6 | `lamina_e1_ataque1_<dir>.png` | 5 | sword slash: wind-up, fast horizontal swing with amber light trail, recovery | as 3 direções |
| B7–B9 | `lamina_e1_ataque2_<dir>.png` | 5 | backhand diagonal slash with trail | as 3 direções |
| B10–B12 | `lamina_e1_ataque3_<dir>.png` | 5 | overhead finishing strike with stronger trail | as 3 direções |
| B13–B15 | `lamina_e1_esquiva_<dir>.png` | 5 | quick dodge roll | as 3 direções |
| B16–B18 | `lamina_e1_dano_<dir>.png` | 2 | flinch back when hit | as 3 direções |
| B19 | `lamina_e1_morte_lado.png` | 7 | falls to the ground, lantern rolls away and goes out | right side |
| B20 | `lamina_e1_pesado_lado.png` | 8 | 2 frames charging (sword glowing amber) + 6 frames spinning heavy slash | right side |

Depois do Lâmina, use a mesma tabela para o **Arco** (troque os ataques por `tiro`: draw bow, release arrow, recoil) e para o
**Martelo** (ataques lentos e pesados, impacto no chão).

---

## Parte C: monstros do Ato I

```
Request C[n] - sprite strip, [N] frames in ONE row, evenly spaced, flat green #00FF00 background, no shadow, side view facing
right. Monster: [DESCRIPTION]. Animation: [ANIMATION]. Same design, size and lighting in every frame. No text.
```

**Lodo**, com a descrição `small slime made of kitchen scraps with broken plates and spoons stuck inside, one glowing core`:
- C1: `idle wobble` (4)
- C2: `crouch then jump` (6)
- C3: `warning pose glowing amber` (3)
- C4: `hit` (2)
- C5: `death splat` (6)

**Espectro**, com a descrição `floating ghost shaped like an empty old coat with a torn veil, faint violet glow`:
- C6: `float idle` (4)
- C7: `chase forward` (6)
- C8: `warning, veil flares amber` (3)
- C9: `lunge attack` (4)
- C10: `dissolve death` (6)

**Lodo-Mãe (Guardião)**, com a descrição `huge mother slime, a mass of sludge holding plates, cutlery and toys, three glowing cores`:
- C11: `idle` (6)
- C12: `big jump` (8)
- C13: `swallow attack` (8)
- C14: `split into three` (8)
- C15: `death` (10)

---

## Parte D: NPCs (retratos de diálogo)

```
Request D[n] - character portrait, bust, facing slightly right, flat green #00FF00 background, same painted pixel style.
[DESCRIPTION]. Expression: [EXPRESSION].
```

- **D1–D2, Odete:** `blacksmith woman in her 50s, strong arms, leather apron, never smiles`. Expressões: neutral / angry.
- **D3–D4, Benício:** `old formal master of the Order of the Lantern, blind in one eye, dark robe with lantern emblem`. Expressões: neutral / stern.
- **D5–D6, Ravi:** `18-year-old rival hunter, patched clothes, E seal on wrist, cocky`. Expressões: neutral / laughing.
- **D7–D8, Lis:** `11-year-old barefoot girl scout with a satchel of hand-drawn maps`. Expressões: neutral / suspicious.
- **D9–D10, Tia Nilza:** `warm tavern keeper, apron, wooden spoon, motherly`. Expressões: neutral / smiling.

---

## Ordem sugerida

A1 → B1–B3 → B4–B12 → C1–C5 → A4–A7 → o resto.

A Praça e o caçador destravam a próxima camada do jogo, que é andar pela cidade sem atravessar parede.
