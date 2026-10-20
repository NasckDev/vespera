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
| `GoMap(string mapId, Vector2 at, bool instant = false)` / `GoMap(id, x, y, instant)` | Troca de mapa. Normal: fade-out 3,5/s → carrega → fade-in 3/s (spec §1.2). `instant`: carrega já e só clareia. Ignorado se já estiver trocando. A chegada passa por `CorrectToWalkable` (D6/D18). |
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

- Zoom = altura da viewport / (400 u em `hd`, 300 u nos demais), largura mínima 380 u; mapas menores que a vista ficam centralizados; seguimento `lerp(min(1, dt·7))` do pé −14 u.
- `Shake(amount)` (mantém o maior), `StopShake()`, `CurrentShake`.
- `LookAtTarget = chefeNode` puxa o foco para o chefe (`lerp` 0,3 / 0,35 em y−30); `null` volta ao normal (zerado em cada troca de mapa).
- `Snap()` pula direto para o alvo; `VisibleSize` em u. Matemática testada em `Vespera.Core.World.CameraFraming`.

## `AtlasSprite` e `AtlasLibrary` (`src/World/`)

- `AtlasSprite` é um `Sprite2D`: `Key = "c_sword_front"` (ou `"shadow"`, `"por3"`, `"cnev_f"`…), `SpriteScale` (o `o.s` da v2). Aplica a textura do recorte, `offset = -âncora` (pé) e `scale = SpriteScale / k`. Trocar `Key` para a mesma chave não faz nada.
- **Espelhar**: `scale.x = -1` num `Node2D` pai (gira em torno do pé). **Bob**: mover o pai em y.
- `AtlasLibrary.TryGet(key, out SpriteFrameInfo)`: atlas primeiro; depois PNG soltos de `assets/sprites/enemies` e `items` (pivô = centro da base do recorte não transparente, D10; k do `meta.json`).

## `Player` (`src/Player/Player.cs`)

- 78 u/s × `SpeedMultiplier`, sem inércia; `MovementLocked` trava o input; `CircleShape2D` r=4 no pé; `Teleport(pos)`.
- Visual: `ClassId` (`sword`, `bow`, `staff`, `hammer`, `dag`) escolhe `c_{classe}_{front|back|side|atk}`; `AttackPose` + `Aim` mostram a pose de ataque virada para a mira. Bob de caminhada e de respiração como `render.js:157-171`.
- `Direction` (`FacingDirection`), `Facing`, `IsMoving`, `FocusedInteractable`.

## Core (`src/Core/World`, `src/Core/Atlas`)

`MapCatalog`/`MapDef` (maps.json), `IWalkArea` (`ShapeWalkArea`, `MaskWalkArea`; extensões `CanStand`, `NearestWalkable`),
`WalkPathfinder` (BFS de 4 u; útil para NPCs não atravessarem parede, D6), `CameraFraming`, `AtlasIndex`.

## Ainda não feito aqui (próximas frentes)

Iluminação/escuridão e `lights.json`, ciclo do dia, NPCs com rotina (`MapDef.Npcs`), caçadores ambiente, Odete animada e o
patch da arte da Guilda (`MapDef.Clerk`/`Patch`), bonecos de treino (`MapDef.Dummies`), props sólidos na colisão, portais
animados, diálogos/lojas (ligar em `InteractionRequested`), esquiva/ataques/morte do jogador, título.
