using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Core.Final;
using Vespera.Core.Ranks;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.Boot;
using Vespera.Game.Components;
using Vespera.Game.Story;
using Vespera.Game.UI;
using Vespera.Game.World.Rift;

namespace Vespera.Game.World;

/// <summary>
/// O Final (ROTEIRO-V2 §13 FINAL, "A Primeira Brecha"): depois do Ato VII (<see cref="FinalStory.UnlockFlag"/>) a primeira
/// Brecha abre na Praça, ao pé da grande lanterna (interagir abre <c>final/antes</c>; "Entrar." dispara
/// <c>EnterBreach:primeira_brecha</c>). Dentro, a praça de baixo e o <see cref="Reflexo"/>. Ao cair, toca
/// <c>final/reflexo_queda</c> e o sino de baixo vira interação (<c>final/sino</c>). Eventos: <c>RingBell:*</c> mostra a
/// ilustração de quem tocou (<see cref="BellScene"/>), <c>BurnSeal:Alvor</c> faz a cerimônia do selo e a caçada fecha de
/// volta na Praça, onde toca o epílogo; <c>EndPart1</c> mostra o encerramento (<see cref="PartOneEnd"/>) e volta ao título
/// com o save marcado (<see cref="FinalStory.PartOneDoneFlag"/>).
/// </summary>
public partial class Level
{
    public const string BellZoneKind = "sino";
    public const string FinalPortalName = "PrimeiraBrecha";
    private const string LisHiddenFlag = "lis_escondida";
    private const string LisInsideFlag = "lis_entrou";
    private const float FinalPortalHumans = 4.2f;
    private const float FinalPortalBelowLantern = 110f;
    private const float FallPause = 1.4f;
    private static readonly Vector2 BellSteps = new(838, 196);

    private bool _bellSequence;

    /// <summary>Portal da primeira Brecha na Praça (nulo se fechada ou fora da Praça).</summary>
    public BreachPortal? FinalPortal { get; private set; }

    /// <summary>A sequência do sino (ilustração, selo, volta) está rodando.</summary>
    public bool BellSequenceRunning => _bellSequence;

    private static bool HasFlag(string flag) => StoryService.Engine.HasFlag(flag);

    /// <summary>Chamado ao montar cada fase (gancho em <c>OnWorldMounted</c>).</summary>
    private void ShowFinal()
    {
        FinalPortal = null;
        if (!RiftSession.InRift && World.Data.Id == FinalStory.HubLevel && FinalStory.BreachOpen(HasFlag))
        {
            AddFinalPortal();
            return;
        }

        if (!RiftSession.InRift || World.Data.Id != FinalStory.ArenaLevel)
        {
            return;
        }

        if (World.Director?.Guardian is Reflexo reflexo && !reflexo.Dead)
        {
            reflexo.Died += OnReflexoDied;
        }
        else if (FinalStory.BellReady(HasFlag))
        {
            AddBellZone();
        }
    }

    /// <summary>A Brecha do tamanho da praça: fenda grande e vermelha logo abaixo da grande lanterna apagada.</summary>
    private void AddFinalPortal()
    {
        var human = World.Data.HumanHeight;
        var lantern = World.Props.FirstOrDefault(p => p.Type == "lanterna_bronze");
        var at = World.ClampToFloor((lantern?.Position ?? World.SpawnPoint) + new Vector2(0, FinalPortalBelowLantern * World.Data.ActorScale));
        FinalPortal = new BreachPortal(human * FinalPortalHumans, "brecha", "Entrar na primeira Brecha", 60f * World.Data.ActorScale)
        {
            Name = FinalPortalName,
            Position = at,
            Empowered = true,
        };
        FinalPortal.Zone.Text = "A Primeira Brecha · Rank SSS";
        FinalPortal.Zone.Target = FinalStory.BrechaId;
        FinalPortal.Zone.Dialogue = "final/antes";
        World.Entities.AddChild(FinalPortal);
        Log.Info("final: a primeira Brecha está aberta na Praça");
    }

    /// <summary>Eventos de fala do Final (chamado no fim de cada conversa). Verdadeiro se tratou.</summary>
    private bool HandleFinalEvent(string name)
    {
        if (FinalStory.BreachFromEvent(name) is { } breach)
        {
            EnterFinalBreach(breach);
            return true;
        }

        if (BellChoice.FromEvent(name) is { } ringer)
        {
            _ = RunBellSequence(ringer);
            return true;
        }

        if (name == FinalStory.EventEndPart1)
        {
            EndPartOne();
            return true;
        }

        return false;
    }

    private void EnterFinalBreach(string breach)
    {
        if (!RiftSession.BrechaFiles.ContainsKey(breach))
        {
            Log.Error($"final: Brecha desconhecida '{breach}'");
            return;
        }

        // A Lis entra junto se o jogador a escondeu da Ordem (BellChoice: só assim ela pode tocar o sino).
        if (HasFlag(LisHiddenFlag))
        {
            StoryService.SetFlag(LisInsideFlag);
        }

        RiftSession.Select(breach);
        EnterFromPortal();
    }

    private void OnReflexoDied(Enemy reflexo) => _ = OnReflexoFell();

    /// <summary>O Reflexo caiu: a fala da queda e o sino de baixo vira interação.</summary>
    private async Task OnReflexoFell()
    {
        await ToSignal(GetTree().CreateTimer(FallPause), SceneTreeTimer.SignalName.Timeout);
        if (!IsInstanceValid(this) || World.Data.Id != FinalStory.ArenaLevel)
        {
            return;
        }

        AddBellZone();
        StartDialogue("final/reflexo_queda");
    }

    private void AddBellZone()
    {
        if (World.Entities.GetNodeOrNull("zone_sino") is not null)
        {
            return;
        }

        var zone = new Interactable(BellZoneKind, "Tocar o sino de baixo", 70f * World.Data.ActorScale)
        {
            Name = "zone_sino",
            Dialogue = "final/sino",
            Position = BellSteps,
            PromptAnchor = BellSteps - new Vector2(0, World.Data.HumanHeight * 1.1f),
        };
        World.Entities.AddChild(zone);
    }

    /// <summary>Ilustração de quem tocou, a cerimônia do selo Alvor e a volta para a Praça (onde toca o epílogo).</summary>
    private async Task RunBellSequence(BellRinger ringer)
    {
        if (_bellSequence)
        {
            return;
        }

        _bellSequence = true;
        Player.Frozen = true;
        if (World.Entities.GetNodeOrNull<Interactable>("zone_sino") is { } bell)
        {
            bell.Enabled = false;
        }

        Log.Info($"final: o sino tocou ({ringer})");
        var scene = new BellScene(FinalStory.SceneFor(ringer)) { Name = "CenaDoSino" };
        AddChild(scene);
        await ToSignal(scene, Node.SignalName.TreeExited);
        if (HasFlag(Core.Story.StoryFlags.SealBurnedFlag(nameof(Rank.Alvor))))
        {
            await AlvorCeremony();
        }

        _bellSequence = false;
        Player.Frozen = false;
        if (RiftSession.InRift && World.Director is { } director)
        {
            director.RequestExit();
        }
    }

    /// <summary><c>BurnSeal:Alvor</c> fora da sequência do sino (cena avulsa): só a cerimônia.</summary>
    private void OnSealBurned(string rank)
    {
        if (rank == nameof(Rank.Alvor) && !_bellSequence)
        {
            _ = AlvorCeremony();
        }
    }

    /// <summary>Cerimônia simples: a celebração de rank com o selo Alvor (sol âmbar). O rank da progressão segue o XP.</summary>
    private async Task AlvorCeremony()
    {
        if (!Hunter.Progression.IsActive)
        {
            return;
        }

        var engine = Hunter.Progression.Engine;
        RankUpScreen.Show(new Core.Advancement.RankChange(engine.Rank, Rank.Alvor, engine.Stage, engine.Ladder.Stage(Rank.Alvor)));
        while (RankUpScreen.Active is not null && IsInstanceValid(this))
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    /// <summary>Fim da Parte 1: grava, mostra o encerramento com o gancho e volta ao título.</summary>
    private void EndPartOne()
    {
        if (GetNodeOrNull("FimDaParte1") is not null)
        {
            return;
        }

        StoryService.SetFlag(FinalStory.PartOneDoneFlag);
        GameSession.Autosave();
        Player.Frozen = true;
        var end = new PartOneEnd { Name = "FimDaParte1" };
        end.Finished += () => SceneRouter.Instance.GoTo(ScreenKeys.Title);
        AddChild(end);
    }
}
