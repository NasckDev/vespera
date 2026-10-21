# API da Interface (frente B3)

Título, HUD, diálogos, escolhas, lojas/forja, Mural, inventário, pausa/opções, morte e Novo Selo. As outras frentes
falam com a UI **só pelo `EventBus`** (contratos em [`contratos-fase-b.md`](contratos-fase-b.md)); os atalhos C# abaixo
servem para código da própria UI e para testes. Decisões B3-D39–B3-D57 em [`DECISIONS.md`](DECISIONS.md).

## Cenas

| Cena | Script | Onde |
|---|---|---|
| `scenes/UI/Title.tscn` | `UI.Title.TitleScreen` | aberta pelo `Main` (fora do smoke). Novo jogo → história → classe → World (Guilda 250,560 + tutorial); Continuar → Guilda (250,330). |
| `scenes/UI/Hud.tscn` | `UI.Hud` (CanvasLayer 10) | instância no `World.tscn`. |
| `scenes/UI/GameUi.tscn` | `UI.GameUi` (CanvasLayer 20) | instância no `World.tscn` (`World = ".."`). Pilha de telas, serviços da cidade, missões. |

## O que a UI escuta

| Sinal | O que acontece |
|---|---|
| `InteractionRequested(kind, id)` | `talkClerk`, `openBoard`, `talkSmith`, `oficiosDialog`, `portalDialog`, `talkInn`, `restDialog`, `talkGaspar`, `talkVesna`, `talkIvo`, `talkAbel` abrem a fala/loja da v2 (só sem tela aberta). Outros `kind` são ignorados. |
| `DialogueRequested(speaker, lines)` | Uma fala por linha (máquina de escrever 75 car/s, retrato de `npcs.json/portraits`). No fim: **`DialogueFinished`**. `{name}`, `{class}`, `{ultimate}` são preenchidos. |
| `ChoiceRequested(id, speaker, text, options)` | Fala com opções navegáveis. Escolheu: **`ChoiceMade(id, índice)`**; Esc/B: **`ChoiceMade(id, -1)`**. |
| `ScreenRequested(id)` | `board`, `inventory`, `pause`, `options`, `journal`, `help`, `controls`, `death`. |
| `ToastRequested(msg)` | Toast no centro. `"título\nsubtítulo"` mostra os dois. |
| `PlayerDied` | 2,4 s depois abre a tela de morte (texto de Brecha se entre `BrechaStarted` e `BrechaEnded`). Confirmar (após 1,2 s) emite **`RespawnRequested`**. |
| `PortalOpened` / `PortalClosed` / `InvasionRequested` / `InvasionStateChanged` | Lista de Brechas do HUD e do Mural (`BrechaTracker`). `InvasionStateChanged` também liga `GameState.Invasion` (preços ×1,35, Brás fechado). |
| `BrechaStartRequested` / `BrechaStarted` / `BrechaEnded` | Em qual Brecha o jogador está (HUD "você está dentro", tela de morte). `BrechaStarted` conta a missão `entered`. |
| `StaminaChanged`, `EssenceChanged` | Barras de Energia e Essência (com a marca do custo do Eco equipado). |
| `AbilityCooldownChanged(id, restante, total)` | `ultimate` → botão do Supremo (e banner com o nome quando `restante ≈ total`); `potion` → véu do slot R; ids que começam com `eco` → slot Q. A UI conta o tempo sozinha entre um sinal e outro. |
| `WeaponChanged(arma)` | Slot selecionado da hotbar e nome da arma. |
| `WaveStarted(n, total)` | "Onda n de total" no canto superior direito. |
| `BossSpawned(nome, hp)` / `BossHealthChanged(hp, max)` | Alerta "NOME / Quebre suas partes…" (3,2 s) e barra no topo; some com hp 0 ou `BrechaEnded`. |
| Estado (`GameLoaded`, `PlayerDamaged`, `LumesChanged`, `MaterialChanged`, `RankChanged`, `DayChanged`…) | HUD, caixa de loot (ganhos), Novo Selo (subida de Selo), toast de dia novo. |

## O que a UI emite

| Sinal | Quando |
|---|---|
| `BrechaStartRequested(spot)` | O jogador confirmou "Entrar na Brecha" no portal (respeita `BrechaRules.CanEnter`). |
| `ChoiceMade`, `DialogueFinished`, `RespawnRequested` | Ver acima. |
| `PauseChanged(bool)` | A pilha de telas passou a ter (ou deixou de ter) tela modal — qualquer uma, não só a pausa (B3-D42). |
| `LanternRescueRequested(delaySeconds)` | **Novo.** Pagou a Odete pelo resgate; o Combate agenda a volta da Lanterna. |
| `CameraShakeRequested(4 / 3)` | Melhorar arma / forjar infusão (v2). |

Durante qualquer tela modal: `GameManager.BlockGameplay("ui")` (e `"sleep"` no fade de dormir). Telas cheias
(inventário, pausa, mural) também escondem os painéis do HUD (`Hud.ChromeVisible = false`, B3-D54).

## Atalhos C# (UI e testes)

- `GameUi.Current`: `ShowDialog(speaker, text, choices?, next?, cancel?)` com `DialogChoice(label, sub, run, disabled, icon) { Tooltip = () => ItemInfo }`;
  `OpenBoard()`, `OpenInventory()`, `OpenPause()`, `CloseAll()`, `Toast(...)`, `Note(...)`, `GoToMap(mapId)`, `Sleep(serviço)`;
  telas `Dialog`, `Board`, `Inventory`, `Pause`, `Options`, `Journal`, `Death`; `Brechas` (`BrechaTracker`); `Town` (`TownServices`).
- `Hud.Current`: `Toast(title, sub, color, icon, seconds)`, `Note(text, icon)`, `RefreshBrechas()`, `NextDayMessage`.
- `GameSettings` (Autoload, estático): `Fullscreen`, `ScreenShake`, `QuestHints`, evento `Changed`. Volumes continuam em `AudioManager`.
- `GameUi.PendingTutorial = true` antes de entrar no World roda o tutorial da Odete (o título faz isso no jogo novo).

## Estilo para quem fizer UI

Tema `src/UI/Theme/vespera_theme.tres` (fonte padrão Pixelify Sans 8 px base): variações `OrnatePanel`, `SoftPanel`,
`InsetPanel`, `SlotPanel`/`SlotFocus`, `KeyCap`, `RowNormal`/`RowFocus`, `TabNormal`/`TabFocus`, `CardNormal`/`CardFocus`,
`ButtonNormal`/`ButtonFocus`/`ButtonDanger`; rótulos `TitleLabel`, `HeadingLabel`, `SubLabel`, `BodyLabel`, `BoldLabel`,
`NumberLabel`; barras `UiBar/hp|energy|essence|xp|boss`. Widgets prontos em `src/UI/Widgets` (`OrnatePanel`, `IconRect`,
`KeyCap`, `UiBar`, `SelectableRow`, `ItemTooltip`) e atalhos em `UI.Common.Ui`. Cores em `UiPalette`. Números de dano no
mundo (Combate) devem definir a própria fonte/tamanho: o padrão do tema é pequeno.
