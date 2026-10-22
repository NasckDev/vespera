# Catálogo de assets — leva de 2026-10-20 (frente B5)

O dono mandou 70 imagens únicas (geradas por ele; os nomes originais estão em `INDEX.tsv` da leva, "Imagem do ChatGPT …").
Este documento registra a **triagem completa** e o que entrou no jogo. Decisões: [`DECISIONS.md`](DECISIONS.md) B5-1 a B5-5.
Licença de tudo que entrou: CC BY-NC-SA 4.0 ([`LICENSE-ASSETS.md`](../../LICENSE-ASSETS.md)).

## O que entrou

| Entrega | Onde | Quanto |
|---|---|---|
| Biblioteca de objetos do mundo | `assets/sprites/objects/<tema>/<id>.png` + `src/Data/objects.json` | **445 objetos** em 9 temas (8,3 MB de PNG) |
| Folhas-fonte dos objetos (para refazer o recorte) | `docs/brand/source/objects/NN.png` | 6 folhas (27, 59, 61, 63, 65, 67) |
| Mapas candidatos, sem colisão | `assets/maps/extra/<id>/map_<id>.png` + `src/Data/maps_extra.json` | **20 mapas**: 6 salas extras das Brechas atuais e 14 salas das Brechas 6–10 |
| Imagens-resumo | `docs/design/catalogo/<tema>.jpg`, `docs/design/catalogo/mapas-extra.jpg` | 9 temas + mapas |
| Galeria no jogo | `scenes/Debug/ObjectGallery.tscn` (`-- --object-gallery[=tema]`) | todos os objetos no tamanho calibrado |

### Como refazer

```bash
python -I tools/assets/cut_sheet.py docs/brand/source/objects/67.png --out <pasta>   # recorte + contact.png + overview.png
python -I tools/assets/build_objects.py --docs     # recorta, calibra, grava sprites, objects.json e imagens-resumo
python -I tools/assets/build_objects.py --check    # confere que objects.json bate com a curadoria
python -I tools/maps/extra_maps.py --src <pasta com NN.png>   # mapas candidatos + mapas-extra.jpg
godot --headless --path . --import                 # .import novos (objetos com mipmaps, como sprites)
```

A curadoria fica em `tools/assets/objects_curation.json`: para cada folha, quais peças viram objeto, nome, tema, `kind` e
ajustes. **Os números das peças dependem dos parâmetros do recorte** (`params` no topo): mudou parâmetro, confira de novo
as folhas de conferência (`contact.png`, `overview.png`) antes de rodar o build.

### Recorte (`tools/assets/cut_sheet.py`)

Máscara por alpha (≥ 110, fora o véu semitransparente das folhas), componentes 8-conectados com dilatação de 1 px
(partes do mesmo objeto se fundem), peças pequenas a até 6 px de um objeto maior anexadas a ele (chamas, faíscas),
descarte de ruído (< 120 px) e de linhas de texto, painéis opacos abertos para recortar o miolo, alpha remapeado e cor da
borda trocada pela dos pixels opacos vizinhos (**sem halo** claro nem escuro), margem transparente de 2 px.
Conferido sobre fundo magenta e branco.

### Calibração (`objects.json`)

| Referência | Valor |
|---|---|
| Jogador (`c_sword_front`, 162 px / k 5,0025) | **32,4 u** |
| Porta (`door_arched`) | 24 × 36,3 u (1,12 × o jogador: jogador ≈ altura de porta) |
| Barril | 0,45 × jogador (14,6 u) |
| Lampião de poste | 1,6 × jogador (51,8 u) |
| Barraca | 2,0 × jogador (64,8 u) |
| Lápide 0,72 · estátua 1,7 · árvore 2,8 · mausoléu 2,4 · estufa 3,2–3,4 · vela 0,28 · caixote 0,42 | × jogador |
| Densidade | mediana 3,1 px/u; recortes acima de 4 px/u são reduzidos (sem texturas gigantes; 4 px/u cobre 1440p) |

Cada objeto traz `worldSize` (u), `density` (px/u: desenhe com escala `1/density`), `anchor` (px da textura: o **pé**, no
centro do que toca o chão), `footprint` (retângulo sólido em u atrás do pé, ou `null`), `layer`
(`entity` · `floor` · `wall` · `overhead` · `fx`), `occluder` (esconde o jogador que passa atrás), `sway` (balança),
`light` (cor, raio, posição da chama/cristal), `interaction` sugerida (vocabulário do `layout.py`: `break`, `chest`,
`lamp`, `flower`, `boat`, `fishing`, `sign`, `bench`, `fountain`, `stall`, `inspect`) e `sound` (`ambient` do layout +
`material`). Tudo isso é **sugestão** para quem monta os mapas; nada é colocado no mundo por esta frente.

### Temas

| Tema | Folha | Objetos | Com luz | Sólidos | Balançam | Imagem |
|---|---|---|---|---|---|---|
| `cemetery` — Cemitério das Velas | 67 | 43 | 31 | 36 | 2 | [cemetery.jpg](catalogo/cemetery.jpg) |
| `market` — Mercado e cidade | 67 | 25 | 2 | 24 | 2 | [market.jpg](catalogo/market.jpg) |
| `docks` — Cais e barcos | 67 | 22 | 2 | 13 | 0 | [docks.jpg](catalogo/docks.jpg) |
| `astral` — Observatório Astral | 59 | 80 | 27 | 61 | 9 | [astral.jpg](catalogo/astral.jpg) |
| `clockwork` — Torre do Relógio e galerias alagadas | 61 | 65 | 2 | 45 | 5 | [clockwork.jpg](catalogo/clockwork.jpg) |
| `cathedral` — Catedral e cripta | 63 | 90 | 17 | 57 | 4 | [cathedral.jpg](catalogo/cathedral.jpg) |
| `greenhouse` — Estufa Silvestre | 65 | 48 | 14 | 38 | 23 | [greenhouse.jpg](catalogo/greenhouse.jpg) |
| `mine` — Minas da Forja | 65 | 38 | 14 | 34 | 1 | [mine.jpg](catalogo/mine.jpg) |
| `common` — Comuns (contorno grosso) | 27 | 34 | 10 | 29 | 1 | [common.jpg](catalogo/common.jpg) |

Descartados na curadoria (ficam só nas folhas-fonte): duplicatas e variações quase iguais (lápides e lanternas repetidas,
fogos-fátuos, vasos), cacos de cristal soltos, recortes cortados ou com fundo de painel (sinos e livros do painel
da folha 63, respingos da folha 67), círculos de runa quase transparentes, pedaços de trilho e a fileira de miniaturas da
folha 65. A folha 27 tem traço de ícone (contorno preto grosso), diferente da arte pintada dos mapas: use os objetos
`common` com cuidado (marcadores, Guilda, UI), não misturados com os pintados.

### Mapas candidatos (`maps_extra.json`)

Ver [mapas-extra.jpg](catalogo/mapas-extra.jpg). Nenhum tem colisão: para jogar, faça o layout com `tools/maps/layout.py`
e mova para o `maps.json`. Atribuição é sugestão pelo visual e pelos nomes da prancha 52.

| Papel | Mapas |
|---|---|
| Sala extra do Mercado de Peixes (`fish2`) | `extra_fish2_b` (41) |
| Sala extra do Cemitério (`crypt2`) | `extra_crypt2_b` (42) |
| Sala extra das Minas (`mine`) | `extra_mine_b` (43) |
| Sala extra da Estufa (`garden`) | `extra_garden_b` (44), `extra_garden_c` (66 direita) |
| Sala extra da Catedral (`cath`) | `extra_cath_b` (45) |
| Brecha 6 — Biblioteca Submersa | `brecha6_library` (46), `brecha6_library_b` (60 esquerda) |
| Brecha 7 — Torre Relógio Mecânica | `brecha7_clock` (47), `brecha7_clock_b` (60 direita), `brecha7_clock_c` (64 esquerda) |
| Brecha 8 — Labirinto Alagado das Catacumbas | `brecha8_catacombs` (48), `_b` (62 esquerda), `_c` (62 direita), `_d` (66 esquerda) |
| Brecha 9 — Templo em Ruínas | `brecha9_temple` (49), `brecha9_temple_b` (64 direita) |
| Brecha 10 — Observatório Astral | `brecha10_astral` (50), `_b` (58 esquerda), `_c` (58 direita) |

As imagens 58, 60, 62, 64 e 66 têm dois mapas lado a lado; o corte é no vão escuro entre eles (não no meio exato), então
as metades têm larguras diferentes (751 a 1023 px) e 887 px de altura. Os mapas 41–50 têm 1672×941, como os hd da v2.

## Triagem das 70 imagens

Legenda: **entrou** = usado nesta frente · **já no repo** = idêntico a um asset existente · **candidato** = útil, para
outra frente decidir · **referência** = prancha com texto/mock, só para consulta (não vai para o jogo).

| # | Tipo | Conteúdo | Destino |
|---|---|---|---|
| 00 | prancha com texto (RGB) | Atlas de equipamentos e itens por rank E–SS, animações de itens | referência (Arsenal, D17) |
| 01 | prancha com texto | Catálogo de itens: armas, armaduras, acessórios, materiais por rank | referência |
| 02 | prancha com texto | Compêndio de itens por rank, efeitos de personagem | referência |
| 03 | prancha com texto | Guia de itens e equipamentos, produção | referência |
| 04 | mapa | Mercado de Peixes | já no repo (`map_fish2.png`) |
| 05 | mapa | Rua da Forja | já no repo (`map_city.png`) |
| 06 | mapa | Cemitério das Velas | já no repo (`map_crypt2.png`) |
| 07 | mapa | Catedral do Arauto | já no repo (`map_cath.png`) |
| 08 | mapa | Estufa Silvestre | já no repo (`map_garden.png`) |
| 09 | mapa | Minas da Forja | já no repo (`map_mine.png`) |
| 10 | ilustração | Key art com logo | já no repo (`docs/brand/key-art.png`) |
| 11 | ilustração com texto (inglês) | Lumeira vista de cima com rótulos ("Harbor District", "The Highroad") | referência (nomes divergem dos do jogo) |
| 12 | RGBA | Caçador: poses, ataques, armas e estandarte | candidato (Interface/Combate; o jogador usa o atlas) |
| 13 | RGBA | Ícones (lanterna, cristal, moeda, poção, chave, armas), selos E–Alvor, barras e painéis | candidato (Interface) |
| 14 | RGBA | Sprite sheet do caçador: andar, golpes, arco, queda | candidato (animação do jogador) |
| 15 | RGBA | Texturas de chão e parede (pedra, grama, madeira, água, tapete, brecha) | candidato (tiles/decalques) |
| 16 | RGBA 2172×724 | 6 quadros da Brecha crescendo (magenta) | candidato (efeito; o portal já usa `portal_open_0..5`, D38) |
| 17 | RGBA | Ícones grandes (versão da 13 sem selos) | candidato (Interface) |
| 18 | RGBA | Monstros em poses (lobo sombrio, lanterna-viva, dragão-peixe, espectro) | candidato (Combate) |
| 19 | RGBA | Chefes (lobo de cristal, dragão, ceifador) | candidato (Combate) |
| 20 | RGBA | Evolução do caçador em 5 estágios | referência (progressão visual) |
| 21 | RGBA | Classes em ação (espada, arco, cajado, martelo, adagas) | candidato (Combate/Interface) |
| 22 | prancha com texto (inglês) | Interiores: estalagem, forja, guilda | referência |
| 23 | prancha com texto (inglês) | Ambientes de dungeon: mercado alagado, cripta, câmara do núcleo | referência |
| 24 | prancha com texto (inglês) | Mapas de cidade: estalagem, distrito da forja, guilda | referência |
| 25 | prancha com texto (inglês) | Mapas: mercado alagado, cripta, câmara do núcleo | referência |
| 26 | RGBA | Conceitos de personagem | já no repo (`docs/brand/character-concepts.png`) |
| 27 | RGBA | Ícones e objetos de contorno grosso (armas, armaduras, baús, poços, lápides, cristais) | **entrou**: 34 objetos (`common`); ícones de item = candidato (Interface) |
| 28 | mock de UI | Personagem e inventário | referência (Interface) |
| 29 | RGBA | Armas lendárias, escudos, lanternas e orbes | candidato (Arsenal) |
| 30 | RGBA | Monstros (espectros, peixe-lanterna, besouros, golens) | candidato (Combate) |
| 31 | prancha com texto (inglês) | As 4 lojas da Rua dos Ofícios | referência |
| 32 | RGBA | Chefes com lanternas e brecha | candidato (Combate) |
| 33 | RGBA | NPCs (taberneira, ferreiro, maga, alquimista, pescador…) em 3 poses | candidato (Cidade) |
| 34 | prancha com texto | Mercado de Peixes (E–D): comuns, elite, chefe, drops | referência (Combate) |
| 35 | prancha com texto | Minas da Forja (C–B): comuns, elite, chefe, drops | referência (Combate) |
| 36 | prancha com texto | Mercado de Peixes: Lodo Salgado, Caranguejo de Rede, Peixe-Lanterna, Abissal | referência (Combate) |
| 37 | prancha com texto | Cemitério das Velas (D–C): Espectro da Névoa, Ossário, Ceifador | referência (Combate) |
| 38 | prancha com texto | Minas da Forja: Besouro de Brasa, Autômato, Colosso | referência (Combate) |
| 39 | prancha com texto | Estufa Silvestre (B–A): Broto, Golem de Musgo, Rainha das Raízes | referência (Combate) |
| 40 | prancha com texto | Catedral do Arauto (A–S): Escudeiro do Halo, Sino Vivo, Arauto | referência (Combate) |
| 41 | mapa | Variação do Mercado de Peixes | **entrou**: `extra_fish2_b` |
| 42 | mapa | Variação do Cemitério | **entrou**: `extra_crypt2_b` |
| 43 | mapa | Variação das Minas | **entrou**: `extra_mine_b` |
| 44 | mapa | Variação da Estufa | **entrou**: `extra_garden_b` |
| 45 | mapa | Variação da Catedral | **entrou**: `extra_cath_b` |
| 46 | mapa | Salas alagadas sobre o vazio | **entrou**: `brecha6_library` |
| 47 | mapa | Relógio gigante e engrenagens | **entrou**: `brecha7_clock` |
| 48 | mapa | Catacumbas alagadas | **entrou**: `brecha8_catacombs` |
| 49 | mapa | Templo claro com estátuas e jardins | **entrou**: `brecha9_temple` |
| 50 | mapa | Plataformas astrais violeta | **entrou**: `brecha10_astral` |
| 51 | prancha com texto | Objetos interativos das Brechas 1–5 com estados (parado, interagindo, aberto, destruído) | referência (interações) |
| 52 | prancha com texto | Idem para as Brechas 6–10 (fonte dos nomes das Brechas novas) | referência (interações) |
| 53 | prancha com texto | NPCs por Brecha (pescador ferido, zelador das velas, espírito guia…) | referência (Cidade/Combate) |
| 54 | prancha com texto | Monstros das Brechas 6–10 | referência (Combate) |
| 55 | prancha com texto | Classes de jogador I: Lâmina e Arco, evoluções e animações | referência |
| 56 | prancha com texto | Classes de jogador II: Cajado e Martelo | referência |
| 57 | prancha com texto | Classes de jogador III: Adagas e evolução universal do caçador | referência |
| 58 | 2 mapas | Astral: plataformas e anel de salas | **entrou**: `brecha10_astral_b`, `_c` |
| 59 | RGBA | Objetos astrais (cristais, ilhas, arcos, astrolábios, estátuas, fontes, móveis) | **entrou**: 80 objetos (`astral`) |
| 60 | 2 mapas | Galerias alagadas · câmara do mecanismo | **entrou**: `brecha6_library_b`, `brecha7_clock_b` |
| 61 | RGBA | Canos, válvulas, grades, cascatas, engrenagens, sinos, vitrais, ruínas | **entrou**: 65 objetos (`clockwork`) |
| 62 | 2 mapas | Catacumbas alagadas | **entrou**: `brecha8_catacombs_b`, `_c` |
| 63 | RGBA | Altares, estátuas, vitrais, bancos, estantes, baús, portas, cercas, esgotos | **entrou**: 90 objetos (`cathedral`) |
| 64 | 2 mapas | Torre do relógio · nave em chamas | **entrou**: `brecha7_clock_c`, `brecha9_temple_b` |
| 65 | RGBA | Estufa (plantas, fontes, estufas) e mina (fornos, trilhos, carrinhos, minério) | **entrou**: 48 (`greenhouse`) + 38 (`mine`) |
| 66 | 2 mapas | Canais com vitórias-régias · estufas em volta de tanques | **entrou**: `brecha8_catacombs_d`, `extra_garden_c` |
| 67 | RGBA | Cemitério (lápides, mausoléus, estátuas), mercado (barracas, caixotes) e cais (píeres, barcos) | **entrou**: 43 + 25 + 22 objetos |
| 68 | prancha com texto | Painel de conceito: cidade, NPCs, menus, mapas | referência |
| 69 | prancha com texto | Guia visual: personagens, mapas, UI | referência |

## Limites conhecidos

- Densidade dos recortes: 1,7 a 4,3 px/u (mediana 3,1). Em 1080p a câmera mostra ~2,7 px/u, então a maioria fica nítida;
  em 4K (5,4 px/u) os objetos ficam mais macios que o jogador (5 px/u). Não dá para criar resolução que a folha não tem.
- `footprint` é um retângulo só: objetos em diagonal (estufa comprida, barcos) e passagens (arcos, portão aberto, que
  saem sem footprint) precisam de colisão desenhada no layout do mapa.
- Âncora e luz são automáticas (faixa de baixo do recorte; pixels claros com a cor da luz). O guindaste da mina tem o pé
  escolhido à mão (`ax`). Confira na galeria antes de usar um objeto assimétrico.
