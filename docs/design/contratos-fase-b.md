# Contratos entre as frentes da Fase B

As três frentes (Combate e Brechas, Cidade viva, Interface) trabalham em paralelo e se falam **só** por estes sinais do
`EventBus` e pelas APIs já documentadas (`api-estado.md`, `api-mundo.md`, `api-audio.md`). Não crie atalhos entre frentes.

## Quem é dono de quê

| Assunto | Dono | Os outros só… |
|---|---|---|
| Relógio do dia, véspera, estações, clima, luzes | **Cidade** | escutam `DayPhaseChanged`, `DayChanged` |
| Quais portais abrem, quando, com que tema e rank; ruptura de Brecha ignorada | **Cidade** (via `BrechaRules`) | escutam `PortalOpened`/`PortalClosed`/`InvasionRequested` |
| NPCs andando, rotina, caçadores ambiente, chat simulado | **Cidade** | — |
| Animação do portal abrindo (`assets/sprites/fx/portal_open_0..5.png`) | **Cidade** | — |
| Combate do jogador, armas, esquiva, poção, Ecos, Supremo, hitstop, números de dano | **Combate** | escutam `WeaponChanged`, `EssenceChanged`, `AbilityCooldownChanged` |
| Monstros, IA, partes, projéteis, drops/ímã, ondas, porta, Núcleo, chefe, saída | **Combate** | escutam `BrechaStarted`/`BrechaEnded`, `WaveStarted`, `BossSpawned`, `BossHealthChanged` |
| Morte, Lanterna, respawn, invasão na cidade | **Combate** (via `LanternRules`/`BrechaRules`) | a UI mostra a tela de morte e emite `RespawnRequested` |
| Título, história, classe, HUD, diálogos, escolhas, Mural, lojas, forja, inventário, pausa, opções, morte, rank-up | **Interface** | outros pedem por `DialogueRequested`, `ChoiceRequested`, `ScreenRequested`, `ToastRequested` |

## Fluxos

**Abrir e entrar numa Brecha**
1. Cidade, na véspera: escolhe o tema/rank (`BrechaRules`), habilita o interagível (`World.SetInteractableEnabled("portal0", true, "Brecha · Mercado")`), toca a animação do portal e emite `PortalOpened(spot, theme, rank)`.
2. Jogador interage com o portal → o World emite `InteractionRequested("portalDialog", "portal0")` → Interface mostra o diálogo da v2 com o tema e o rank.
3. Confirmou → Interface emite `BrechaStartRequested(spot)` → Combate carrega o mapa da dungeon (`World.GoMap`), emite `BrechaStarted(theme, rank)` e roda as ondas.

**Fugir**: World emite `DungeonEscapeRequested(map)` → Combate emite `ChoiceRequested("flee", …, ["Fugir", "Ficar"])` → Interface mostra → `ChoiceMade("flee", 0)` → Combate chama `World.LeaveDungeon(spot)` e emite `BrechaEnded(theme, false)`.

**Morrer**: Combate aplica `GameState.DieInBrecha/DieInCity`, emite `PlayerDied` → Interface mostra a tela de morte da v2 → `RespawnRequested` → Combate leva o jogador ao ponto de respawn.

**Ruptura**: Brecha ignorada pelo tempo da v2 → Cidade emite `InvasionRequested(theme, rank)` → Combate spawna a invasão na Rua da Forja e emite `InvasionStateChanged(true/false)`.

**Pausa**: Interface emite `PauseChanged(true)` e usa `GameManager.BlockGameplay("pause")`; Cidade para o relógio; Combate congela.

## Regras para não quebrar o merge

- Sinal novo que só a sua frente usa: pode criar, no bloco da sua frente no `EventBus.cs`, com comentário de quem emite e quem escuta.
- Mudar a assinatura de um sinal deste arquivo: **não**. Precisa? Crie outro e registre em `DECISIONS.md`.
- Para testar sem a outra frente, emita o sinal do outro lado no seu cenário de smoke (ex.: Interface emite `PortalOpened` falso para testar o diálogo do portal).
