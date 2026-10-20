using System.Threading.Tasks;
using Vespera.Autoload;
using Vespera.Core.Ranks;
using Vespera.Core.Save;
using Vespera.Core.State;
using Vespera.UI;

namespace Vespera.Testing;

/// <summary>Cenários do estado do jogador: progressão (XP → Nível e Selo), economia com arredondamento JS e save → load.</summary>
public partial class SmokeTest
{
    // HP base no limiar de cada Selo (spec 02 §1.3).
    private static readonly int[] HpAtSeal = [100, 124, 148, 178, 202, 232, 256, 286, 310, 334];

    [SmokeScenario("progressão: XP sobe nível e selo", order: 10)]
    private async Task ProgressionRaisesLevelAndSeal()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var game = GameManager.Instance;
        game.NewGame("Rui", "sword");
        await NextPhysicsFrame();
        var hud = Find<Hud>() ?? throw new SmokeFailure("HUD não encontrado na cidade");
        Require(hud.RankSeal.Rank == Rank.E && game.MaxHp == 100, $"jogo novo deveria ser Selo E com 100 HP (está {hud.RankSeal.Rank}, {game.MaxHp})");
        await Shot("selo-e");

        var levelUps = 0;
        var rankUps = 0;
        void OnLevel(int previous, int current) => levelUps++;
        void OnRank(Rank previous, Rank current) => rankUps++;
        var bus = EventBus.Instance;
        bus.LevelChanged += OnLevel;
        bus.RankChanged += OnRank;
        try
        {
            game.AddXp(40);
            await NextPhysicsFrame();
            Require(game.State.Level.Level == 2 && game.Rank == Rank.E, "40 XP deveria dar Nv. 2 e manter o Selo E");
            Require(game.MaxHp == 106 && game.CurrentHp == 106, $"Nv. 2 deveria dar 106 HP (está {game.CurrentHp}/{game.MaxHp})");

            game.AddXp(60);
            await NextPhysicsFrame();
            Require(game.Rank == Rank.D && game.State.Level.Level == 3, $"100 XP deveria dar Selo D e Nv. 3 (está {game.Rank}, Nv. {game.State.Level.Level})");
            Require(game.MaxHp == 124 && game.CurrentHp == 124, $"Selo D/Nv. 3 deveria dar 124 HP (está {game.CurrentHp}/{game.MaxHp})");
            Require(hud.RankSeal.Rank == Rank.D, "o selo do HUD não mudou para D");
            Require(hud.LevelLabel.Text == "Nv. 3 · Selo D", $"rótulo de nível errado: '{hud.LevelLabel.Text}'");
            Require(levelUps == 2 && rankUps == 1, $"sinais: esperado 2 LevelChanged e 1 RankChanged, veio {levelUps} e {rankUps}");
            await Shot("progressao-selo-d");

            var ranks = Database.Db.Ranks;
            for (var i = (int)game.Rank + 1; i < ranks.Count; i++)
            {
                var missing = ranks[i].Xp - game.TotalXp;
                if (missing > 0)
                {
                    game.AddXp(missing);
                }

                await NextPhysicsFrame();
                Require(hud.RankSeal.Rank == (Rank)i, $"com {ranks[i].Xp} XP o HUD deveria mostrar o Selo {ranks[i].Id}, mostra {hud.RankSeal.Rank}");
                Require(game.MaxHp == HpAtSeal[i], $"Selo {ranks[i].Id}: HP máximo {game.MaxHp}, esperado {HpAtSeal[i]}");
                await Shot($"selo-{ranks[i].Id.ToLowerInvariant()}");
            }

            Require(game.State.Level.Level == 22, $"Alvor (7500 XP) deveria ser Nv. 22, está {game.State.Level.Level}");
            Require(rankUps == 9, $"deveria ter subido 9 Selos, subiu {rankUps}");
        }
        finally
        {
            bus.LevelChanged -= OnLevel;
            bus.RankChanged -= OnRank;
            game.NewGame("Rui", "sword");
        }
    }

    [SmokeScenario("economia: comprar e pagar com arredondamento JS", order: 11)]
    private async Task EconomyUsesJsRounding()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var game = GameManager.Instance;
        game.NewGame("Rui", "sword");
        await NextPhysicsFrame();
        var state = game.State;
        var db = Database.Db;
        var hud = Find<Hud>() ?? throw new SmokeFailure("HUD não encontrado na cidade");
        var potion = db.Services["inn_potion"];

        try
        {
            Require(state.CostLumes(30) == 27, $"poção da Lúcia (30) deveria custar 27, custa {state.CostLumes(30)}");
            Require(state.TryBuyService(potion) == PurchaseResult.Ok, "não comprou a poção com 60 Lumes");
            await NextPhysicsFrame();
            Require(game.Lumes == 33 && state.Save.Potions == 4, $"depois da poção: {game.Lumes} Lumes e {state.Save.Potions} poções");
            Require(hud.LumesLabel.Text == "Lumes: 33", $"HUD de Lumes não atualizou: '{hud.LumesLabel.Text}'");

            state.Invasion = true;
            Require(state.CostLumes(30) == 41, $"em invasão 30 Lumes deveria custar 41 (JS), custa {state.CostLumes(30)}");
            Require(state.CostLumes(22) == 30, $"em invasão 22 Lumes deveria custar 30, custa {state.CostLumes(22)}");
            state.AddLumes(8);
            Require(state.TryBuyService(potion) == PurchaseResult.Ok && game.Lumes == 0, $"não pagou 41 exatos (sobrou {game.Lumes})");
            Require(state.TryBuyService(potion) == PurchaseResult.CannotAfford && game.Lumes == 0, "comprou sem Lumes");
            state.Invasion = false;

            var weaponLevel = 0;
            void OnUpgrade(int level) => weaponLevel = level;
            EventBus.Instance.WeaponUpgraded += OnUpgrade;
            state.AddLumes(36);
            state.AddMaterial("presa", 1);
            var upgraded = state.TryUpgradeWeapon("sword");
            EventBus.Instance.WeaponUpgraded -= OnUpgrade;
            Require(upgraded == PurchaseResult.Ok && game.Lumes == 0, $"upgrade +1→+2 deveria custar 36 Lumes + 1 Presa ({upgraded}, sobrou {game.Lumes})");
            Require(weaponLevel == 2 && state.Stats.Atk == 15, $"Lâmina +2 deveria ter 15 de ataque (nível {weaponLevel}, ataque {state.Stats.Atk})");

            game.AddFragments(5);
            Require(state.SellShards() == 20, "5 Estilhaços deveriam render 20 Lumes no Brás");
            await NextPhysicsFrame();
            Require(hud.FragmentsLabel.Text == "Estilhaços: 0" && hud.LumesLabel.Text == "Lumes: 20", $"HUD: '{hud.LumesLabel.Text}', '{hud.FragmentsLabel.Text}'");
            await Shot("economia-hud");
        }
        finally
        {
            game.NewGame("Rui", "sword");
        }
    }

    [SmokeScenario("save → load roundtrip", order: 12)]
    private async Task SaveLoadRoundTrip()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var game = GameManager.Instance;
        game.NewGame("Lia", "bow");
        var state = game.State;
        state.GainXp(300);
        state.AddLumes(55);
        state.AddMaterial("presa", 3);
        Require(state.EquipById("capuz") is null, "não equipou o Capuz");
        state.AbsorbEco("uivo");
        state.AddPotions(2);
        state.AdvanceClock(100);
        state.CountQuest("dummy", 3);
        state.DieInBrecha(new LostLantern
        {
            BrechaId = 1, Bairro = "Mercado de Peixes", ThemeId = "fish", MapId = "fish2", Room = "theme", X = 830, Y = 870,
            Bag = new RunBag { Lumes = 12, Materials = { ["isca"] = 2 } },
        });

        try
        {
            game.SaveGame();
            var saved = SaveSerializer.ToJson(state.Save);
            var expectedRank = game.Rank;
            var expectedMaxHp = game.MaxHp;

            game.NewGame("Outro", "sword");
            Require(game.TotalXp == 0, "NewGame deveria zerar o estado antes do load");
            game.LoadGame();
            await NextPhysicsFrame();

            var loaded = SaveSerializer.ToJson(game.State.Save);
            Require(loaded == saved, "o save carregado difere do salvo");
            Require(game.State.Save.Name == "Lia" && game.State.Save.ClassId == "bow", "nome/classe não voltaram");
            Require(game.State.HasLostLantern && game.MaxHp == expectedMaxHp, $"Lanterna perdida não voltou (HP máx. {game.MaxHp}, esperado {expectedMaxHp})");
            Require(game.State.Save.Equipment.TryGetValue("cabeca", out var head) && head == "capuz", "equipamento não voltou");
            var hud = Find<Hud>() ?? throw new SmokeFailure("HUD não encontrado na cidade");
            Require(hud.RankSeal.Rank == expectedRank, $"HUD não releu o Selo após o load ({hud.RankSeal.Rank})");
            Require(hud.LumesLabel.Text == $"Lumes: {game.Lumes}", $"HUD não releu os Lumes após o load ('{hud.LumesLabel.Text}')");
            await Shot("save-load");
        }
        finally
        {
            game.NewGame("Rui", "sword");
            game.SaveGame();
        }
    }
}
