# Decisões do projeto

Registro de decisões que mudam ou detalham o [`CLAUDE.md`](../../CLAUDE.md). Quando houver conflito, **este arquivo vence**.
Formato: data · decisão · motivo. Proponha mudanças por issue com a label `decisão`.

## 2026-10-20 — Adoção do protótipo v2 como referência de jogo

O protótipo v2 (HTML/JS, Claude Design) é a referência de *game feel*, conteúdo e arte. Especificação completa em
[`prototype-v2/01-mundo-render-fluxo.md`](prototype-v2/01-mundo-render-fluxo.md) e
[`prototype-v2/02-dados-combate-ui.md`](prototype-v2/02-dados-combate-ui.md). O GDD continua valendo para lore e tom.

| # | Decisão | Motivo |
|---|---|---|
| D1 | **Mapas são pinturas + máscara de colisão** (não tiles 32×32 nem LDtk). 1 u do protótipo = 1 px de mundo. Máscara: R>127 = andável, 1 px = célula de 8 u; fora da máscara = bloqueado. | A arte da v2 é pintada; a máscara garante que o jogador respeite o desenho. LDtk fica para mapas futuros, se o time quiser. |
| D2 | **Colisão por física Godot**: `MaskCollision` (StaticBody2D) gera retângulos mesclados (`WalkGrid.BlockedRects`). Mapas sem máscara usam formas `walk`/`block` da spec convertidas em polígonos. Jogador `CharacterBody2D` com círculo r=4 no pé. | Física nativa é rápida, extensível (monstros, projéteis) e já validada no smoke test. |
| D3 | **Escala contínua e filtro Linear** para mapas e sprites (sprites do atlas têm ~5 px/u e são reduzidos; mipmaps nos sprites). Viewport base 640×360, `stretch=canvas_items`, `scale_mode=fractional`. Câmera própria: zoom = 360/400 em mapas `hd`, 360/300 nos demais; seguimento `lerp(min(1, dt·7))`. UI e fontes pixel continuam nítidas. | A arte v2 é ilustrada em alta densidade; Nearest serrilha. Substitui "Nearest / escala inteira" do CLAUDE.md §2. |
| D4 | **10 selos de rank**: E, D, C, B, A, S, SS, SSS, **SSSS**, Alvor, com os limiares de XP da v2, e **Nível (Nv.) separado do Selo** (curva da v2). | A v2 e o Arsenal (146 itens) usam SSSS e nível. Substitui a tabela de 9 ranks do CLAUDE.md §10. |
| D5 | **Dados do jogo em JSON** (`src/Data/*.json`) carregados por `Vespera.Core` (`GameDatabase`), não em `.tres`. Balanceamento continua fora do código. | O Core precisa carregar e testar os dados sem Godot (e servir ao servidor da fase online). Centenas de entradas são mais fáceis de revisar em JSON. |
| D6 | **Bugs do protótipo são corrigidos para o comportamento pretendido**, não reproduzidos: `broken()` só é verdadeiro para partes existentes e quebradas; caster usa `d.triple`; laços com `RI` sorteiam o limite uma vez; clima usa `dt`; spawns em célula bloqueada são corrigidos com `NearestWalkable`; NPCs não atravessam parede; `firstDone` é salvo. | Fidelidade à intenção, não aos acidentes. Cada correção fica comentada no código com a referência da spec. |
| D7 | **Arredondamento JS**: toda conta herdada do protótipo usa `JsMath.Round(x) = floor(x + 0.5)`. | `Math.Round` do C# usa banker's rounding e muda preços (30 em invasão: JS 41, C# 40). |
| D8 | **RNG injetável** (`IRandom`) em toda lógica de Core; produção usa um `Random` com seed por sessão. | Testes determinísticos de loot, ondas e IA. |
| D9 | **Gamepad suportado** (a v2 não tem). Ações nomeadas em `InputActions`; mira por stick direito ou direção de movimento. | CLAUDE.md §4: teclado e gamepad desde o início. |
| D10 | **Sprites de `assets/src/` (20 monstros e 36 ícones de material) são usados**, mesmo não carregados na v2. Pivô = centro da base do recorte não transparente. | Sem eles, 4 temas de Brecha ficam sem monstros visíveis. |
| D11 | **Fontes**: Cinzel (títulos, OFL) e Pixelify Sans (texto) como na v2; VT323 continua disponível para números de HUD. | Identidade da v2. |
| D12 | **Plugins adiados**: Dialogic 2, Phantom Camera e LimboAI **não** são instalados agora. Diálogo, câmera e IA são C# próprios, pequenos e testáveis. LimboAI com C# exige a versão módulo (build customizado do Godot), incompatível com o CI oficial. | Menos dependências, CI simples. Reavaliar quando o conteúdo crescer. |
| D13 | **Autoload de log chama `Log`** (classe estática), não `Logger`. | Godot 4.5+ tem a classe nativa `Godot.Logger`. |
| D14 | **Godot 4.7.2 .NET + `net8.0`**. O .NET 8 sai de suporte em 10/11/2026: migrar para `net10.0` assim que o time validar (Godot 4.7 aceita .NET 8+). | Stack travada no CLAUDE.md; o fim de suporte é iminente. |
| D15 | **Smoke test é o playtest automático oficial** (`-- --smoke-test`). Toda mecânica nova ganha um cenário em `src/Testing/Scenarios/`. | Garante que o jogo continua jogável a cada PR. |
| D16 | **Export Windows gera `Vespera.exe` + pasta `data_Vespera_windows_x86_64`** (o C# do Godot não embute tudo no .exe). O zip de release leva os dois. | Limitação do Godot .NET; corrige a promessa de "um único .exe" do CLAUDE.md §8. |
| D17 | **Itens do Arsenal (146)** (`Downloads/VESPERA_Arsenal_146_Itens_Assets_Claude.zip`) são o catálogo de equipamentos do jogo; entram depois do port da v2, mapeados para os mesmos `GearDef`. | Pedido do autor: "considerar como arsenais do jogo". |

## 2026-10-20 — Dados e núcleo da v2 (frente A1)

| # | Decisão | Motivo |
|---|---|---|
| D18 | **Toasts de parte quebrada só nas partes do chefe a que o texto se refere** (`angler` Isca/Barbatanas, `reaper` Capuz/Corrente; campo `toast` da parte). O toast de 50% do Abissal e do Ceifador, que estava no código de IA (`combat.js:306,329`), vira o `phase2` desses chefes nos dados. | No protótipo o texto era por id de parte e disparava também para o Pescador e para a corrente do Sino Vivo (spec 02 §5.10), com frases sobre o chefe. |
| D19 | **Save v2 (`saveVersion: 2`) com migração do v1 da fundação**: `totalXp` → `xp`; `fragments` → material `estilhaco`; `weaponLevel` → nível da Lâmina (classe padrão `rules.start.defaultClass`); `day` → `day`; `lantern` → Lanterna perdida com os Estilhaços na bolsa. O resto vem do jogo novo. Ids que não existem mais nos dados são descartados ao carregar (com aviso no log). | O v1 só tinha XP, Fragmentos, nível da arma, dia e Lanterna; a v2 é o `G.s` do protótipo. |
| D20 | **Outono continua sem efeito de jogo** (só o texto "Espectros a mais em cada onda."), como na v2. | A spec não define quantos espectros nem de qual tipo; inventar número viola "não invente". Revisitar com o time. |
| D21 | **Ondas: se o sorteio por peso não cair em nenhum mob (erro de ponto flutuante), usa o último mob do tema**, não `wolf`. | O fallback `'wolf'` do protótipo (`world.js:87`) colocaria lobos no Cemitério ou na Catedral (D6). |
| D22 | **Ids de estação ASCII** (`primavera`, `verao`, `outono`, `inverno`) nos JSON e no código; o nome exibido fica em `name`. Raridade ausente vira `comum` e valor ausente vira `3` escritos explicitamente em `materials.json`. | Chaves estáveis sem acento; os defaults da v2 ficam visíveis nos dados. |
| D23 | **Selos usam as cores da v2 (`ranks.json`)**; as formas continuam as do CLAUDE.md §6 e o **SSSS é uma estrela de oito pontas com anel interno**, entre a estrela do SS/SSS e o sol do Alvor. A barra fina do HUD mostra o progresso do **Nível** (v2), e o rótulo "Nv. N · Selo X". | Pedido da frente: "cores dos dados; SSSS com forma própria". |
| D24 | **Invasão: `5 + min(4, rk)` mobs e sempre um Alfa** (`alphaAbove: 4` nos dados; com base 5 a condição `n > 4` da v2 é sempre verdadeira). | Fidelidade ao comportamento efetivo; o limiar fica no JSON para o time ajustar. |

## 2026-10-20 — Mundo base (frente A2)

| # | Decisão | Motivo |
|---|---|---|
| D25 | **Toda chegada passa por `World.CorrectToWalkable`** (`NearestWalkable`, r=4, passos de 2 u, até 96 u). Único caso encontrado: volta da Brecha no spot 1 `(1174, 558)` cai em célula bloqueada e vira `(1174, 532)`. Os demais spawns/chegadas da §9.3 são andáveis (testes do Core para mapas por formas; smoke para os hd). | D6: corrigir o dado ruim, não reproduzi-lo. |
| D26 | **Interagíveis do `maps.json` ganham `id`** (`odete`, `board`, `bras`, `oficios`, `portal0/1`, `lucia`, `rest`, `gaspar`, `vesna`, `ivo`, `abel`); `kind` do `InteractionRequested` = nome da ação da v2. Portais (com `cond`) começam desligados, rótulo `Brecha · {bairro}` preenchido por quem abre a Brecha. | A spec não dá ids; lojas, diálogos e Brechas precisam de uma chave estável. |
| D27 | **Fuga (`back`)**: um empurrão de 14 u (não os 28 u acidentais da v2, spec §10) e no máximo um `DungeonEscapeRequested` por segundo. **Porta** só com `World.DoorOpen`. | Sem o modal da v2 pausando o mundo, o pedido repetiria a cada frame. |
| D28 | **Colisores exatos**: mapas por formas viram faixas horizontais nas bordas dos retângulos (exato); o círculo do Núcleo vira faixas de 2 u com a corda mais estreita (colisor ≥ área bloqueada). Moldura bloqueada de 16 u em volta de todos os mapas (inclui as máscaras: fora da grade = bloqueado). | Não rasterizar formas (spec §9.2) e nunca deixar o jogador num ponto que a regra da v2 considera bloqueado. |
| D29 | **Tremor de câmera** na escala da v2 (px de tela): amplitude em u = `shake × altura_visível / 720`. | A v2 mede em device px; 720p é a janela padrão do projeto. |
| D30 | **`maps.json`** tem só os 13 mapas da v2 (sem os legados `fish`/`crypt`), caminhos `res://` e os textos descritivos da spec (`hunters`, `voltaDaBrecha`) mantidos como texto e ignorados pelo parser. | Fidelidade à §9.3 sem inventar formato para regras que são código. |
| D31 | **Filtro por nó** (Godot 4 não filtra no import): `World/Map` Linear, `World/Entities` Linear com mipmaps; o padrão do projeto continua Nearest para a UI pixel. Câmera do jogador removida em favor da `WorldCamera`. | Aplica D3 sem borrar a UI. |

## 2026-10-20 — Áudio

| # | Decisão | Motivo |
|---|---|---|
| D32 | **SFX são a transcrição da síntese WebAudio da v2** (`tools/audio/gen_sfx.py`), com o ganho mestre 0.22 preservado (picos de −37 a −23 dBFS). Única mudança: o ruído do `portal` (0.6 s) não é mais cortado pelo buffer de 0.5 s (D6). Para o jogo não soar baixo demais, o bus Master tem um `AudioEffectHardLimiter` com `pre_gain_db` +12 dB e teto −1 dBFS (picos passam a −25 a −11 dBFS; música −12), igual para todos, então a proporção da v2 se mantém. | Fidelidade ao *game feel* do protótipo sem obrigar o jogador a subir o volume do sistema (os barramentos só vão até 0 dB). |
| D33 | **Música em WAV gerado por script** (`tools/audio/gen_music.py`), sem encoder OGG disponível; import com QOA (padrão do Godot) e `edit/loop_mode=2` (Forward). Pico das faixas 0.06, abaixo dos SFX. | Sem dependências externas; o loop fica no `.import` versionado. |
| D34 | **Pool de áudio**: 8 players sem posição + 16 `AudioStreamPlayer2D` (alcance e atenuação padrão do Godot), no máximo 4 vozes por id em cada pool (rouba a mais antiga), pitch ±5% por padrão (a v2 não variava). `ui` toca no barramento UI; os demais no SFX. | Números simples e ajustáveis; evitam estouro com muitos acertos sem criar nós. |
| D35 | **Ganchos automáticos do `AudioManager`**: `coin` em todo `FragmentsChanged` (a v2 toca `coin` ao ganhar e ao gastar), `hurt` em `PlayerDamaged`, `rank` só em subida de `RankChanged`, `brecha` + reverb do bus Music em `PortalEntered`; música por local conforme D22. As outras frentes chamam `PlaySfx` direto (`docs/design/api-audio.md`). | Mínimo para o jogo soar já, sem tocar em código de outras frentes. |
| D36 | **Música por local tolerante a nomes**: `LocationChanged` de local sem registro toca `city_day` sem reverb; mapas de Brecha se registram com `RegisterLocationMusic(nome, "brecha", reverb: true)`. Nenhum nome de mapa fica fixo no `AudioManager`. | A frente Mundo troca a cena Lumeira por World com nomes de exibição por mapa ("Guilda dos Lanterneiros", "Rua da Forja"); mapear por nome quebraria no merge. |
| D37 | **Crossfade com 3 players**: a música nova entra num player parado (ou no mais baixo, se os 3 tocam) e as outras saem do volume em que estão. | Trocar de novo no meio de um fade (cidade → Brecha → cidade) não corta a faixa que sai. |

## 2026-10-20 — Super kit visual

| # | Decisão | Motivo |
|---|---|---|
| D38 | **Do super kit entram só:** (1) a sequência do portal abrindo (`assets/sprites/fx/portal_open_0..5.png`, 6 quadros recortados com base alinhada) para a Brecha abrindo na véspera; (2) `docs/brand/reference/hud-kit.png` como **guia oficial de design da UI** (barras, retrato, números de dano, alerta de chefe, tooltip, caixa de loot, diálogo); (3) o catálogo do Arsenal (`docs/design/arsenal/`, 146 itens) como dados de referência (D17). **Não entram:** os 6 fundos 16:9 (idênticos aos mapas da v2 já no jogo), a folha de monstros (poses já no atlas), os mapas/ilustrações com texto em inglês e nomes divergentes, o painel de objetos das dungeons 6–10 (sem transparência, conteúdo futuro) e as animações do arsenal (qualidade abaixo da arte da v2). | Só o que melhora a jogabilidade sem duplicar arte ou conflitar com a v2. |

## 2026-10-21 — Combate e Brechas (frente B1)

Números prefixados com a frente (`B1-`) para não colidir com as outras frentes da fase B no merge.

| # | Decisão | Motivo |
|---|---|---|
| B1-D39 | **IAs genéricas consultam as partes por índice** (0 = a da frente, 1 = a das costas), como o `knight` da v2 já fazia; parte inexistente não conta como quebrada (D6). O `caster` usa `triple` dos dados e só sai de `idle` com o jogador a ≤ 230 u. | Na v2 o `broken('jaw')` dos monstros sem `jaw` (Pescador: Isca) nunca valia; por índice o efeito da parte funciona em todos. |
| B1-D40 | **A Lanterna perdida aparece na mesma Brecha ou na próxima Brecha do mesmo tema** (mesma sala, no ponto da morte). | As Brechas não são salvas e o id muda entre sessões; sem isso a Lanterna sumiria ao recarregar. |
| B1-D41 | **Dois slots de Eco**: o 1º é o Eco equipado (`ecoEq`, Q da v2); o 2º é o primeiro outro Eco absorvido, na ordem em que foi obtido. Sem campo novo no save. | Pedido da fase B (2 slots) sem mudar o save v2. |
| B1-D42 | **Ações novas** `weapon_next/prev` (roda, RB/LB), `weapon_1..5` (1–5), `potion` (R, direcional ↑), `eco_1` (Q, LT), `eco_2` (C, clique do analógico direito), `ultimate` (F/V, RT), `aim_*` (analógico direito). O Back do gamepad fica com o inventário da Interface. | Teclas da v2 (spec 01 §5.1) + gamepad (D9) sem colidir com as outras frentes. |
| B1-D43 | **Campo de perseguição** (`ChaseField`, BFS em células de 8 u a partir do jogador, fatiado em ≤ 6000 células por frame): monstros contornam paredes, água e props quando a reta até o jogador está bloqueada; ilhas da máscara são rotuladas e **ondas só nascem onde se chega a pé até o jogador**. | Na v2 o `moveEnt` só desliza eixo a eixo: monstro nascido atrás de uma banca nunca chega e a onda trava. |
| B1-D44 | **Objetos do mundo apanham pelo grupo `hittable`** com `Hit(int dano, Vector2 origem)` (sem depender da classe do motor): setor do golpe, onda do Martelo, orbe e flechas. A lista do grupo é refeita só em golpe/disparo (no máximo 1× por frame). | Contrato da fase B com a frente do motor; buscar o grupo por frame alocaria. |
| B1-D45 | **Texto do combate rasterizado no tamanho da janela** (`oversampling` = fator da janela sobre 640×360) e com contorno escuro; números 7/9 u (a v2 usa ~6/7,5). | Sem isso o `DrawString` sai em 6 px e é ampliado em blocos: ilegível em monitor grande. |
| B1-D46 | **Props sólidos não travam o passo do monstro**: o passo testa só o mapa e depois o monstro é empurrado para fora da elipse do prop (`dx² + 2,2·dy²`, raio do prop + raio de movimento). Encostado numa quina de parede, a direção do campo gira em passos de 45° até achar uma que ande. | Colado num caixote, o passo de ~0,6 u caía na faixa mais larga da elipse e o monstro ficava parado para sempre (onda travada no smoke). |
| B1-D47 | **Avisos de golpe (telegraphs) em âmbar** (`#F2A541`, contorno `#FFD68C`), com as formas e alfas da v2. | Pedido da frente; o magenta da v2 some nas salas roxas/magenta e no Núcleo. |
