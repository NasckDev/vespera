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
