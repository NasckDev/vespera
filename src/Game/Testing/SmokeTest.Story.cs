using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Core.Story;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Game.Story;
using Vespera.Game.UI;
using Vespera.Game.World;

namespace Vespera.Game.Testing;

/// <summary>
/// Bot da história (<c>--story</c>): jogo novo → fala de chegada → quadro do Benício → Ravi → ponte → forja → Odete
/// (lâmina, objetivo, gancho da forja) → Guardião derrotado → fala nova da Odete. Termina com o jogo na Forja.
/// A fala automática de chegada fica desligada em todo o smoke (atrapalharia os outros roteiros); o bot a chama à mão.
/// </summary>
public partial class SmokeTest
{
    public const string StoryFlag = "--story";

    private const int MaxDialogueSteps = 90;

    private readonly HashSet<string> _portraitShots = [];

    private static void ConfigureStory()
    {
        StoryService.SuppressAutoplay = true;
        Level.SceneSpeed = 20f;
    }

    /// <summary>Espera a conversa abrir depois de uma cena de entrada (a cena anda e espera antes de falar).</summary>
    private async Task WaitForDialogue(Level level, string what)
    {
        for (var i = 0; i < 4000 && !level.DialogueOpen; i++)
        {
            await Frames(1);
        }

        Check(level.DialogueOpen, $"tempo esgotado esperando: {what}");
        await Frames(2);
    }

    private async Task RunStoryBot(Level start)
    {
        var events = new List<string>();
        void OnEvent(string name) => events.Add(name);
        StoryService.Engine.EventRaised += OnEvent;
        var level = start;

        // Recomeça a história do zero (os testes de interação do quadro já mexeram nas flags).
        var progress = StoryService.Engine.Progress;
        progress.Flags.Clear();
        progress.ObjectiveId = string.Empty;
        StoryService.Engine.BeginCampaignIfNew();
        await CheckScaleContract(level, "praca");
        Check(StoryService.Engine.ObjectiveId == "estrada", $"jogo novo deveria começar na estrada, está em '{StoryService.Engine.ObjectiveId}'");
        Check(level.PlayAutoplay(), "a chegada deveria tocar");
        await WaitForDialogue(level, "Benício na Brecha aberta");
        Check(level.Dialogue.CurrentNodeId == "a", $"a chegada deveria abrir no Benício, está em '{level.Dialogue.CurrentNodeId}'");
        await Shot("story_01_chegada");
        await SkipDialogue(level);
        Check(StoryService.Engine.HasFlag(StoryFlags.ArrivalDone), "a chegada deveria ligar arrival_done");
        Check(StoryService.Engine.HasFlag(StoryFlags.StarterBlade), "o caçador deveria chegar com a Lâmina");
        Check(StoryService.Engine.ObjectiveId == "cacada1", $"objetivo depois da chegada: '{StoryService.Engine.ObjectiveId}'");

        Check(level.PlayAutoplay(), "depois do Benício o Ravi deveria vir apostar");
        await WaitForDialogue(level, "aposta do Ravi");
        await SkipDialogue(level);
        Check(StoryService.Engine.HasFlag(StoryFlags.MetRavi), "Ravi deveria apostar");

        await TalkToNpc(level, "lis");
        await OpenAndCloseJournal();

        StoryService.SetFlag(StoryFlags.HuntOne);
        Check(StoryService.Engine.ObjectiveId == "nilza", $"depois da 1ª Brecha o objetivo deveria ser a Nilza, é '{StoryService.Engine.ObjectiveId}'");
        Check(level.PlayAutoplay(), "na volta o Ravi deveria perguntar o placar");
        await WaitForDialogue(level, "placar do Ravi");
        await SkipDialogue(level);
        Check(level.PlayAutoplay(), "depois do placar a Nilza deveria vir com a sopa");
        await WaitForDialogue(level, "sopa da Nilza");
        await SkipDialogue(level);
        Check(events.Contains(StoryFlags.EventGivePotion), "a Nilza deveria dar a sopa (GivePotion)");
        Check(StoryService.Engine.ObjectiveId == "forja", $"depois da Nilza o objetivo deveria ser a forja, é '{StoryService.Engine.ObjectiveId}'");

        level = await Travel(level, z => z.Target == "forjas", new Vector2(24, 0), "Level_forjas");
        level = await Travel(level, z => z.Target == "forja_interior", new Vector2(0, 40 * level.World.Data.ActorScale), "Level_forja_interior");
        await TalkToNpc(level, "odete");
        Check(events.Contains(StoryFlags.EventOpenForge), "a Odete deveria pedir os Fragmentos (OpenForge)");
        await WaitFor(() => FindOverlay<ForgeScreen>() is not null, "forja aberta pela conversa (ForgeScreen.Open)");
        await Tap(Key.Escape);
        await Frames(4);
        Check(FindOverlay<ForgeScreen>() is null && !GetTree().Paused, "Esc deveria fechar a forja");
        await Seconds(0.4);
        Check(StoryService.Engine.ObjectiveId == "manha", $"depois da Odete o objetivo deveria ser a manhã, é '{StoryService.Engine.ObjectiveId}'");

        StoryService.SetFlag(StoryFlags.GuardianDefeatedLodoMae);
        StoryService.SetFlag(StoryFlags.Ato1FimDone);
        Check(level.PlayAutoplay(), "no fecho a Odete deveria chamar na Forja");
        await WaitForDialogue(level, "fecho da Odete");
        await Shot("story_09_odete_fecho", 0.4);
        await SkipDialogue(level);
        Check(StoryService.Engine.HasFlag("odete_marisa"), "a Odete deveria falar da Marisa");
        await WaitFor(() => FindOverlay<ClassSelectScreen>() is not null, "escolha da arma na Odete");
        await Tap(Key.Escape);
        await Frames(4);
        await Seconds(0.4);
        Check(!GetTree().Paused, "cancelar a escolha deveria despausar");
        Check(StoryService.Engine.ObjectiveId == "dormir", $"depois da Odete o objetivo deveria ser dormir, é '{StoryService.Engine.ObjectiveId}'");

        StoryService.Engine.EventRaised -= OnEvent;
        Log.Info("história OK: chegada, Ravi, placar, Nilza, Odete, fecho com a Marisa");
    }

    private const float ScaleTolerance = 0.08f;
    private static readonly string[] ScaleActors = ["odete", "ravi", "benicio", "lis", "nilza"];

    /// <summary>
    /// Contrato de escala (PLANO-V2 §2.1): em cada mapa, mede a altura opaca renderizada (px de mundo) do caçador e de
    /// cada NPC e exige o alvo da tabela dentro de ±8%; depois fotografa a "fila de escala" (caçador, Odete, Ravi, Benício).
    /// </summary>
    private async Task CheckScaleContract(Level level, string name)
    {
        var data = level.World.Data;
        var human = data.HumanHeight;
        var hunter = OpaqueHeight(HunterSprites.Build(HunterClass.Lamina).GetFrameTexture(HunterSprites.IdleAnimation(Facing.Down), 0)) * level.Player.Sprite.Scale.Y;
        Check(Within(hunter, human), $"{name}: caçador com {hunter:0.0} px, alvo {human:0.0} px (±8%)");
        var row = new List<Npc>();
        var lineup = data.Lineup.Length == 2 ? new Vector2(data.Lineup[0], data.Lineup[1]) : level.World.SpawnPoint;
        var spacing = human;
        foreach (var id in ScaleActors)
        {
            var npc = new Npc(id, new NpcPlacement { Id = id, At = [0, 0], Routine = "idle" }, human) { Name = $"Lineup_{id}" };
            level.World.Entities.AddChild(npc);
            var frames = NpcSprites.Build(id);
            var measured = OpaqueHeight(frames.GetFrameTexture(NpcSprites.Animation(false, Facing.Down), 0)) * npc.SpriteScale;
            var wanted = ScaleRegistry.TargetHeight(id, human);
            Check(Within(measured, wanted), $"{name}: {id} com {measured:0.0} px, alvo {wanted:0.0} px (±8%)");
            row.Add(npc);
        }

        for (var i = 0; i < row.Count; i++)
        {
            row[i].GlobalPosition = lineup + new Vector2((i + 1) * spacing, 0);
            row[i].SetActive(false);
        }

        await StandAt(level, lineup);
        level.World.Camera.FixedFocus = lineup + new Vector2(spacing * (row.Count / 2f), -human / 2f);
        await PhysicsFrames(3);
        await Shot($"escala_{name}");
        level.World.Camera.FixedFocus = null;
        foreach (var npc in row)
        {
            npc.QueueFree();
        }
    }

    private static bool Within(float measured, float wanted) => Mathf.Abs(measured - wanted) <= wanted * ScaleTolerance;

    private static float OpaqueHeight(Texture2D texture)
    {
        var image = texture.GetImage();
        int top = -1, bottom = -1;
        for (var y = 0; y < image.GetHeight(); y++)
        {
            for (var x = 0; x < image.GetWidth(); x++)
            {
                if (image.GetPixel(x, y).A >= 0.5f)
                {
                    top = top < 0 ? y : top;
                    bottom = y;
                    break;
                }
            }
        }

        return bottom - top + 1;
    }

    private async Task TalkToProp(Level level, string type)
    {
        var prop = level.World.Props.First(p => p.Type == type);
        await StandAt(level, prop.GlobalPosition + new Vector2(0, 50 * level.World.Data.ActorScale));
        await PressInWorld(Key.E);
        Check(level.DialogueOpen, $"E em '{type}' deveria abrir uma conversa");
        await Shot($"story_02_{type}");
        await SkipDialogue(level);
    }

    private async Task TalkToNpc(Level level, string npcId, string? shot = null)
    {
        var npc = level.World.Npcs.First(n => n.NpcId == npcId);
        // De frente para o NPC, a ~1 altura humana (dentro do alcance da conversa, sem sobrepor os dois na foto).
        await StandAt(level, level.World.ClampToFloor(npc.GlobalPosition + new Vector2(0, 1.0f * level.World.Data.HumanHeight)));
        Check(level.Player.Focus?.Kind == "npc", $"perto de {npcId} o foco deveria ser o NPC, foi '{level.Player.Focus?.Kind}'");
        await PressInWorld(Key.E);
        Check(level.DialogueOpen, $"E em {npcId} deveria abrir a conversa");
        Log.Info($"conversa com {npcId}: nó '{level.Dialogue.CurrentNodeId}' flags [{string.Join(",", StoryService.Engine.Progress.Flags)}]");
        if (shot is not null)
        {
            await Shot(shot);
        }

        await SkipDialogue(level);
    }

    /// <summary>Aperta Interagir até a conversa fechar (a 1ª aperto de cada caixa completa o texto; a 2ª avança).</summary>
    private async Task SkipDialogue(Level level)
    {
        var shot = new HashSet<string>();
        var lastNode = string.Empty;
        for (var i = 0; i < MaxDialogueSteps && level.DialogueOpen; i++)
        {
            if (level.Dialogue.CurrentNodeId != lastNode)
            {
                lastNode = level.Dialogue.CurrentNodeId;
                _dialogueChars += level.Dialogue.FullText.Length;
            }

            // Uma foto por retrato diferente (falante + expressão), com o texto todo na caixa.
            var key = $"{level.Dialogue.SpeakerName}|{level.Dialogue.CurrentNodeId}";
            if (_portraitShots.Contains(level.Dialogue.SpeakerName + level.Dialogue.Expression) is false && shot.Add(key))
            {
                _portraitShots.Add(level.Dialogue.SpeakerName + level.Dialogue.Expression);
                level.Dialogue.Complete();
                await Shot($"dialogo_{System.Text.RegularExpressions.Regex.Replace(level.Dialogue.SpeakerName, "[^A-Za-z0-9]+", "_")}_{level.Dialogue.Expression}", 0.5);
            }

            await PressInWorld(Key.E);
        }

        Check(!level.DialogueOpen, "a conversa deveria ter terminado");
        await Seconds(0.35);
    }

    private async Task<Level> Travel(Level from, Func<Vespera.Game.Components.Interactable, bool> pick, Vector2 offset, string expectedName)
    {
        var zone = from.World.Zones.First(pick);
        await StandAt(from, zone.GlobalPosition + offset);
        Check(from.Player.Focus == zone, $"perto da saída para {expectedName} o foco deveria ser a saída");
        Log.Info($"viagem para {expectedName}: foco '{from.Player.Focus?.Kind}', congelado {from.Player.Frozen}, estado {from.Player.Combat.State}, " +
            $"conversa {from.DialogueOpen}, cena {from.SceneBusy}, painel {from.Hud.PanelOpen}, pausado {GetTree().Paused}, sobreposições {_router.OverlayDepth}");
        await PressInWorld(Key.E);
        await WaitFor(() => _router.Current is Level l && l.Name == expectedName && !_router.IsBusy, $"chegar em {expectedName}");
        var next = (Level)_router.Current!;
        await Frames(4);
        await Shot($"story_{expectedName}");
        await CheckScaleContract(next, expectedName.Replace("Level_", string.Empty, StringComparison.Ordinal));
        if (Array.IndexOf(_args, BotFlag) >= 0)
        {
            next.World.SetNpcsActive(false);
            _failures.AddRange(await new CollisionBot(next.World, this).Run());
            next.World.SetNpcsActive(true);
            next.Player.Teleport(next.World.SpawnPoint);
            await PhysicsFrames(2);
        }

        return next;
    }

    private async Task OpenAndCloseJournal()
    {
        await PressInWorld(Key.L);
        Check(_router.OverlayDepth == 1 && GetTree().Paused, "L deveria abrir o Diário e pausar o jogo");
        await Shot("story_diario");
        await Tap(Key.Escape);
        await Frames(3);
        Check(_router.OverlayDepth == 0 && !GetTree().Paused, "Esc deveria fechar o Diário e continuar o jogo");
    }
}
