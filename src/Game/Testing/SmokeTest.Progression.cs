using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Core.Advancement;
using Vespera.Core.Ranks;
using Vespera.Game.Boot;
using Vespera.Game.Hunter;
using Vespera.Game.UI;

namespace Vespera.Game.Testing;

/// <summary>
/// Roteiro da progressão (<c>-- --smoke-test --progression [--shots=pasta]</c>): cria um save, dá loot, forja a arma pela
/// <see cref="ForgeScreen"/>, veste peças no inventário, abre a ficha, derruba Guardiões até a celebração de rank aparecer (com e sem
/// troca de estágio), grava, relê o save e confere a regra da Lanterna.
/// </summary>
public partial class SmokeTest
{
    public const string ProgressionFlag = "--progression";

    private async Task RunProgressionSmoke()
    {
        await WaitFor(() => _router.Current is TitleScreen && !_router.IsBusy, "título");
        var data = SaveService.StartNew(1) ?? throw new InvalidOperationException("não criou o save");
        GameSession.Begin(1, data);
        Progression.Reset();
        Progression.UseSeed(42);
        Progression.ShowRankUpOverlay = false;

        var loot = 0;
        var ranks = 0;
        var stages = 0;
        Progression.LootGranted += _ => loot++;
        Progression.RankChanged += _ => ranks++;
        Progression.StageChanged += _ => stages++;

        await ProgressionLootAndForge(data);
        await ProgressionInventoryAndCharacter();
        await ProgressionRankUps(() => (ranks, stages));
        await ProgressionLargeText();
        Check(loot >= 6, $"LootGranted deveria disparar a cada saque (foram {loot})");
        await ProgressionSaveAndLantern();
        Progression.ShowRankUpOverlay = true;
    }

    private async Task ProgressionLootAndForge(Core.Save.SaveData data)
    {
        Check(Progression.Rank == Rank.E && Progression.Stage == 0, "o caçador novo deveria ser rank E, estágio 0");
        Progression.AddXp(150);
        Check(Progression.Rank == Rank.E, "XP sem o Guardião não deveria subir de rank");
        for (var i = 0; i < 6; i++)
        {
            Progression.GrantLoot(Rank.E, i % 2 == 0 ? Core.Advancement.LootSource.Chest : Core.Advancement.LootSource.Monster);
        }

        Check(Progression.State.Fragmentos > 0, "o saque deveria render Fragmentos");
        Check(Progression.State.Materials.Count > 0, "o saque deveria render materiais");

        // Garante o custo do primeiro upgrade sem depender da sorte do sorteio.
        Progression.State.Fragmentos = Math.Max(Progression.State.Fragmentos, 40);
        Progression.State.Materials["gel_de_lodo"] = Math.Max(Progression.State.MaterialCount("gel_de_lodo"), 6);
        Progression.State.Materials["veu_de_espectro"] = Math.Max(Progression.State.MaterialCount("veu_de_espectro"), 3);

        ForgeScreen.Open();
        await Frames(6);
        Check(_router.OverlayDepth == 1 && GetTree().Paused, "a forja deveria abrir sobre o jogo e pausar a árvore");
        var screen = FindOverlay<ForgeScreen>();
        Check(screen is not null, "ForgeScreen não está na árvore");
        if (screen is null)
        {
            return;
        }

        Check(screen.LastCheck?.CanUpgrade == true, "a forja deveria poder forjar com os recursos dados");
        await Shot("p01_forja");
        await Tap(Key.Enter);
        await Seconds(0.25);
        await Shot("p02_forja_acendendo", 0);
        Check(data.Hunter.WeaponLevels["sword"] == 2, $"a arma deveria estar no nível 2, está em {data.Hunter.WeaponLevels["sword"]}");
        await Seconds(1.2);
        await Shot("p03_forja_nivel2", 0);
        Check(screen.LastCheck?.Block == ForgeBlock.RankTooLow, "no rank E o nível 2 é o teto da arma");
        await Tap(Key.Enter);
        Check(data.Hunter.WeaponLevels["sword"] == 2, "forjar sem poder não deveria mexer na arma");
        await Shot("p04_forja_bloqueada", 0.3);
        await Tap(Key.Escape);
        await Frames(3);
        Check(_router.OverlayDepth == 0 && !GetTree().Paused, "Esc deveria fechar a forja e despausar");
    }

    private async Task ProgressionInventoryAndCharacter()
    {
        var set = Progression.Catalog.Sets["conjunto_lodo_mae"];
        var engine = Progression.Engine;
        foreach (var piece in set.Pieces)
        {
            var item = LootRoller.NewSetPiece(set, piece);
            engine.Apply(new LootResult(Rank.E, Core.Advancement.LootSource.Guardian("lodo_mae"), [new LootEntry(LootEntryKind.Gear, piece.Id, piece.Name, 1, Rank.E, false, true, item)], 0));
        }

        engine.Apply(new LootResult(Rank.E, Core.Advancement.LootSource.Chest, [engine.Roller.RollRareGear(Rank.E, engine.Random)], 0));
        Progression.State.Echoes.Add("eco_viscoso");

        InventoryScreen.Open();
        await Frames(8);
        var inventory = FindOverlay<InventoryScreen>();
        Check(inventory is not null && _router.OverlayDepth == 1, "o inventário deveria abrir");
        if (inventory is null)
        {
            return;
        }

        await Shot("p05_inventario");
        var capuz = inventory.Rows.First(r => r.NameText == "Capuz de Louça Trincada");
        capuz.GrabFocus();
        await Frames(4);
        Check(inventory.DetailText.Contains("Conjunto da Lodo-Mãe", StringComparison.Ordinal), "o detalhe da peça deveria citar o conjunto");
        await Tap(Key.Enter);
        await Frames(6);
        Check(Progression.Equipment.In("cabeca")?.Name == "Capuz de Louça Trincada", "Enter na peça deveria vesti-la");
        foreach (var name in new[] { "Peitoral de Talheres", "Luvas de Gel Seco" })
        {
            var row = inventory.Rows.First(r => r.NameText == name);
            row.GrabFocus();
            await Frames(2);
            await Tap(Key.Enter);
            await Frames(6);
        }

        Check(Progression.Equipment.Summary().SetBonuses.Hp == 20, "com 3 peças o bônus de 2 deveria estar ativo");
        inventory.RowFor("echo:0")?.GrabFocus();
        await Frames(3);
        await Tap(Key.Enter);
        await Frames(4);
        Check(Progression.Engine.EchoIn(0) == "eco_viscoso", "confirmar no slot de Eco deveria equipar o Eco Viscoso");
        inventory.RowFor("slot:peito")?.GrabFocus();
        await Frames(3);
        await Shot("p06_inventario_equipado");
        await Tap(Key.Escape);
        await Frames(3);

        CharacterScreen.Open();
        await Frames(8);
        var character = FindOverlay<CharacterScreen>();
        Check(character is not null, "a ficha deveria abrir");
        Check(character?.RequirementTexts.Any(t => t.Contains("Derrote o Guardião do rank E", StringComparison.Ordinal)) == true, "a ficha deveria pedir o Guardião do rank E");
        await Shot("p07_personagem");
        await Tap(Key.Escape);
        await Frames(3);
        Check(_router.OverlayDepth == 0, "a ficha deveria fechar");
    }

    private async Task ProgressionRankUps(Func<(int Ranks, int Stages)> counts)
    {
        Progression.ShowRankUpOverlay = true;
        Progression.RecordGuardianKill("lodo_mae");
        await WaitFor(() => RankUpScreen.Active is { Revealed: true }, "celebração do rank D");
        var overlay = RankUpScreen.Active;
        Check(overlay is not null && overlay.Change.To == Rank.D, "a celebração deveria ser do rank D");
        Check(overlay?.StageText.Length == 0, "E para D não troca de estágio");
        await Shot("p08_rankup_d");
        await Tap(Key.Enter);
        await Frames(4);
        Check(RankUpScreen.Active is null && !GetTree().Paused, "Continuar deveria fechar a celebração e despausar");

        Progression.AddXp(400);
        Progression.RecordGuardianKill("macieira_oca");
        await WaitFor(() => RankUpScreen.Active is { Revealed: true }, "celebração do rank C");
        Check(RankUpScreen.Active?.StageText.Contains("Caçador", StringComparison.Ordinal) == true, "D para C deveria anunciar o estágio Caçador");
        await Shot("p09_rankup_estagio");
        await Tap(Key.Enter);
        await Frames(4);
        Check(Progression.Rank == Rank.C && Progression.Stage == 1, $"deveria ser rank C estágio 1, foi {Progression.Rank}/{Progression.Stage}");
        var (ranks, stages) = counts();
        Check(ranks == 2 && stages == 1, $"eventos: 2 RankChanged e 1 StageChanged esperados, vieram {ranks} e {stages}");
        Progression.ShowRankUpOverlay = false;
    }

    /// <summary>Repete as telas com o texto grande ligado para conferir se nada estoura.</summary>
    private async Task ProgressionLargeText()
    {
        GameSettings.Current.TextSize = GameSettings.TextLarge;
        GameSettings.Commit(GetTree());
        await Frames(4);
        InventoryScreen.Open();
        await Frames(8);
        await Shot("p10_inventario_texto_grande");
        await Tap(Key.Escape);
        await Frames(3);
        ForgeScreen.Open();
        await Frames(8);
        await Shot("p11_forja_texto_grande");
        await Tap(Key.Escape);
        await Frames(3);
        CharacterScreen.Open();
        await Frames(8);
        await Shot("p12_personagem_texto_grande");
        await Tap(Key.Escape);
        await Frames(3);
        GameSettings.Current.TextSize = GameSettings.TextNormal;
        GameSettings.Commit(GetTree());
        Check(_router.OverlayDepth == 0, "as telas deveriam ter fechado");
    }

    private async Task ProgressionSaveAndLantern()
    {
        Progression.TryPassExam(Rank.C);
        Progression.BeginRun("bairro_das_forjas");
        Progression.GrantLoot(Rank.C, Core.Advancement.LootSource.Chest);
        var held = Progression.State.RunFragmentos;
        Check(held > 0, "a caçada deveria guardar os Fragmentos");
        Progression.EndRun(survived: false);
        Check(Progression.Engine.LanternFragmentos("bairro_das_forjas") == held, "morrer deveria deixar os Fragmentos na Lanterna");

        var level = Progression.State.WeaponLevels["sword"];
        var loaded = SaveService.Load(1);
        Check(loaded is not null, "o save deveria ler de volta");
        if (loaded is null)
        {
            return;
        }

        GameSession.Begin(1, loaded);
        Progression.Reset();
        Check(Progression.Rank == Rank.C, $"depois de recarregar o rank deveria seguir C, foi {Progression.Rank}");
        Check(Progression.State.WeaponLevels["sword"] == level, "o nível da arma deveria persistir");
        Check(Progression.Equipment.In("cabeca") is not null, "a peça vestida deveria persistir");
        Check(Progression.Engine.EchoIn(0) == "eco_viscoso", "o Eco equipado deveria persistir");
        Check(Progression.Engine.LanternFragmentos("bairro_das_forjas") == held, "a Lanterna perdida deveria persistir");
        Check(Progression.BeginRun("bairro_das_forjas") == held, "a próxima Brecha do bairro deveria devolver os Fragmentos");
        Progression.EndRun(survived: true);
        await Frames(2);
    }

    private T? FindOverlay<T>()
        where T : Control
    {
        var stack = new System.Collections.Generic.Stack<Node>();
        stack.Push(GetTree().Root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is T found && found.IsVisibleInTree())
            {
                return found;
            }

            foreach (var child in node.GetChildren())
            {
                stack.Push(child);
            }
        }

        return null;
    }
}
