# Assets do Véspera

Só arte, áudio e fontes. PNG e TTF vão para o Git LFS (`.gitattributes`). Licença: [`LICENSE-ASSETS.md`](../LICENSE-ASSETS.md).
Origem: protótipo **v2** (ver [`docs/design/prototype-v2/01-mundo-render-fluxo.md`](../docs/design/prototype-v2/01-mundo-render-fluxo.md) §9.4).
Convenção de mundo: **1 px da imagem do mapa = 1 u** (DECISIONS D1).

## Importação (Godot)

| Pasta | Compressão | Mipmaps | Filtro (no nó) |
|---|---|---|---|
| `maps/*/map_*.png`, `maps/shops/*.png` | sem perda | não | Linear (`World/Map`) |
| `maps/*/mask_*.png` | sem perda | não | — (nunca desenhadas; lidas por `MaskCollision`) |
| `sprites/atlas`, `sprites/enemies`, `sprites/items`, `sprites/ui` | sem perda | **sim** | Linear com mipmaps (`World/Entities`, DECISIONS D3) |
| `fonts/PixelifySans.ttf`, `fonts/VT323-Regular.ttf` | — | — | sem antialias (pixel) |
| `fonts/Cinzel.ttf` | — | — | com antialias (serifada, títulos) |

No Godot 4 o filtro de textura é do `CanvasItem` (propriedade `texture_filter`), não do import. O padrão do projeto
continua Nearest para a UI pixel; o mundo liga Linear nos próprios nós.

## Arte final da v2 (usar como está)

| Arquivo | Conteúdo |
|---|---|
| `maps/city/` | Rua da Forja (`map_city.png` 1672×941; colisão em `walk_forge.png` gerada de `layout_forge.json`; `mask_city.png` 209×117 da v2 só como referência) |
| `maps/fish2/`, `maps/crypt2/`, `maps/mine/`, `maps/garden/`, `maps/cath/` | 5 Brechas hd (mapa 1672×941 + máscara 209×117) |
| `maps/guild/`, `maps/inn/`, `maps/core/` | Guilda, Estalagem, Câmara do Núcleo (colisão por formas em `src/Data/maps.json`) |
| `maps/shops/` | 4 lojas da Rua dos Ofícios (Armaria, Feitiços, Alquimia, Relíquias) |
| `maps/lights.json` | posições das luzes dos mapas hd (raio/tipo do arquivo são ignorados pela v2: sempre 46, âmbar) |
| `sprites/atlas/atlas.png` + `atlas.json` | 217 recortes `[x, y, w, h, ax, ay, k]`: 5 classes × 4 poses (`c_*`), 8 NPCs (`npc_*`), monstros do tema Mercado e chefes, portais `por0–5`, props `pr_*`, sombra, lanterna, ícones, selos `r_*`, retratos `look0–4`, UI |
| `sprites/enemies/*.png` + `meta.json` | 20 monstros dos temas Cemitério, Minas, Estufa e Catedral (`_f`, `_a`, `_d`, alguns `_s`), `meta.json` = `[w, h, k]` |
| `sprites/items/d_*.png` | 36 ícones de materiais |
| `sprites/ui/` | `keyart.png` (título), `lumeira_map.png` (história), `logo.png` |

## Leva do dono de 2026-10-20 (frente B5)

| Arquivo | Conteúdo |
|---|---|
| `sprites/objects/<tema>/*.png` | 445 objetos do mundo em 9 temas, recortados das folhas de `docs/brand/source/objects/`; tamanho, pé, footprint e luz em `src/Data/objects.json` (gerado por `tools/assets/build_objects.py`). Importados com mipmaps, como os outros sprites |
| `maps/extra/<id>/map_<id>.png` | 20 mapas candidatos **sem colisão** (salas extras das Brechas e Brechas 6–10), registrados em `src/Data/maps_extra.json` |

Triagem completa das 70 imagens: [`docs/design/catalogo-assets.md`](../docs/design/catalogo-assets.md).

## Arte aprovada do designer (pacotes v1–v4, issues #2–#8; frente C2)

Copiada e calibrada por `tools/sprites/build_art.py` (`copy` + `frames`); cada pasta tem um `frames.tres` com pé e densidade.

| Pasta | Conteúdo |
|---|---|
| `sprites/player/<classe>/` | andar 6 quadros × frente/costas/lado das 5 classes (420×420, pé 210,412); Lâmina com esquiva lado (v4) e frente/costas (v3, provisória) |
| `sprites/enemies/anim/<sprite>/` | poses dos chefes `cceif`, `mcol`, `grai`, `karr` (v4, 820×490, pé 410,462), `anglerB` (v3 + ataque v4) e Alfa `wolfE` com cristas (v2) |
| `sprites/props/` | quebráveis barril/caixote com estilhaços, baús dos 5 tiers, lampião/braseiro com chama, flores, alavanca, porta, quadro de avisos, balsa/barco a remo, vara de pesca |
| `sprites/npcs/portraits/` | bustos neutros de 8 NPCs (128×128) |
| `sprites/ui/hud/` | 29 ícones do HUD (v1) + sangramento/atordoamento (v4), com a moldura do slot |

## Placeholders

Nenhum no momento. O `placeholder_player.png` da fundação foi removido: o jogador usa os recortes `c_{classe}_*` do
atlas. `sprites/player/` tem o kit da Lâmina (`hunter_*`) e o andar desenhado das 5 classes (frente C2).

## O que falta desenhar ou decidir

- **Bustos neutros de Gaspar, Abel e Iara** (128×128, no estilo dos 8 de `sprites/npcs/portraits/`): o designer entregou só 8 e o protótipo v2 também só tinha retrato para esses 8. Até chegarem, os três falam em caixa de diálogo só de texto (sem moldura de retrato). Ao entregar: copiar para `sprites/npcs/portraits/<id>.png` e acrescentar a chave em `portraits` de `src/Data/npcs.json`; o cenário `arte (qa): testa retratos de todos os NPCs que falam` passa a exigir os 11 quando a lista de exceções ficar vazia.
- **Costas (`_b`) dos 20 monstros de `sprites/enemies/`**: só existem frente, ataque, morte e (alguns) especial. A v2 cai para `_f`.
- **Âncora desses 20 monstros e dos ícones `d_*`**: o `meta.json` não traz pivô. Usamos o centro da base do recorte não transparente (DECISIONS D10); conferir na arte.
- **Quadros de caminhada**: o caçador tem andar desenhado (C2); NPCs e monstros comuns ainda têm uma pose por direção com *bob* procedural (spec §4.3). Faltam esquivas das outras 4 classes, golpes pesados desenhados e costas/locomoção dos chefes.
- **Recepcionista da Guilda**: está pintada em `map_guild.png`; a v2 apaga com um recorte em tempo de execução para desenhar a Odete animada (spec §2.7). Uma versão da arte sem ela evitaria o patch.
- **Áudio**: nada. A v2 sintetiza os efeitos (`swing, hit, brk, dash, pick, coin, hurt, ui, open, boom, bow, magic, rank, die, portal, growl, kill, eco`); faltam samples e música.
- **Ícone do app** (`icon.png` 128/256 com a lanterna) e `icon.ico` do export Windows.

## Não copiados do protótipo (de propósito)

`*_orig.png` (versões sem nitidez), `map_fish.png`, `map_crypt.png`, `map_forge.png` (v1/legado), `src/old_atlas.png`,
`src/old_meta.json`, `src/i_coin.png` (duplicado do atlas), pastas `scratch/` e `uploads/`.
