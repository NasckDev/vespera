# API da cidade viva (frente B2)

Relógio do dia, véspera e Brechas, iluminação/estações/clima, NPCs com rotina, caçadores ambiente e chat simulado.
Specs: [`prototype-v2/01-mundo-render-fluxo.md`](prototype-v2/01-mundo-render-fluxo.md) §1.9, §4.5–4.6, §6, §7 e
[`prototype-v2/02-dados-combate-ui.md`](prototype-v2/02-dados-combate-ui.md) §1.4, §1.17–1.18. Decisões B2-D39–B2-D47 e B2-D48–D51.

A cidade é o nó `City` (`CityLife`) da cena `World`: vive enquanto o World existir (como o `G` da v2) e fala com as
outras frentes pelo `EventBus`. Acesso direto, para leitura: `CityLife.Instance` (null fora do World).

## Relógio e fases (`CityLife.Instance.Clock`, Core `DayPhase`)

- 1 min de jogo por segundo real, 16:00 → 06:00 em 840 s, por `GameState.AdvanceClock`. Para com `PauseChanged(true)` e
  com qualquer `GameManager.BlockGameplay(...)` (menos a troca de mapa). Grava o jogo a cada 45 s.
- `DayPhase` (Core, `Vespera.Core.World`): `Vespera` 16:00–19:00, `Night` 19:00–05:00, `Dawn` 05:00–06:00.
  `DayPhases.Of(clock, db.SeasonData.Clock)`.
- Sinais: `DayPhaseChanged(int fase)` quando muda; `DayChanged(dia)` vem do `GameState` (madrugada ou `NewDay()` ao dormir).
- **Dormir**: a Interface chama `GameState.TryBuyService(rest)`/`NewDay()`. A cidade reage ao `DayChanged`: toast
  `"Dia N · Estação\nVocê dorme. Lá fora, a cidade aguenta."` (ou `"…A noite passou sem você perceber."` na virada),
  próxima Brecha em até 15 s e save. **A Interface não precisa mostrar outro toast.**
- Música: o `AudioManager` toca `vespera` na fase da véspera em qualquer local da cidade e `city_day` fora dela (Brechas não mudam).

## Brechas (`CityLife.Instance.Brechas`, Core `BrechaSchedule`)

| Membro | Uso |
|---|---|
| `Brechas` | `IReadOnlyList<BrechaInstance>` da sessão (no máximo 5, mais antiga primeiro), como `G.brechas` |
| `OpenAt(spot)`, `Find(id)`, `Running`, `NextOpenIn` | Brecha aberta num portal, por id, a da caçada em curso, segundos até a próxima |
| `TrackedId`, `Track(id)` | "Rastrear" do Mural (`G.track`) |
| `InvasionActive` | Há Brecha rompida esperando o Combate resolver |

`BrechaInstance`: `Id`, `Theme`/`ThemeId`, `Bairro`, `Season` (`SeasonDef`), `Title`, `Rank` (índice), `Spot`, `TimeLeft`,
`MaxTime`, `NpcCloseAt`, `Status` (`Open`/`Ruptured`/`Closed`), `ClosedBy` (Casa).

Fluxo (contratos-fase-b.md):

1. Na hora (`InitialOpenDelay` 16 s/6 s, depois `R(80,120)`), a cidade sorteia tema/rank/timer pelas `BrechaRules`, liga
   `portal{spot}` com o rótulo `Brecha · {bairro}`, toca a abertura animada + `portal`, treme leve (3) se o jogador está na
   Rua da Forja e emite `ToastRequested("Uma Brecha se abriu — {bairro}\n{título} · Rank {X} · Portal na Rua da Forja")`,
   `ChatMessage` e `PortalOpened(spot, tema, rank)`.
2. `BrechaStartRequested(spot)` (ou `BrechaStarted`) para o timer dessa Brecha. `BrechaEnded(tema, false)` (fuga/morte) volta a
   contar; `BrechaEnded(tema, true)` fecha o portal (`PortalClosed(spot)`) e traz a próxima para até `R(30,50)` s.
3. Uma Casa NPC pode fechar a Brecha (`npcAt`): `NoteRequested("A {Casa} fechou a Brecha do {bairro}", "i_banner")`,
   `ChatMessage`, `PortalClosed(spot)`.
4. Timer zerado com ninguém dentro: **ruptura** → toast `"A Brecha do {bairro} ROMPEU\nMonstros invadem a Rua da Forja. A forja
   fechou e os preços subiram."`, tremor 8, `boom`, chats da Iara e da Lúcia, `PortalClosed(spot)` e
   `InvasionRequested(tema, rank)`. A Rua da Forja fica rosada até o Combate emitir `InvasionStateChanged(false)`, que
   fecha a Brecha rompida (próxima em até `R(35,55)` s).

Todo estado novo também sai em `BrechaStatusChanged(id, (int)BrechaStatus)`.

**Mural**: interagir com `board` faz a cidade emitir `ScreenRequested("board")`. Os textos da v2 estão em `BoardTexts`
(`Title`, `Subtitle`, `InvasionSuffix`, `Empty`, `Footer(n)`, `Heading(b)`, `SeasonLine(b)`, `Status(b, rastreada)`,
`Timer(b)`); boatos em `db.Social.Rumors`.

## Luz (`LightEmitter`, `CityLife.Instance.Lighting`)

Escuridão e furos de luz da v2 numa passada de tela cheia (`city_lighting.gdshader`). Para algo brilhar, ponha um
`LightEmitter` como filho (segue o pai):

```csharp
AddChild(new LightEmitter { Radius = 100, Kind = "m", Position = new Vector2(0, -30) }); // portal de saída da v2
```

`Kind` = `a` âmbar, `f` fogo, `m` magenta, `c` ciano, `g` verde; `Intensity` 0–1 apaga sem tirar do registro; `GlowBoost`
multiplica só o glow colorido (o portal usa 2,2 para dominar a noite). Até 64 luzes visíveis por frame (jogador primeiro,
depois os emissores, depois as do mapa). Lanterna do jogador = 72 × `Stats.Light` (40 morto).
`Lighting.CurrentDarkness`, `CurrentGrade` (cor da hora na rua), `CurrentLightCount`, `LitStreetLamps` servem para testes.

Rua da Forja (B2-D51): escuridão `CityLighting.Darkness` = base × `StreetFactor` (0,096 de dia, 0,713 de noite), cor da hora
`CityLighting.StreetGrade(clock)` multiplicada sobre o mundo e lâmpadas com poças menores. A escuridão é desenhada na
resolução base numa SubViewport (o nó visível continua se chamando `Darkness`; escondê-lo também para a SubViewport).

## Moradores (`CityLife.Instance.Townsfolk`)

`Npcs` (`TownNpc`, com `Walker` do Core) e `Hunters` (`TownHunter`, com `Brain`). `Chat(quem, selo, texto)` emite
`ChatMessage` e, se `quem` é um caçador presente no mapa e na tela, mostra o balão sobre ele (B2-D50) — útil para outras
frentes ("limpo! valeu {nome}" ao resolver a invasão, "gg {nome}…" na volta da Brecha). Falar com um NPC emite
`DialogueRequested(nome, [fala])` com as falas verbatim da v2, em ordem.

Andar (B2-D48, Core): `WalkPlanner` (A\* sobre qualquer `IWalkArea`, uma instância por thread; `TryPlan`, `IsClear`,
`RegionOf`, `Prepare`), `WalkFollower` (segue um caminho sem entrar em parede, desvia de corpos e avisa `Stuck`),
`WalkerRoute.Build` (waypoints da v2 levados para a região comum + trechos planejados) e `TownWalker`/`AmbientHunter`,
que replanejam sozinhos. As rotas são calculadas numa thread sobre a área andável do mapa carregado, então valem para a
máscara nova de 4 px da Rua da Forja sem mudar nada. O diretor passa a cada passo as posições do jogador e dos moradores
(`TownNpc.Step(area, dt, random, corpos, índice, planner)`), sem alocar.

## Sinais novos (bloco "Cidade viva" do `EventBus`)

| Sinal | Quem emite → quem escuta |
|---|---|
| `ChatMessage(who, rank, text)` | Cidade → Interface (log de chat opcional; o balão já aparece no mundo) |
| `NoteRequested(text, icon)` | Cidade → Interface (nota lateral da v2) |
| `BrechaStatusChanged(id, status)` | Cidade → Interface (Mural, lista de Brechas do HUD) |

## Smoke

A simulação da cidade começa **desligada** no smoke (`SimulationEnabled`), para portais, NPCs e música não aparecerem nos
cenários das outras frentes. Para usar: `CityLife.Instance.SetSimulationEnabled(true)`, `TimeScale` (só no smoke),
`Brechas.OpenSoon(0)`; desligue no fim. Cenários em `src/Testing/Scenarios/CityScenarios.cs`.
