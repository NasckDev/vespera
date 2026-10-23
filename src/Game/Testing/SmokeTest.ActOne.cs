using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Core.Advancement;
using Vespera.Core.Ranks;
using Vespera.Core.Story;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Game.Hunter;
using Vespera.Game.Story;
using Vespera.Game.UI;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Testing;

/// <summary>
/// Bot do Ato I inteiro (<c>--ato1</c>), num só jogo novo: prólogo (já passado pelo smoke) → fala de chegada → quadro do
/// Benício → Ravi → ponte para as Forjas → forja da Odete (Lâmina, e a forja abre pela conversa) → volta à Praça (Ravi
/// esperando junto ao portal) → portal (seletor começa na arma equipada) → 4 salas → Lodo-Mãe (rank E → D) → volta →
/// fala nova da Odete → forja com upgrade possível e feito → objetivo = Ato II (em breve). O combate usa o modo de
/// teste do bot de combate (caçador invulnerável, dano ×10) para caber no tempo; todo o resto é o fluxo real.
/// </summary>
public partial class SmokeTest
{
    public const string ActOneFlag = "--ato1";

    /// <summary>Só a captura de legibilidade dos avisos (iteração rápida da revisão visual).</summary>
    public const string WarningsQaFlag = "--qa-avisos";

    private const float RaviNearPortal = 260f;

    /// <summary>Foto do caçador parado no ponto de chegada do mapa, com a posição e a altura dele na tela no log (folha de tamanhos).</summary>
    private async Task SizeShot(Level level, string name)
    {
        await StandAt(level, level.World.SpawnPoint);
        await PhysicsFrames(20);
        var at = level.WorldToScreen(level.Player.GlobalPosition);
        Log.Info($"qa-pos {name} {at.X:0} {at.Y:0} {CameraCheck.OnScreenHeight(level):0.0}");
        await Shot($"ato1_tamanho_{name}", 0.6);
    }

    private async Task RunActOne(Level start)
    {
        Progression.AutosaveEnabled = true;

        // O roteiro base do smoke já tocou no quadro (teste de interação): a história recomeça do zero, como num jogo novo.
        var progress = StoryService.Engine.Progress;
        progress.Flags.Clear();
        progress.ObjectiveId = string.Empty;
        StoryService.Engine.BeginCampaignIfNew();
        var level = await ActOnePrologue(start);
        level = await ActOneForge(level);
        level = await ActOneBackToPraca(level);
        level = await ActOneHunt(level);
        await ActOneEpilogue(level);
    }

    private async Task<Level> ActOnePrologue(Level level)
    {
        Check(StoryService.Engine.ObjectiveId == "quadro", $"jogo novo deveria começar no objetivo do quadro, está em '{StoryService.Engine.ObjectiveId}'");
        await SizeShot(level, "praca");
        Check(Progression.Rank == Rank.E, "jogo novo deveria começar no rank E");
        level.PlayAutoplay();
        await WaitForDialogue(level, "fala de chegada");
        level.Dialogue.Complete();
        Check(level.Dialogue.IsNarration && !level.Dialogue.PortraitFrameVisible,
            "a narração (sem retrato) deveria esconder a moldura vazia e usar a largura toda");
        await Shot("ato1_dialogo_narracao", 0.5);
        await SkipDialogue(level);
        Check(StoryService.Engine.HasFlag(StoryFlags.ArrivalDone), "a chegada deveria ligar arrival_done");

        await TalkToProp(level, "mural");
        Check(StoryService.Engine.HasFlag(StoryFlags.Registered), "o quadro do Benício deveria registrar o caçador");
        Check(level.PlayAutoplay(), "depois do quadro o Ravi deveria vir desafiar");
        await WaitForDialogue(level, "desafio do Ravi");
        Check(level.Dialogue.CurrentNodeId == "r1", $"o desafio deveria abrir em 'r1', abriu em '{level.Dialogue.CurrentNodeId}'");
        await Shot("ato1_ravi_desafio", 0.5);
        await SkipDialogue(level);
        Check(StoryService.Engine.HasFlag(StoryFlags.MetRavi), "o desafio do Ravi deveria ligar met_ravi");
        await TalkToNpc(level, "ravi");
        Check(StoryService.Engine.ObjectiveId == "odete", $"depois do Ravi o objetivo deveria ser a Odete, é '{StoryService.Engine.ObjectiveId}'");

        // Sem a Lâmina o portal recusa.
        var portal = level.World.Zones.First(z => z.Kind == "brecha");
        await StandAt(level, portal.GlobalPosition + new Vector2(0, 40f * level.World.Data.ActorScale));
        await PressInWorld(Key.E);
        Check(!RiftSession.InRift && level.Hud.PanelOpen, "sem arma o portal deveria recusar com um aviso");
        await PressInWorld(Key.E);
        Log.Info("ato I: prólogo, quadro e Ravi OK; portal recusa sem arma");
        return level;
    }

    private async Task<Level> ActOneForge(Level level)
    {
        level = await Travel(level, z => z.Target == "forjas", new Vector2(24, 0), "Level_forjas");
        await SizeShot(level, "forjas");
        level = await Travel(level, z => z.Target == "forja_interior", new Vector2(0, 40 * level.World.Data.ActorScale), "Level_forja_interior");
        await SizeShot(level, "forja_interior");
        await TalkToNpc(level, "odete", shot: "ato1_odete");
        Check(StoryService.Engine.HasFlag(StoryFlags.StarterBlade), "Odete deveria entregar a Lâmina");
        Check(StoryService.Engine.ObjectiveId == "brecha", $"depois da Lâmina o objetivo deveria ser a Brecha, é '{StoryService.Engine.ObjectiveId}'");

        await TalkToNpc(level, "odete");
        await WaitFor(() => FindOverlay<ForgeScreen>() is not null, "forja aberta pela conversa com a Odete");
        Check(GetTree().Paused, "a forja deveria pausar o jogo");
        await Shot("ato1_forja_antes");
        await Tap(Key.Escape);
        await Frames(4);
        Check(FindOverlay<ForgeScreen>() is null && !GetTree().Paused, "Esc deveria fechar a forja");
        await Seconds(0.4);
        Log.Info("ato I: Odete entregou a Lâmina e abriu a forja");
        return level;
    }

    private async Task<Level> ActOneBackToPraca(Level level)
    {
        var s = level.World.Data.ActorScale;
        level = await Travel(level, z => z.Target == "forjas", new Vector2(0, -30 * s), "Level_forjas");
        level = await Travel(level, z => z.Target == "praca", new Vector2(-24, 0), "Level_praca");
        var portal = level.World.Zones.First(z => z.Kind == "brecha").GlobalPosition;
        var ravi = level.World.Npcs.Where(n => n.NpcId == "ravi").ToList();
        Check(ravi.Count == 1, $"deveria haver um só Ravi na Praça, há {ravi.Count}");
        Check(level.World.Npcs.Count(n => n.NpcId == "lis") == 1, "deveria haver uma só Lis na Praça");
        var distance = ravi.Count == 0 ? float.MaxValue : ravi[0].GlobalPosition.DistanceTo(portal);
        Check(distance <= RaviNearPortal, $"depois da Lâmina o Ravi deveria esperar junto ao portal (a {distance:0} px dele)");
        Log.Info($"ato I: de volta à Praça; Ravi a {distance:0} px do portal");
        await StandAt(level, level.World.ClampToFloor(portal + new Vector2(-0.6f * level.World.Data.HumanHeight, 1.6f * level.World.Data.HumanHeight)));
        await PhysicsFrames(20);
        await Shot("ato1_ravi_portal", 0.6);
        return level;
    }

    private async Task<Level> ActOneHunt(Level level)
    {
        var shots = _shotsDir is not null && DisplayServer.GetName() != "headless";
        var bot = new CombatBot(level, this) { Shots = shots, ShotPrefix = "ato1" };
        _failures.AddRange(await bot.Run(hunter: null, lanternTest: false));
        Check(bot.RankUps.Contains(Rank.D), $"vencer a Lodo-Mãe deveria celebrar o rank D (celebrações: [{string.Join(",", bot.RankUps)}])");
        Check(Progression.Rank == Rank.D, $"depois da Lodo-Mãe o rank deveria ser D, é {Progression.Rank}");
        Check(StoryService.Engine.HasFlag(StoryFlags.GuardianDefeatedLodoMae), "a história deveria saber da Lodo-Mãe");
        Check(StoryService.Engine.ObjectiveId == "ato2" && StoryService.Engine.CurrentObjective?.Locked == true,
            $"depois do Guardião o objetivo deveria ser o Ato II (em breve), é '{StoryService.Engine.ObjectiveId}'");
        await WaitFor(() => _router.Current is Level { Switching: false } && !GetTree().Paused, "Praça depois da Brecha");
        var hub = (Level)_router.Current!;

        // Cena do fim do Ato I: Benício vem até o jogador, a lanterna brilha mais, selo D e o gancho do Pomar Alto.
        Check(hub.PlayAutoplay(), "depois do Guardião a cena do fim do Ato I deveria tocar");
        await WaitForDialogue(hub, "cena do fim do Ato I");
        Check(hub.Dialogue.CurrentNodeId == "n1", $"o fim do Ato I deveria abrir em 'n1', abriu em '{hub.Dialogue.CurrentNodeId}'");
        hub.Dialogue.Complete();
        await Seconds(0.4);
        await Shot("ato1_fim_lanterna", 0.5);
        await SkipDialogue(hub);
        Check(StoryService.Engine.HasFlag(StoryFlags.Ato1FimDone), "o fim do Ato I deveria ligar ato1_fim_done");
        Check(!hub.PlayAutoplay(), "a cena do fim do Ato I não deveria repetir");
        return hub;
    }

    /// <summary>
    /// Legibilidade dos avisos (QA): Lodo, Espectro e Lodo-Mãe lado a lado, cada um parado sem aviso e com o contorno âmbar,
    /// longe do caçador, no chão escuro da arena (o pior caso para o Espectro).
    /// </summary>
    private async Task CaptureWarnings(Level level)
    {
        await level.GoToRoom("esgoto_arena", withDirector: false);
        await PhysicsFrames(4);
        var world = level.World;
        var human = world.Data.HumanHeight;
        var row = world.PlayArea.GetCenter() + new Vector2(0, 0.6f * human);
        world.Player.Teleport(world.ClampToFloor(row + new Vector2(0, 2.4f * human)));
        var spawned = new System.Collections.Generic.List<Vespera.Game.Actors.Enemies.Enemy>();
        (string Id, float X)[] layout = [("lodo", -5.2f), ("lodo", -4.3f), ("espectro", -2.9f), ("espectro", -1.8f), ("lodo_mae", 0.9f), ("lodo_mae", 4.4f)];
        foreach (var (id, x) in layout)
        {
            Vespera.Game.Actors.Enemies.Enemy e = id switch
            {
                "lodo" => new Vespera.Game.Actors.Enemies.Lodo(world),
                "espectro" => new Vespera.Game.Actors.Enemies.Espectro(world),
                _ => new Vespera.Game.Actors.Enemies.LodoMae(world),
            };
            e.Position = row + new Vector2(x * human, 0);
            world.AddEnemy(e);
            spawned.Add(e);
        }

        await PhysicsFrames(3);
        for (var i = 0; i < spawned.Count; i++)
        {
            spawned[i].ProcessMode = ProcessModeEnum.Disabled;
            spawned[i].ShowWarningPose(i % 2 == 1);
        }

        foreach (var e in spawned)
        {
            Log.Info($"qa-aviso {e.MonsterId} em {e.GlobalPosition.Round()} visível {e.IsVisibleInTree()} anim '{e.SpriteNode.Animation}' escala {e.SpriteNode.GlobalScale.Y:0.00}");
        }

        world.Camera.FixedFocus = row + new Vector2(-0.4f * human, -1.0f * human);
        await PhysicsFrames(10);
        await Shot("ato1_avisos_legiveis", 0.3);
        world.Camera.FixedFocus = null;
        foreach (var e in spawned)
        {
            e.QueueFree();
        }

        await level.GoToRoom("forja_interior");
        await PhysicsFrames(4);
    }

    private async Task ActOneEpilogue(Level level)
    {
        level = await Travel(level, z => z.Target == "forjas", new Vector2(24, 0), "Level_forjas");
        level = await Travel(level, z => z.Target == "forja_interior", new Vector2(0, 40 * level.World.Data.ActorScale), "Level_forja_interior");
        var check = Progression.Forge.CanUpgrade();
        Check(check.CanUpgrade, $"depois da Brecha a forja deveria permitir o upgrade ({check.Reason}; Fragmentos {Progression.State.Fragmentos}, " +
            $"materiais [{string.Join(", ", Progression.State.Materials.Select(m => $"{m.Key} {m.Value}"))}])");
        var before = Progression.Engine.WeaponStats.Level;
        await TalkToNpc(level, "odete", shot: "ato1_odete_depois");
        Check(StoryService.Engine.HasFlag("odete_after"), "a fala nova da Odete deveria ter rodado");
        await WaitFor(() => FindOverlay<ForgeScreen>() is not null, "forja depois da fala nova");
        await Shot("ato1_forja_depois");
        await Tap(Key.Enter);
        await Seconds(1.0);
        Check(Progression.Engine.WeaponStats.Level == before + 1, $"forjar deveria subir a arma ({before} -> {Progression.Engine.WeaponStats.Level})");
        await Shot("ato1_forja_feita", 0);
        await Tap(Key.Escape);
        await Frames(4);
        Check(StoryService.Engine.ObjectiveId == "ato2", "o objetivo deveria seguir no Ato II");

        InventoryScreen.Open();
        await WaitFor(() => FindOverlay<InventoryScreen>() is not null, "inventário");
        await Shot("ato1_inventario");
        for (var i = 0; i < 4; i++)
        {
            await Tap(Key.Down);
        }

        await Shot("ato1_inventario_rolado", 0.4);
        for (var i = 0; i < 4; i++)
        {
            await Tap(Key.Up);
        }

        await Shot("ato1_inventario_topo", 0.4);
        await Tap(Key.Escape);
        await Frames(4);
        Check(FindOverlay<InventoryScreen>() is null && !GetTree().Paused, "Esc deveria fechar o inventário");
        if (_shotsDir is not null && DisplayServer.GetName() != "headless")
        {
            await CaptureWarnings(level);
        }

        Log.Info($"ato I completo: rank {Progression.Rank}, arma nível {Progression.Engine.WeaponStats.Level}, " +
            $"{Progression.State.Fragmentos} Fragmentos, objetivo '{StoryService.Engine.CurrentObjective?.Title}' (em breve)");
    }
}
