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
/// esperando junto ao portal) → arco das caçadas (<see cref="ActOneArc"/>: sem invulnerabilidade) → fala nova da Odete
/// → forja → objetivo = Ato II (em breve).
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
        ApplyClassFlag(start);
        var level = await ActOnePrologue(start);
        level = await ActOneArc(level);
        await ActOneEpilogue(level);
    }

    /// <summary><c>--classe=bow|hammer|sword</c>: joga o Ato I com outra arma (comparação de classes no QA).</summary>
    private void ApplyClassFlag(Level start)
    {
        var arg = System.Array.Find(_args, a => a.StartsWith("--classe=", System.StringComparison.Ordinal));
        if (arg is null || GameSession.Data is not { } data)
        {
            return;
        }

        var weapon = arg["--classe=".Length..];
        SaveService.ChangeClass(data, weapon);
        Progression.Reset();
        RiftSession.Hunter = RiftSession.HunterFor(weapon);
        start.Player.SetHunter(RiftSession.Hunter);
        Log.Info($"ato I com a classe {weapon} ({Progression.Engine.ClassId})");
    }

    /// <summary>Chegada v2: estrada (borda sul), Lodo no caminho, Brecha abrindo, Benício e a aposta do Ravi.</summary>
    private async Task<Level> ActOnePrologue(Level level)
    {
        Check(StoryService.Engine.ObjectiveId == "estrada", $"jogo novo deveria começar na estrada, está em '{StoryService.Engine.ObjectiveId}'");
        await SizeShot(level, "praca");
        Check(Progression.Rank == Rank.E, "jogo novo deveria começar no rank E");
        Check(level.PlayAutoplay(), "a chegada deveria tocar sozinha");
        await WaitForDialogue(level, "Benício na Brecha aberta");
        Check(level.Dialogue.CurrentNodeId == "a", $"a chegada deveria abrir no Benício, abriu em '{level.Dialogue.CurrentNodeId}'");
        Check(level.Player.GetNodeOrNull<PointLight2D>("OwnLantern") is not null, "a lanterna do caçador deveria acender sozinha");
        await Shot("ato1_chegada_benicio", 0.5);
        await SkipDialogue(level);
        Check(StoryService.Engine.HasFlag(StoryFlags.ArrivalDone), "a chegada deveria ligar arrival_done");
        Check(StoryService.Engine.HasFlag(StoryFlags.StarterBlade), "o caçador deveria chegar com a Lâmina enferrujada");
        Check(StoryService.Engine.ObjectiveId == "cacada1", $"depois do Benício o objetivo deveria ser fechar a Brecha, é '{StoryService.Engine.ObjectiveId}'");
        Check(level.PlayAutoplay(), "depois do Benício o Ravi deveria chegar correndo");
        await WaitForDialogue(level, "aposta do Ravi");
        Check(level.Dialogue.CurrentNodeId == "r1", $"a aposta deveria abrir em 'r1', abriu em '{level.Dialogue.CurrentNodeId}'");
        await Shot("ato1_ravi_aposta", 0.5);
        await SkipDialogue(level);
        Check(StoryService.Engine.HasFlag(StoryFlags.MetRavi), "a aposta do Ravi deveria ligar met_ravi");
        Log.Info("ato I: estrada, Lodo, Brecha abrindo, Benício e Ravi OK");
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
        Check(StoryService.Engine.ObjectiveId == "cacada1", $"depois da Lâmina o objetivo deveria ser a 1ª caçada, é '{StoryService.Engine.ObjectiveId}'");

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

    /// <summary>Fecho v2: Odete (escolha da arma + Marisa) na Forja e, de manhã, a maçã roxa da Lis na Praça.</summary>
    private async Task ActOneEpilogue(Level level)
    {
        level = await Travel(level, z => z.Target == "forjas", new Vector2(24, 0), "Level_forjas");
        level = await Travel(level, z => z.Target == "forja_interior", new Vector2(0, 40 * level.World.Data.ActorScale), "Level_forja_interior");
        await PlayPendingScenes(level);
        Check(StoryService.Engine.HasFlag("odete_marisa"), "a Odete deveria falar da Marisa no fecho");
        Check(StoryService.Engine.HasFlag(StoryFlags.WeaponChosen), "a Odete deveria oferecer a escolha da arma");
        await WaitFor(() => FindOverlay<ClassSelectScreen>() is not null, "escolha da arma na Odete");
        await Shot("ato1_odete_escolha_arma");
        await Tap(Key.Escape);
        await Frames(4);
        await Seconds(0.4);
        Check(!GetTree().Paused, "cancelar a escolha deveria despausar");
        level = await Travel(level, z => z.Target == "forjas", new Vector2(0, -30 * level.World.Data.ActorScale), "Level_forjas");
        level = await Travel(level, z => z.Target == "praca", new Vector2(-24, 0), "Level_praca");
        await PlayPendingScenes(level);
        Check(StoryService.Engine.ObjectiveId == "ato2", $"o objetivo final deveria ser o Pomar Alto, é '{StoryService.Engine.ObjectiveId}'");

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
