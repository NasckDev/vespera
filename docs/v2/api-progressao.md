# API de progressão (v2)

Serviço estático `Vespera.Game.Hunter.Progression` (arquivos em `src/Game/Progression/`). Liga o motor puro do Core
(`src/Core/Advancement/`, namespace `Vespera.Core.Advancement`) ao save da sessão (`GameSession.Data.Hunter`).
Precisa de sessão ativa (`GameSession.Begin`). Salva sozinho depois de forja, equipar, rank, Guardião, exame e fim de caçada
(`Progression.AutosaveEnabled`). Dados de balanceamento: `data/progressao/*.json`.

## Chamar de qualquer lugar

```csharp
using Vespera.Core.Advancement; using Vespera.Core.Ranks; using Vespera.Game.Hunter;

Progression.AddXp(25);                                        // XpResult (RankChange? se subiu)
Progression.GrantLoot(Rank.E, LootSource.Monster);            // Monster | Part | Chest | LootSource.Guardian(id)
Progression.RecordGuardianKill("lodo_mae");                   // conquista + saque garantido + Eco + 30% peça do conjunto
Progression.TryPassExam(Rank.C);                              // exames só em C, A, S, SSS (ExamStatus)
Progression.BeginRun("bairro_das_forjas");                    // devolve Fragmentos recuperados da Lanterna do bairro
Progression.EndRun(survived: true);                           // vivo: Fragmentos -> carteira; morto: ficam na Lanterna do bairro

Progression.Forge.CanUpgrade();  Progression.Forge.Upgrade(); // ForgeCheck / ForgeResult (custo, antes/depois, o que falta)
Progression.Equipment.Equip(itemId); .Unequip("peito"); .EquipEcho(0, "eco_viscoso"); .Summary();

ForgeScreen.Open();        // forja (a história chama ao falar com a Odete)
InventoryScreen.Open();    // inventário e equipamento
CharacterScreen.Open();    // ficha do caçador (rank, XP, o que falta)
```

Para o combate: `Progression.Engine.WeaponStats` (dano, crítico, ritmo, quebra de partes), `Engine.Equipment().Total` (`StatBlock`:
Hp, Defense, CritPct, SpeedPct, FragmentosPct), `Engine.MaxHpBonus` (rank + peças, soma ao HP base), `Progression.Rank/Stage`.

## Eventos (C#)

`RankChanged(RankChange)`, `StageChanged(RankChange)` (fronteira de estágio visual), `LootGranted(LootGrant)`,
`GuardianDefeated(GuardianKillResult)`, `Changed()`. A celebração (`RankUpScreen`) abre sozinha em `RankChanged`
(`Progression.ShowRankUpOverlay = false` para controlar à mão).

## Regras

- Rank sobe por XP **e** conquista: para sair do rank R, XP do seguinte + Guardião de R + exame de R (C, A, S, SSS).
- Loot de Brecha de rank R: 80% material R, 15% R+1, 5% peça rara R com afixo. Guardião: material garantido + 30% de peça do conjunto.
- Arma: 2 níveis por rank (máx. 18), teto de nível acompanha o rank. Custo: Fragmentos + 2 materiais do rank (+ material de Guardião na troca de degrau).
- Lanterna: Fragmentos da caçada ficam na Brecha ao morrer; voltam ao entrar de novo numa Brecha do **mesmo bairro**. Peças e materiais nunca se perdem.
- Estágios visuais: 0 E-D, 1 C-B, 2 A-S, 3 SS-SSS, 4 Alvor (`Progression.Stage`).
- Save v3: bloco `hunter` (migração 2 -> 3 só aproveita XP e Lumes) e bloco `story` (a história; um save v2 que já tinha `story` o mantém).

## Ligações feitas na integração do Ato I

- Combate/Brecha (`src/Game/World/Rift/RiftLoot.cs`, `RiftSession`, `Level.Rift.cs`): entrar no portal chama `BeginRun(brecha.neighborhoodId)`
  (Fragmentos da Lanterna do bairro voltam já na entrada, com aviso); sair pela fenda depois do Guardião, `EndRun(true)`; cair,
  `EndRun(false)`. Cada morte dá `AddXp` (`xp` em `src/Data/brecha_esgotos.json`) e `GrantLoot(E, Monster)`; baú `Chest`; núcleo
  `Part`; Lodo-Mãe `RecordGuardianKill("lodo_mae")` + flag de história `GuardianDefeated:lodo_mae`. Os cristais de Fragmento no chão
  são só a imagem do que a progressão já entregou. A Lanterna perdida não reduz a vida (regra da v2: só os Fragmentos ficam).
- Uma caçada completa dá 129 XP (rank D pede 120) e a Lodo-Mãe sempre deixa 4 Gel de Lodo (`spoils` em `guardians.json`): a primeira
  caçada sobe E -> D (a celebração abre ao vencer o Guardião) e paga o primeiro upgrade da Lâmina.
- Player: HP, dano, defesa e crítico vêm de `Progression` (`src/Game/Combat/HunterStats.cs`). HUD: Fragmentos da caçada/carteira e selo do rank.
- História: a Odete abre `ForgeScreen.Open()` direto (o gancho por reflexão saiu).

## Ganchos ainda em aberto

- Eco Viscoso (`eco_viscoso`) e demais Ecos: só estão no inventário; o efeito em combate é do dono do Player (`Engine.EchoIn(slot)`).
- Exames: a sala é montada com `AdvancementCatalog.Exams` (objetivos, tempo, golpes máx.); ao vencer, `TryPassExam`.
