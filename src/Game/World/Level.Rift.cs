using System.Threading.Tasks;
using Godot;
using Vespera.Core.Rift;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Core.Story;
using Vespera.Game.Combat;
using Vespera.Game.Story;
using Vespera.Game.UI;
using Vespera.Game.World.Rift;

namespace Vespera.Game.World;

/// <summary>
/// Fluxo da Brecha dentro da cena de jogo: escolha da arma no portal da Praça, troca de sala com fade (a vida passa
/// de uma sala para a outra), vitória (os Fragmentos da caçada vão para a carteira) e queda (ficam na Lanterna do
/// bairro e voltam na próxima entrada). A regra da Lanterna é a da <see cref="Hunter.Progression"/>.
/// </summary>
public partial class Level
{
    public const string HubLevelId = "praca";
    private const float FadeSeconds = 0.32f;
    private const float PortalStepBack = 70f;

    private ColorRect _fade = null!;
    private bool _switching;
    private int? _carriedHp;
    private Vector2? _hubSpawn;

    /// <summary>Uma troca de sala está em andamento (bots esperam por ela).</summary>
    public bool Switching => _switching;

    /// <summary>Atalhos de teste (1/2/3 trocam a arma na Praça) só existem com <c>--dev</c> na linha de comando.</summary>
    public static bool DevMode { get; } = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--dev") >= 0;

    /// <summary>Entra na Brecha com a classe da arma (usado pelo portal e pelo bot de combate).</summary>
    public void EnterRift(HunterClass hunter)
    {
        RiftSession.Hunter = hunter;
        var recovered = RiftSession.BeginRun();
        _carriedHp = null;
        _ = EnterFirstRoom(recovered);
    }

    private async Task EnterFirstRoom(int recovered)
    {
        await GoToRoom(RiftSession.CurrentRoom);
        if (recovered > 0)
        {
            CombatHud.Toast($"Lanterna recuperada! +{recovered} Fragmentos de volta à caçada.");
        }
    }

    /// <summary>Troca o mundo do SubViewport pela sala <paramref name="id"/> com fade para preto.</summary>
    public async Task GoToRoom(string id, Vector2? spawn = null, bool withDirector = true)
    {
        if (_switching)
        {
            return;
        }

        _switching = true;
        World.Player.Frozen = true;
        await FadeTo(1f);
        CloseReading();

        // Aviso da sala anterior não atravessa a porta (senão o primeiro Interagir da sala nova só fecharia o aviso).
        _noticeTimer = 0f;
        _hud.ClosePanel();
        Numbers.Clear();
        var old = World;
        _data = LoadData(id);
        if (spawn is { } at)
        {
            _data.Spawn = [at.X, at.Y];
        }

        old.Name = "OldWorld";
        _viewport.RemoveChild(old);
        old.QueueFree();
        Engine.TimeScale = 1.0;
        MountWorld(new LevelWorld(_data, _catalog, withDirector) { Name = "World" });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await FadeTo(0f);
        _switching = false;
    }

    private void OnWorldMounted()
    {
        CombatHud.ClearToasts();
        RecordStoryLevel();
        RestoreLanternGlow();
        if (_carriedHp is { } hp)
        {
            World.Player.Combat.SetHp(hp);
        }

        if (World.Director is { } director)
        {
            CombatHud.ShowTitle(RiftSession.Brecha.Name, director.Title.Split('—')[^1].Trim());
        }
    }

    private void OnRoomExit()
    {
        _carriedHp = World.Player.Combat.Hp;
        if (!RiftSession.IsLastRoom)
        {
            RiftSession.AdvanceRoom();
            _ = GoToRoom(RiftSession.CurrentRoom);
            return;
        }

        var guardian = Hunter.HuntService.Plan?.HasGuardian == true;
        if (guardian)
        {
            RiftRules.MarkClosed(RiftSession.Save);
        }

        var (end, summary) = RiftSession.EndRunWithSummary(survived: true, completed: true);
        _carriedHp = null;
        GameSession.Autosave();
        var toast = guardian
            ? $"Brecha fechada! +{end?.FragmentosBanked ?? 0} Fragmentos levados para casa."
            : $"Caçada completa! +{end?.FragmentosBanked ?? 0} Fragmentos levados para casa.";
        _ = ReturnToHub(toast, guardian ? $"{RiftSession.Brecha.Name} — fechada" : "De volta, inteiro", summary, guardian);
    }

    private void OnPlayerDied(HunterCombat combat)
    {
        if (!RiftSession.InRift)
        {
            combat.Revive();
            return;
        }

        var (end, summary) = RiftSession.EndRunWithSummary(survived: false, completed: false);
        var lost = end?.FragmentosLeft ?? 0;
        _carriedHp = null;
        GameSession.Autosave();
        var message = lost > 0
            ? $"Você caiu. Seus {lost} Fragmentos ficaram na Lanterna, dentro da Brecha."
            : "Você caiu. A Brecha te cuspiu de volta na Praça.";
        _ = ReturnToHub(message, "A Lanterna ficou para trás", summary, false);
    }

    /// <summary>Tela de resultado da última caçada (abre na Praça, antes das falas automáticas).</summary>
    public static bool ShowResults { get; set; } = true;

    private async Task ReturnToHub(string toast, string subtitle, Hunter.RunSummary? summary = null, bool closed = false)
    {
        var hub = RiftSession.Brecha.Hub.Length > 0 ? RiftSession.Brecha.Hub : HubLevelId;
        await GoToRoom(hub, _hubSpawn);
        if (summary is not null && ShowResults)
        {
            Hunt.RunResultScreen.Show(summary, closed);
            while (Hunt.RunResultScreen.Active is not null && IsInstanceValid(this))
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }

        World.Player.Combat.Revive();
        CombatHud.ShowTitle(World.Data.Name, subtitle);
        CombatHud.Toast(toast);
        if (RiftSession.LanternFragments > 0)
        {
            CombatHud.Toast($"{RiftSession.LanternFragments} Fragmentos esperam na Lanterna. Entre de novo na Brecha para recuperá-los.");
        }

        ScheduleAutoplay();
    }

    /// <summary>
    /// Entrar na Brecha pelo portal da Praça: sem pergunta de classe (ela foi escolhida em "Nova história" e só muda pela
    /// Odete). Sem a arma da Odete o portal recusa com um aviso.
    /// </summary>
    private void EnterFromPortal()
    {
        if (_switching)
        {
            return;
        }

        if (!StoryService.Engine.HasFlag(StoryFlags.StarterBlade))
        {
            ShowNotice("A fenda pulsa, mas você está de mãos vazias. Fale com a Odete, na Forja: atravesse a ponte a oeste.");
            return;
        }

        _hubSpawn = World.Player.GlobalPosition + new Vector2(0, PortalStepBack * World.Data.ActorScale);
        EnterRift(EquippedHunter);
    }

    /// <summary>Classe da arma equipada na progressão (a escolhida em "Nova história" ou trocada pela Odete).</summary>
    private static HunterClass EquippedHunter =>
        Hunter.Progression.IsActive ? RiftSession.HunterFor(Hunter.Progression.Engine.ClassId) : RiftSession.Hunter;

    /// <summary>Teclas de teste na Praça (só com --dev): 1 Lâmina, 2 Arco, 3 Martelo.</summary>
    private bool HandleDebugKey(Key key)
    {
        if (!DevMode)
        {
            return false;
        }

        HunterClass? hunter = key switch
        {
            Key.Key1 => HunterClass.Lamina,
            Key.Key2 => HunterClass.Arco,
            Key.Key3 => HunterClass.Martelo,
            _ => null,
        };
        if (hunter is not { } chosen || RiftSession.InRift || _switching)
        {
            return false;
        }

        RiftSession.Hunter = chosen;
        Player.SetHunter(chosen);
        CombatHud.Toast($"Arma: {HunterSprites.DisplayName(chosen)}");
        return true;
    }

    private void BuildFade()
    {
        _fade = new ColorRect { Name = "Fade", Color = Palette.Indigo with { A = 0f }, MouseFilter = MouseFilterEnum.Ignore };
        _fade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_fade);
    }

    private async Task FadeTo(float alpha)
    {
        var tween = CreateTween().SetIgnoreTimeScale(true);
        tween.TweenProperty(_fade, "color:a", alpha, FadeSeconds);
        await ToSignal(tween, Tween.SignalName.Finished);
    }
}
