# API do mundo (frente A2) — para as próximas frentes

Resumo do que o mundo base oferece. Specs: [`prototype-v2/01-mundo-render-fluxo.md`](prototype-v2/01-mundo-render-fluxo.md) §1.7–1.8, §2–5, §9.
Unidade: **1 u = 1 px da pintura do mapa**, origem no canto superior esquerdo. Toda entidade tem a origem no **pé**.

## Fluxo

`Main` → `GameManager.ChangeScene("res://scenes/World/World.tscn")` → `World` abre na Guilda (250, 560) (novo jogo da v2).
Para outro início (ex.: "Continuar" → Guilda (250, 330)), defina antes da troca de cena:

```csharp
World.World.PendingStart = ("guild", new Vector2(250, 330));
```

## `World` (`src/World/World.cs`, cena `scenes/World/World.tscn`)

| Membro | Uso |
|---|---|
| `GoMap(string mapId, Vector2 at, bool instant = false)` / `GoMap(id, x, y, instant)` | Troca de mapa. Normal: fade-out 3,5/s → carrega → fade-in 3/s (spec §1.2). `instant`: carrega já e só clareia. Ignorado se já estiver trocando. A chegada passa por `CorrectToWalkable` (D6/D25). |
| `LeaveDungeon(int portalSpot)` | Volta da Brecha para a Rua da Forja em `PORTAL_SPOTS[spot] + (0, 24)` (fuga ou portal de saída). |
| `Map` (`MapDef`), `MapId`, `WalkArea` (`IWalkArea`) | Mapa atual e área andável (teste de ponto, `CanStand`, `NearestWalkable`, pathfinding). |
| `Entities` | Raiz com y-sort (filtro Linear com mipmaps). Mobs, props, NPCs, picks: adicione aqui. **Tudo menos o jogador é liberado em cada troca de mapa.** |
| `DoorOpen` | Ligue quando a sala do tema estiver limpa: o pé dentro de `door` leva a `core` (250, 960). Volta a `false` em cada troca. |
| `GetInteractable(id)`, `SetInteractableEnabled(id, enabled, label?)` | Ex.: abrir a Brecha do spot 0: `SetInteractableEnabled("portal0", true, "Brecha · Mercado")`. |
| `IsTransitioning`, `FadeAlpha`, `MapLoads` | Estado do fade (sem saídas nem movimento com `IsTransitioning`). |
| `Camera` (`WorldCamera`) | Ver abaixo. |
| `World.Catalog` (estático) | `MapCatalog` do `src/Data/maps.json`. |

Saídas (`exits`), porta (`door`) e fuga (`back`) são testadas pelo **ponto do pé** contra retângulos com bordas inclusivas, a cada frame de física.

## Sinais (`EventBus`)

| Sinal | Quando |
|---|---|
| `MapChanged(mapId, mapName)` | Mapa novo carregado (no meio do fade). Também emite `LocationChanged(mapName)`. |
| `InteractionRequested(kind, id)` | Jogador apertou interagir. `kind` = ação da v2, `id` = id no maps.json (tabela abaixo). |
| `DungeonEscapeRequested(mapId)` | Pé andando dentro de `back`: o jogador já foi empurrado 14 u para cima; abra o diálogo "Fugir para Lumeira" e chame `LeaveDungeon(spot)` se ele fugir. No máximo 1 por segundo. |
| `DungeonDoorEntered(mapId)` | Passou pela porta liberada (o World já está indo para `core`). |
| `CameraShakeRequested(amount)` | Qualquer sistema pode pedir tremor (escala da v2: 3 golpe forte, 8 ruptura/chefe). |

| Mapa | `id` | `kind` |
|---|---|---|
| guild | `odete`, `board` | `talkClerk`, `openBoard` |
| forge | `bras`, `oficios`, `portal0`, `portal1` (desligados) | `talkSmith`, `oficiosDialog`, `portalDialog` |
| inn | `lucia`, `rest` | `talkInn`, `restDialog` |
| shop_arm / shop_mag / shop_alq / shop_rel | `gaspar` / `vesna` / `ivo` / `abel` | `talkGaspar` / `talkVesna` / `talkIvo` / `talkAbel` |

Para Interactables próprios (NPCs, baús, lanternas), crie um `Interactable` com `Kind`, `Id`, `Prompt` e `Radius` (cria o círculo sozinho).

## `WorldCamera` (`src/World/WorldCamera.cs`)

- Zoom 1 em todos os mapas com escala inteira da janela (B4a-D39); mapas menores que a vista ficam centralizados; seguimento `1 − e^(−7·dt)` do pé interpolado −14 u × `characterScale`, arredondado ao px de tela (B4a-D42).
- `Shake(amount)` (mantém o maior), `StopShake()`, `CurrentShake`.
- `LookAtTarget = chefeNode` puxa o foco para o chefe (`lerp` 0,3 / 0,35 em y−30); `null` volta ao normal (zerado em cada troca de mapa).
- `Snap()` pula direto para o alvo; `VisibleSize` em u. Matemática testada em `Vespera.Core.World.CameraFraming`.

## `AtlasSprite` e `AtlasLibrary` (`src/World/`)

- `AtlasSprite` é um `Sprite2D`: `Key = "c_sword_front"` (ou `"shadow"`, `"por3"`, `"cnev_f"`…), `SpriteScale` (o `o.s` da v2). Aplica a textura do recorte, `offset = -âncora` (pé) e `scale = SpriteScale / k`. Trocar `Key` para a mesma chave não faz nada.
- **Espelhar**: `scale.x = -1` num `Node2D` pai (gira em torno do pé). **Bob**: mover o pai em y.
- `AtlasLibrary.TryGet(key, out SpriteFrameInfo)`: atlas primeiro; depois PNG soltos de `assets/sprites/enemies` e `items` (pivô = centro da base do recorte não transparente, D10; k do `meta.json`).

## `Player` (`src/Player/Player.cs`)

- 78 u/s × `SpeedMultiplier`, aceleração 0,06 s e frenagem 0,05 s (B4a-D42); `MovementLocked` trava o input; `CircleShape2D` r=4 no pé; `Teleport(pos)`.
- Visual: `ClassId` (`sword`, `bow`, `staff`, `hammer`, `dag`) escolhe `c_{classe}_{front|back|side|atk}`; `AttackPose` + `Aim` mostram a pose de ataque virada para a mira. A Lâmina usa os quadros do super kit; as outras classes e as vistas frente/costas, caminhada procedural (`PlayerAnimator`, B4a-D43).
- `Direction` (`FacingDirection`), `Facing`, `IsMoving`, `FocusedInteractable`.

## Core (`src/Core/World`, `src/Core/Atlas`)

`MapCatalog`/`MapDef` (maps.json), `IWalkArea` (`ShapeWalkArea`, `MaskWalkArea`; extensões `CanStand`, `NearestWalkable`),
`WalkPathfinder` (BFS de 4 u; útil para NPCs não atravessarem parede, D6), `CameraFraming`, `AtlasIndex`.

## Ainda não feito aqui (próximas frentes)

Iluminação/escuridão e `lights.json`, ciclo do dia, NPCs com rotina (`MapDef.Npcs`), caçadores ambiente, Odete animada e o
patch da arte da Guilda (`MapDef.Clerk`/`Patch`), bonecos de treino (`MapDef.Dummies`), props sólidos na colisão, portais
animados, diálogos/lojas (ligar em `InteractionRequested`), esquiva/ataques/morte do jogador, título.

## Motor do mundo (frente B4a) — layout, objetos, efeitos

Decisões B4a-D39 a B4a-D47 em [`DECISIONS.md`](DECISIONS.md). Cenários de smoke: `--smoke-filter=motor`.

### Nitidez, fluidez e proporção

- Escala inteira da janela (640×360 base, `aspect=expand`), câmera com zoom 1 arredondada ao px de tela. Mapa e recortes
  em Nearest; tudo em `Entities` é Linear com mipmaps (PNG novos de sprite: importe com `mipmaps/generate=true`).
- `Player.VisualPosition` (pé interpolado) e `Player.VisualHeight` (56 u): use para desenhar coisas presas ao jogador.
  Teleporte sempre por `Player.Teleport` (zera a interpolação).
- `rules.world.characterScale` (1,75) vale para chaves `c_*`, `npc_*`, `shadow` e sprites de monstro dos dados;
  `propScale` (1,25) para `pr_*`. `AtlasSprite.ScaleClass` força outra classe (`Fixed` para ícones e efeitos).
- Combate: `Player.PlayAttack(duração)`, `PlayHurt()`, `PlayDeath()`, `Revive()`, `IsDead`; `AttackPose`/`Aim` continuam.

### Layout (`assets/maps/<pasta>/layout_<id>.json`)

O motor lê tudo da docstring de `tools/maps/layout.py` e mais dois campos:

```jsonc
"fx": [ {"kind": "smoke|fireflies|embers", "x": 0, "y": 0, "r": 24} ],   // fumaça de chaminé, vagalumes (19h–5h), brasas
"objects": [ {"id": "<id do catálogo>", "x": 0, "y": 0, "flip": false} ]  // objetos da frente B5, (x, y) = pé
```

Colisão: `walk_<id>.png` gerado vence; sem ele, as formas do layout (D40). Decor: `lamp`, `flower`, `boat` (precisa de
`to`), `fishing`, `bench`, `sign`, `fountain`, `stall`, `inspect` (`label` troca o prompt, `text` o texto mostrado).

### Catálogo de objetos (`src/Data/objects.json`, frente B5)

```jsonc
{ "objects": { "<id>": {
    "name": "Lampião de ferro",
    "sprite": "res://assets/sprites/objects/<tema>/<id>.png",   // ou "atlas:<chave do atlas da v2>"
    "ppu": 3,                       // px do PNG por u (1 = desenhado na escala da pintura do mapa)
    "anchor": [24, 140],            // pé em px do PNG (sem: centro da base do recorte opaco)
    "footprint": {"ellipse": [6, 3]},   // área sólida em u relativa ao pé: ellipse [rx, ry] | rect [x,y,w,h] | circle [cx,cy,r] | poly
    "occluder": true,               // translúcido (40%) com fade quando o jogador passa por trás
    "sway": false,                  // balança com o vento (shader de vértice barato)
    "light": {"kind": "a|f|m|c|g", "r": 46, "x": 0, "y": -38},   // halo relativo ao pé (f = fogo, com brasas)
    "interact": {"kind": "lamp|sign|bench|fountain|stall|inspect|flower|fishing", "label": "…", "text": "…", "r": 24},
    "breakable": "barrel",          // vira quebrável (vida de rules.world.props.hp)
    "chest": 1                      // vira baú desse tier
} } }
```

**Na integração v0.1** o `objects.json` real é o gerado pela frente B5 (com `reference`/`themes`, lido por
`ObjectCatalog`); o motor o reconhece e converte com `DressingCatalog.FromLibrary` (`file` → sprite, `density` → ppu,
âncora, footprint retangular, cor da luz → tipo de `LIGHTC` mais próximo, `break` → quebrável, `chest` → baú tier 0,
camada `floor` por baixo e `overhead` por cima). O formato acima continua valendo para fixtures (`DressingDef`).
Aceita também lista (`[{"id": …}]`). Campos desconhecidos são ignorados. Enquanto o catálogo não existe, o smoke usa
`src/Testing/Fixtures/objects_fixture.json` (`WorldDressing.ExtraCatalogPath`).

### `WorldDressing` (`World.Dressing`)

| Membro | Uso |
|---|---|
| `Breakables`, `Chests`, `Objects`, `Occluders`, `Decor` | O que foi montado no mapa atual. |
| `Breakable.Hit(dano, origem)` + grupo `hittable` | O combate acerta quebráveis por aqui; desligue o golpe provisório com `WorldDressing.FallbackAttackEnabled = false`. |
| `WorldLoot.Sink` | Na Brecha, desvia o saque de quebráveis/baús para a bolsa da caçada (devolva `true`). |
| `Lights.AddLight/SetLit`, `WorldFx.*` | Halos e chaves de efeitos (a frente Cidade pode desligar `WorldFx.Lights` se trouxer as luzes dela). |
| `World.Objects` (`WorldObjectRegistry`) | Quebrados/abertos/colhidos voltam no `DayChanged`. |
| `World.Minimap` | `Toggle()`, `Expanded` (trava o jogo com o motivo `minimap`). |

Sinais novos (bloco B4a do `EventBus`): `PropBroken(mapId, kind, pos)`, `ChestOpened(mapId, tier, pos)`,
`WorldObjectUsed(kind, mapId, index)`. Ação nova: `map` (M, Tab, Back do gamepad).

### Áudio do mundo (`AudioManager.Ambient.cs`)

`PlaySfx("step")`; `SetAmbientEmitters(layout.Ambient, fade)` e `UpdateAmbientListener(pé, dt)` (o `WorldDressing` já
chama), `AmbientLevel(kind)`, `ActiveAmbientVoices`. Loops em `assets/audio/ambient/` (`tools/audio/gen_world.py`).

## Arte aprovada (frente C2) — DECISIONS C2-D1 a C2-D8

- **`ArtFrames.TryLoad(path, out art)`** (`src/World/Art/ArtFrames.cs`): SpriteFrames gerado por `tools/sprites/build_art.py`
  com pé (`Anchor(anim)`), densidade (`Density(anim)`) e classe de escala; `art.Apply(sprite2D, anim, quadro)` mostra um
  quadro num `Sprite2D` comum. **`ArtSprite`** (`AnimatedSprite2D`): `Setup(art)` e `Show(anim, quadro)` a cada quadro (sem `Play()`).
- **Caçador**: `Player.PlayDodge(direção, segundos)` (o combate chama ao começar o dash); `Animator.Walker` (arte nova),
  `UsesDrawnFrames`, `CurrentAnimation`/`CurrentFrame`, `WalkFrameChanges`, `TryGetGhost(...)` para rastros.
- **Monstros**: `Enemy.ArtPathFor(def)` (chefes e Alfa), `UsesArt`, `ArtPose`, `LiveScale`.
- **Objetos**: `PropArt.Breakable/Chest/Boat/FishingRod` e `PropArt.ForCatalogSprite(png)`; `Breakable.Stage`/`ShardsFlying`,
  `Chest.ArtFrame`, `CatalogObject.ArtRole`/`ArtState`/`Toggle()`/`SetFlowerStage()`, `WorldDressing.FerryBoat`/`FerryFrame`/`RodInHand`.
- **UI**: `UiIcons.Bust(chaveDoRetrato)`, `UiIcons.Hud(id)`/`HudInner(id)`, `HotbarSlot.FramedIcon`, `Hud.DodgeSlot`,
  `Hud.IsStatusShown(id)`; sinal novo `PlayerStatusChanged(status, ativo)` (`shield`, `slow`, `speed`); `shield` = `PlayerCombatState.Shielded` (i-frames concedidos: respawn, Investida, Supremos; **não** o piscar de 0,75 s depois de um golpe).
- Smoke: `--smoke-filter=arte:` (fixture `src/Testing/Fixtures/fixture_art.json`).
