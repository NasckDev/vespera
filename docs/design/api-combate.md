# API do combate (frente B1) — para as outras frentes

Combate do jogador, monstros, Brechas, morte/Lanterna e invasão. Specs: [`prototype-v2/02-dados-combate-ui.md`](prototype-v2/02-dados-combate-ui.md)
§1.13–1.16 e §3; [`prototype-v2/01-mundo-render-fluxo.md`](prototype-v2/01-mundo-render-fluxo.md) §1.9–1.10 e §4.4.
Contratos entre frentes: [`contratos-fase-b.md`](contratos-fase-b.md). Decisões da frente: `DECISIONS.md`, B1-D39 a B1-D47.

## Onde fica

| Peça | Arquivo | Papel |
|---|---|---|
| `CombatDirector` | `src/Combat/CombatDirector*.cs`, nó `World/Combat` em `World.tscn` | Dono do combate na cena World. Um único `_PhysicsProcess` avança jogador → monstros → projéteis/perigos → efeitos → itens → Brecha, na ordem do `update` da v2, sem alocar por frame. Congela com `PauseChanged(true)` ou `GameManager.IsGameplayBlocked`. `CombatDirector.Instance` para smoke e depuração. |
| `PlayerCombat` | `src/Player/PlayerCombat*.cs` | Mira, esquiva, 5 armas, poção, troca de arma, 2 Ecos, Golpe Supremo. Usa só os ganchos do `Player` (`SpeedMultiplier`, `MovementLocked`, `AttackPose`, `Aim`, `Sprite`, `Body`). |
| `BrechaController` | `src/World/Dungeon/BrechaController.cs` | Brechas abertas (`PortalOpened`/`PortalClosed`), incursão (`BrechaRun` do Core), ondas, porta, Núcleo, chefe, recompensa, portal de saída, fuga. |
| `LanternKeeper`, `InvasionController` | `src/World/Dungeon/` | Morte/Lanterna/respawn e a ruptura na Rua da Forja. |
| `Enemy` | `src/Enemies/Enemy.cs` | Só apresentação de um `MonsterBody` (Core). Sprites do atlas e de `assets/sprites/enemies` (D10). |
| Regras puras | `src/Core/Combat/` (`PlayerCombatState`, `CombatMath`, `CombatClock`, `AbilityNumbers`, `PickupMotion`, `BrechaRun`, `Ai/*`) | Testadas no `tests/Core` (GdUnit4). |

## Sinais que o combate emite

| Sinal | Quando |
|---|---|
| `BrechaStarted(theme, rank)` / `BrechaEnded(theme, cleared)` | Entrou na sala do tema / saiu (portal de saída `true`; fuga ou morte `false`). |
| `WaveStarted(wave, total)` | Cada onda (1-based). Depois da última, a porta abre (`World.DoorOpen`). |
| `BossSpawned(name, maxHp)` / `BossHealthChanged(hp, max)` / `BossDefeated(bossId)` | Barra do chefe. `BossDefeated` sai antes do portal de saída (1,4 s depois). |
| `MonsterPartBroken(monsterId, partId, partName, boss)` | Parte quebrada (a barra do chefe mostra "— quebrada"). |
| `EnemyDied(enemyId, position)` | Monstro morreu (bonecos não). |
| `LootCollected(kind, itemId, amount)` | Item coletado: `lume`, `mat`, `gear`, `potion`, `eco`, `lantern`, `mylantern`. |
| `NoteRequested(message, icon)` | Nota lateral curta da v2 ("Sem poções…", "+1 Presa Rubra"). |
| `ToastRequested(text)` | Toasts da v2 (parte de chefe, fase 2, Lanterna). Título e subtítulo separados por `\n`. |
| `ChoiceRequested("flee", "Saída da Brecha", texto, ["Fugir para Lumeira", "Continuar lutando"])` | Pé na borda `back` da sala. A Interface responde `ChoiceMade("flee", 0 \| 1)`. |
| `WeaponChanged(id)`, `StaminaChanged(cur, max)` (energia da esquiva), `EssenceChanged(cur, max)`, `AbilityCooldownChanged(id, remaining, total)` | HUD. Ids de recarga: `dash`, `potion`, `eco0`, `eco1`, `ultimate`. |
| `InvasionStateChanged(active)` | Ruptura começou / foi resolvida. |
| `CameraShakeRequested(amount)` | Tremor na escala da v2 (2 golpe, 4 parte, 6 martelo, 8 chefe nascendo, 12 chefe morrendo). |

`PlayerDied` continua saindo do `GameManager` quando o HP zera; o combate congela o jogador (pose caída) e espera a Interface.

## Sinais que o combate escuta

| Sinal | Efeito |
|---|---|
| `PortalOpened(spot, theme, rank)` / `PortalClosed(spot)` | Registra/remove a Brecha aberta do spot. |
| `BrechaStartRequested(spot)` | `World.GoMap` para o mapa do tema, `BrechaStarted`, ondas (`BrechaRules.GenerateWaves`). |
| `DungeonEscapeRequested(map)` → `ChoiceMade("flee", 0)` | `World.LeaveDungeon(spot)`, a bolsa vai para o save, `BrechaEnded(theme, false)`. |
| `DungeonDoorEntered(map)` | Sala → Núcleo; chefe nasce em 1,8 s. |
| `RespawnRequested` | `DieInBrecha` (bolsa fica na Lanterna, no ponto da morte) ou `DieInCity`; volta à Guilda/estalagem com 1,5 s de i-frames. |
| `InvasionRequested(theme, rank)` | Monstros da invasão na Rua da Forja (agora, ou quando o jogador chegar lá). |
| `PauseChanged(paused)` | Congela. |
| `MapChanged`, `GameLoaded` | Limpa pools e listas; estado de combate novo ao carregar. |

## Ações de entrada (DECISIONS B1-D42)

| Ação | Teclado/mouse | Gamepad |
|---|---|---|
| `attack` (e `attack_heavy`, que também ataca) | J / clique esq. (K / clique dir.) | X (Y) |
| `dodge` | Espaço / Shift | B |
| `weapon_next` / `weapon_prev` | roda do mouse | RB / LB |
| `weapon_1`…`weapon_5` | 1–5 | — |
| `potion` | R | direcional ↑ |
| `eco_1` / `eco_2` | Q / C | LT / clique do analógico direito |
| `ultimate` | F ou V | RT |
| `aim_left/right/up/down` | — (mouse segurado ou clicado há < 0,5 s) | analógico direito |

Sem mouse nem analógico, a mira segue a direção do movimento (D9). A classe trava a arma (v2): trocar para outra mostra a nota da v2.

## Objetos do mundo que apanham (DECISIONS B1-D44)

Golpes corpo a corpo (setor da arma), onda do Martelo, explosão do orbe, flechas e raios acertam todo `Node2D` do grupo
**`hittable`** que tenha o método **`Hit(int dano, Vector2 origem)`** — sem depender da classe. Use
`CombatDirector.HittableGroup`. O alvo é testado na posição global do nó (pé) com `y − 6`, como os props da v2.

## Notas para o merge

- **Motor (`WorldDressing.FallbackAttackEnabled`)**: o motor tem um ataque provisório que bate nos quebráveis até o combate
  existir. Depois do merge, desligue-o no `_Ready` do `CombatDirector` (`WorldDressing.FallbackAttackEnabled = false;`), senão
  cada golpe acerta duas vezes.
- **Interface (`LanternRescueRequested(delaySeconds)`)**: ligar ao `CombatDirector.Brecha.Lantern.ScheduleRescue(delay)` (resgate pago da Odete).
- **Ação `lantern`** (F / RB, da fundação, sem uso no jogo) colide com `ultimate` (F) e `weapon_next` (RB). A v2 não tem tecla de lanterna.
- **Gamepad Back** é do inventário da Interface; por isso a poção ficou no direcional ↑.
