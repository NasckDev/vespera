# API de áudio (`AudioManager`)

Autoload `AudioManager` (`src/Autoload/AudioManager.cs`), carregado logo depois do `EventBus`.
Acesse por `AudioManager.Instance`. Nenhuma chamada cria nós: os efeitos usam um pool fixo
(8 players sem posição + 16 `AudioStreamPlayer2D`), então pode chamar em todo acerto sem medo.

```csharp
using Vespera.Autoload;

AudioManager.Instance.PlaySfx("hit", enemy.GlobalPosition);   // no mundo: volume e pan pela câmera
AudioManager.Instance.PlaySfx("ui");                          // "na cabeça" (menus, jogador)
AudioManager.Instance.PlayMusic("vespera", fadeSeconds: 2f);  // crossfade
```

## Efeitos

`PlaySfx(string id, Vector2? worldPos = null, float pitchJitter = 0.05f)`

- `worldPos` em coordenadas globais de mundo; sem ele o som não tem posição.
- `pitchJitter` varia o pitch em ±fração a cada chamada (0 desliga; limitado a `MaxPitchJitter` = 0.5).
- Até `MaxVoicesPerId` (4) cópias do mesmo id por pool; a 5ª corta a mais antiga. Pool cheio corta a voz mais antiga.
- Id desconhecido: um `Log.Warning` por id, e nada toca. Nunca lança exceção. Depois do encerramento do jogo
  (`_ExitTree` do autoload), `PlaySfx`/`PlayMusic` não fazem nada e não avisam.
- `ui` vai para o barramento **UI**; os demais para **SFX**.

| Id | Onde o protótipo toca (v2) | Quem chama no Godot |
|---|---|---|
| `swing` | golpe da arma | combate |
| `hit` | acerto em monstro | combate (com `worldPos`) |
| `brk` | parte do monstro quebrou | combate |
| `dash` | esquiva | jogador |
| `pick` | pegar material/poção | itens |
| `coin` | ganhar ou gastar Lumes (loot, venda, compra) | **automático** só em `FragmentsChanged` (mudança de `GameManager.Fragments`); a coleta na bolsa da caçada (v2 `collect()` soma em `bag().lumes`) chama `PlaySfx("coin", pos)` se a bolsa não for `GameManager.Fragments` |
| `hurt` | jogador leva dano; opção desabilitada no menu | **automático** em `PlayerDamaged`; UI para opção bloqueada |
| `ui` | navegar/confirmar em menus | UI |
| `open` | abrir baú, inventário, pegar equipamento/lanterna | UI / itens |
| `boom` | explosões, martelo, golpe supremo | combate |
| `bow` | disparo do arco | combate |
| `magic` | cajado, magias de monstro | combate |
| `rank` | novo Selo (arpejo) | **automático** em `RankChanged` (só subida) |
| `die` | morte do jogador | combate (`killPlayer`) |
| `portal` | Brecha abre / entrar no portal | mundo |
| `growl` | monstro "lunger" prepara o bote | inimigos (com `worldPos`) |
| `kill` | monstro morre | combate (com `worldPos`) |
| `eco` | usar/pegar Eco, recuperar a Lanterna | combate / itens |

`HasSfx(id)`, `ActiveVoices(id)`, `ActiveSfxVoices` e `SfxPoolSize` existem para testes.

## Música

- `PlayMusic(string id, float fadeSeconds = 1.5f)`: crossfade no barramento **Music** (3 players; trocar de
  novo no meio de um fade não corta quem está saindo). Pedir a música que já toca não faz nada; id desconhecido
  avisa uma vez e mantém a atual. As 3 faixas são pré-carregadas no boot.
- `StopMusic(float fadeSeconds = 1.5f)`, `CurrentMusic`, `IsMusicPlaying`.
- `SetMusicReverb(bool)`: liga o reverb leve do barramento Music (efeito 0 em `default_bus_layout.tres`).

| Id | Quando |
|---|---|
| `city_day` | **automático** em todo `LocationChanged` de local sem registro (a cidade inteira: Lumeira, Guilda, Rua da Forja…), com reverb desligado |
| `vespera` | **automático** na fase `DayPhase.Vespera` (16–19 h): o `AudioManager` assina `EventBus.DayPhaseChanged` e troca a música dos locais sem registro (a cidade); `CityMusic` diz qual toca agora. Não chame `PlayMusic("vespera")` de outro lugar |
| `brecha` | **automático** em `PortalEntered` e em `LocationChanged` de local registrado como Brecha (liga o reverb) |

### Contrato com a frente Mundo (música por local)

`LocationChanged(string locationName)` traz o nome de exibição do local. Local **sem registro** = cidade
(`AudioManager.DefaultLocationMusic`, sem reverb), então nenhum nome de mapa da cidade precisa ser conhecido aqui.
Mapas de Brecha (no `maps.json`, os que têm `dungeon`) se registram uma vez, antes de emitir o sinal:

```csharp
AudioManager.Instance.RegisterLocationMusic(map.Name, "brecha", reverb: true);
```

- `RegisterLocationMusic(string location, string musicId, bool reverb = false)` / `UnregisterLocationMusic(string location)`.
- `ApplyLocationMusic(string location)`: o que o gancho faz (testes, ou troca de área sem o sinal).
- Sem esse registro, entrar numa Brecha que não emite `PortalEntered` toca `city_day`.

## Volumes (para a tela de opções)

Barramentos: `Master`, `Music`, `SFX`, `UI` (constantes `AudioManager.MasterBus` etc., lista em `AudioManager.Buses`).

- `GetBusVolume(string bus)` → 0–1.
- `SetBusVolume(string bus, float linear, bool persist = true)`: aplica (0 muta o barramento) e, com `persist`,
  grava na hora em `user://settings.cfg`, seção `[audio]`, chaves `master`, `music`, `sfx`, `ui`. Outras seções do
  arquivo são preservadas. **Slider:** em `value_changed` use `persist: false` e chame `SaveSettings()` ao soltar
  (`drag_ended`) ou ao fechar a tela, para não gravar em disco a cada tick do arrasto.
- `SaveSettings()`: grava os volumes atuais.
- O Master tem um limitador com +12 dB de ganho de make-up (teto −1 dBFS) antes do volume do jogador (D32).
- Lidos no boot. No smoke test o arquivo é `user://settings_smoke.cfg` (apagado a cada execução).
