# API de dados e estado do jogador

Guia curto para as frentes que consomem dados (`src/Data/*.json`) e o estado do jogador. Regras e números vêm da
spec [`prototype-v2/02-dados-combate-ui.md`](prototype-v2/02-dados-combate-ui.md); cada classe cita a origem no protótipo.

## Dados: `Database` (autoload) e `GameDatabase` (Core)

- `Database.Db` (Godot, `src/Autoload/Database.cs`) carrega e valida todos os JSON de `res://src/Data` no boot, antes do
  `GameManager`. Dados inválidos (campo errado, id inexistente) derrubam o boot com a lista de problemas no log.
- `GameDatabase.Load(Func<string,string> readFile)` (Core, sem Godot) é o mesmo carregamento para testes e servidor.
- Um arquivo por domínio: `palette`, `ranks`, `seasons`, `themes`, `weapons`, `elements`, `materials`, `gear`,
  `recipes`, `shops`, `monsters`, `ecos`, `ultimates`, `classes`, `npcs`, `oficios`, `social`, `quests`, `rules`.
  `maps.json` é da frente de mundo e não passa pelo `GameDatabase`.
- Acesso por id: `db.Monsters["wolf"]`, `db.Gear["capuz"]`, `db.Materials`, `db.Weapons`, `db.Elements`, `db.Ecos`,
  `db.Themes`, `db.Classes`, `db.Ultimates[classe]`, `db.Services["inn_potion"]`, `db.Rarities`.
  Listas em ordem: `db.Ranks` (índice = `rankIdx`), `db.WeaponOrder` (hotbar), `db.GearSlots`, `db.ThemeList`,
  `db.Recipes`, `db.Shops.Relics`, `db.Quests`, `db.ClassList`, `db.Npcs.Walkers`, `db.Oficios`, `db.Social`.
- Constantes de regra: `db.Rules` (`start`, `economy`, `stats`, `combat`, `potion`, `death`, `lantern`, `scaling`,
  `loot`, `brecha`). Falas: `db.Npcs.Dialogue["bras"]["invasion"][0]` (placeholders `{name}`, `{class}`… ficam para a UI).

## Regras puras (`Vespera.Core.Rules`, `Vespera.Core.Combat`)

Todas recebem `GameDatabase` (ou o pedaço dele) e, quando sorteiam, um `IRandom` (`SeededRandom` em produção).
Arredondamento herdado do JS: `JsMath.Round` (D7).

| Classe | Para quê |
|---|---|
| `Progression` (`db.Progression`) | `RankIndex/RankOf/NextRank(xp)`, `LevelOf(xp)` → `(Level, Current, Need)`, `BaseMaxHp(xp)`, `Gain(xp, n)` |
| `PlayerStatsCalculator` | `Compute(save)` → `PlayerStats(Hp, Atk, AtkBonus, Def, Crit, Speed, Light, Essence, Regen, Element)`; `WeaponDamage(arma, nível)` |
| `PriceCalculator` | `PriceMult(invasão)`, `CostLumes(base, invasão)`, `CanPay/TryPay(save, cost, invasão)`, `Describe` |
| `UpgradeCosts` | `WeaponUpgrade(arma, nível)`, `WeaponUnlock`, `Infusion(elemento)`, `EcoTuning(nívelEco)`, `EcoCost(eco, nível)` |
| `MaterialSale` | `UsedMaterials`, `QuoteScraps(mats)`, `ShardSaleLumes(n)` |
| `DamageCalculator` | `HitSide`, `ToMonster(base, ctx, rng)`, `ToPlayer(dmg, def, rng)`, `PartDamage`, `SelectPart`, `IsPartBroken` (D6), `KnockbackScale`, `EssenceAfterHit` |
| `MonsterScaling` | `HpMult(rk)`, `DamageMult(rk, estação)`, `Scale(def, rk, estação)` (estação só dentro da Brecha) |
| `LootTables` | `RollGear`, `KillDrops`, `EcoChance/RollEco`, `PartDrops`, `ChestCount`, `RollPurpleChest`, `ChestDrops`, `PropDrop` → `LootDrop(Kind, Id, Amount)` |
| `SeasonCalendar` | `SeasonOf(dia)`, `Advance`, `IsNewDayDue`, `IsVespera`, `FormatClock`, `FormatTimer`, `IsWalkerPresent` |
| `BrechaRules` | `ChooseTheme`, `RollRank`, `RollTimer`, `RollNpcCloseAt`, `InitialOpenDelay/RollOpenDelay`, `CanEnter`, `GenerateWaves`, `PickMob`, `Invasion(rk)`, `BossBonusLumes`, `Title` |
| `LanternRules` | `PenalizedHp/Light`, `RollRescueDelay`, `OdeteRescueDelay`, `RescueOnLoadDelay`, `DeliveryReward`, `RollFoundLantern`, `IsLanternHere`, `MergeBag` |
| `EquipmentRules` | `LockReason(gear, classe, selo)` (texto da v2), `MinRankIndex`, `ClassesLabel`, `CanWield` |
| `QuestRules` | `Current`, `IsComplete`, `ProgressText`, `RewardText`, `Complete`; contadores em `QuestCounters` |

## Estado do jogador: `GameState` (Core) via `GameManager.Instance.State`

`GameState` = `SaveData` (persistido, formato v2) + sessão (HP atual, `Invasion`, recarga da poção). Toda mudança passa
por ele e dispara um evento C#; o `GameManager` repassa para o `EventBus`.

- Leitura: `Save`, `Stats`, `Hp`, `MaxHp`, `Rank`, `RankIndex`, `RankDef`, `Level`, `Lumes`, `Shards`, `Season`,
  `HasLostLantern`, `CurrentQuest`, `Random`, e as regras (`Prices`, `Upgrades`, `EquipmentRules`, `LanternRules`,
  `QuestRules`, `Sale`, `Calendar`, `Brechas`, `Progression`).
- XP: `GainXp(n)` (HP atual recebe o aumento do máximo).
- Lumes/materiais: `AddLumes`, `TrySpendLumes` (exato), `CostLumes(base)`, `CanPay/TryPay(Cost)` (preço de loja,
  com ×0.9 e ×1.35 em invasão), `DescribeCost`, `AddMaterial`, `TryRemoveMaterial`, `MaterialCount`, `MergeBag(bolsa)`.
- Equipamento: `AddGear`, `EquipLockReason`, `Equip(índice)`/`EquipById` (devolvem o motivo do bloqueio ou null),
  `Unequip(slot)`, `Dismantle(índice)`.
- Lojas: `TryUpgradeWeapon`, `TryUnlockWeapon` (outra classe = `Locked`), `TryForge(receita)`, `TryBuyRelic`,
  `TryBuyService(serviço)`, `ScrapQuote`, `SellScraps`, `SellShards` → `PurchaseResult`.
- Combate/consumo: `Damage(dano final)` (true = caiu), `Heal`, `RestoreFullHealth`, `TryUsePotion(agora, out cura)`.
- Ecos/infusões: `AbsorbEco`, `EquipEco`, `EcoCost`, `TryTuneEco`, `TryBuyInfusion`, `SetActiveInfusion`.
- Lanterna e morte: `DieInBrecha(LostLantern)`, `DieInCity()`, `RecoverLantern()`, `DeliverLanterns()`.
- Tempo: `AdvanceClock(segundos)` (true = dia novo), `NewDay()`.
- Missões/Brechas: `CountQuest(QuestCounters.X)`, `TryCompleteQuest()`, `RecordBossDefeated(tema)`, `RecordInvasionResolved()`.
- Eventos C#: `XpGained`, `RankChanged`, `LevelChanged`, `LumesChanged`, `MaterialChanged`, `InventoryChanged`,
  `EquipmentChanged`, `WeaponLevelChanged`, `PotionsChanged`, `HealthChanged`, `StatsChanged`, `LanternChanged`,
  `DayChanged`, `EcoChanged`, `InfusionChanged`, `QuestCompleted`.

Fica fora do `GameState` (é de quem roda a cena): Brechas abertas e timers, bolsa da Brecha em curso (`RunBag`),
`rescueAt`, cooldowns de combate, Essência e Energia. O save **não** guarda mapa nem posição (Continuar começa na Guilda).

## `GameManager` e `EventBus`

- `GameManager.Instance`: `State`, `Db`, `LoadGame()`, `NewGame(nome, classe)`, `SaveGame()` e os atalhos da fundação
  (`Rank`, `TotalXp`, `ProgressInRank`, `MaxHp`, `CurrentHp`, `Fragments` = Estilhaços, `Lumes`, `WeaponLevel`, `Day`,
  `AddXp`, `AddFragments`, `TrySpendFragments`, `DamagePlayer`, `HealPlayer`, `RestoreFullHealth`, `AdvanceDay`).
  Autosave ao subir de Selo; os demais pontos de save da v2 (spec 01 §1.11) são de quem executa a ação.
- Sinais mantidos: `PlayerDamaged`, `PlayerHealed`, `PlayerDied`, `XpGained`, `RankChanged`, `FragmentsChanged`, `WeaponUpgraded`.
- Sinais novos: `GameLoaded` (releia tudo), `LevelChanged`, `LumesChanged`, `MaterialChanged`, `InventoryChanged`,
  `EquipmentChanged`, `PotionsChanged`, `PlayerStatsChanged`, `LanternStateChanged`, `DayChanged`, `EcoChanged`, `QuestCompleted`.
- Save: `SaveService` lê qualquer versão (v1 da fundação é migrado, D19), saneia ids que sumiram dos dados e grava v2.
