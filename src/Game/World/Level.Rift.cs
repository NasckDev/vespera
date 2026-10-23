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
    private ClassPicker? _picker;
    private bool _switching;
    private int? _carriedHp;
    private Vector2? _hubSpawn;

    /// <summary>Uma troca de sala está em andamento (bots esperam por ela).</summary>
    public bool Switching => _switching;

    public bool PickerOpen => _picker is not null;

    /// <summary>Seletor de arma aberto no portal (bots conferem a arma padrão).</summary>
    public ClassPicker? Picker => _picker;

    /// <summary>Entra na Brecha com a classe escolhida (usado pelo seletor e pelo bot de combate).</summary>
    public void EnterRift(HunterClass hunter)
    {
        ClosePicker();
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

        RiftRules.MarkClosed(RiftSession.Save);
        var end = RiftSession.EndRun(survived: true);
        _carriedHp = null;
        GameSession.Autosave();
        _ = ReturnToHub($"Brecha fechada! +{end?.FragmentosBanked ?? 0} Fragmentos levados para casa.", "Esgotos do Mercado — fechada");
    }

    private void OnPlayerDied(HunterCombat combat)
    {
        if (!RiftSession.InRift)
        {
            combat.Revive();
            return;
        }

        var lost = RiftSession.EndRun(survived: false)?.FragmentosLeft ?? 0;
        _carriedHp = null;
        GameSession.Autosave();
        var message = lost > 0
            ? $"Você caiu. Seus {lost} Fragmentos ficaram na Lanterna, dentro da Brecha."
            : "Você caiu. A Brecha te cuspiu de volta na Praça.";
        _ = ReturnToHub(message, "A Lanterna ficou para trás");
    }

    private async Task ReturnToHub(string toast, string subtitle)
    {
        await GoToRoom(HubLevelId, _hubSpawn);
        World.Player.Combat.Revive();
        CombatHud.ShowTitle("Praça da Lanterna", subtitle);
        CombatHud.Toast(toast);
        if (RiftSession.LanternFragments > 0)
        {
            CombatHud.Toast($"{RiftSession.LanternFragments} Fragmentos esperam na Lanterna. Entre de novo na Brecha para recuperá-los.");
        }

        ScheduleAutoplay();
    }

    private void OpenPicker()
    {
        if (_picker is not null)
        {
            return;
        }

        if (!StoryService.Engine.HasFlag(StoryFlags.StarterBlade))
        {
            ShowNotice("A fenda pulsa, mas você está de mãos vazias. Fale com a Odete, na forja do Bairro das Forjas.");
            return;
        }

        _hubSpawn = World.Player.GlobalPosition + new Vector2(0, PortalStepBack * World.Data.ActorScale);
        Player.Frozen = true;
        _picker = new ClassPicker(EquippedHunter) { Name = "ClassPicker" };
        _picker.Chosen += EnterRift;
        _picker.Cancelled += ClosePicker;
        AddChild(_picker);
        MoveChild(_picker, _fade.GetIndex());
    }

    /// <summary>Classe da arma equipada na progressão (a que a Odete entregou); o seletor do portal começa nela.</summary>
    private static HunterClass EquippedHunter =>
        Hunter.Progression.IsActive ? RiftSession.HunterFor(Hunter.Progression.Engine.ClassId) : RiftSession.Hunter;

    private void ClosePicker()
    {
        if (_picker is null)
        {
            return;
        }

        _picker.QueueFree();
        _picker = null;
        Player.Frozen = false;
    }

    private void UpdatePicker()
    {
        if (_picker is not null)
        {
            Player.Frozen = true;
        }
    }

    /// <summary>Teclas de teste na Praça: 1 Lâmina, 2 Arco, 3 Martelo.</summary>
    private bool HandleDebugKey(Key key)
    {
        HunterClass? hunter = key switch
        {
            Key.Key1 => HunterClass.Lamina,
            Key.Key2 => HunterClass.Arco,
            Key.Key3 => HunterClass.Martelo,
            _ => null,
        };
        if (hunter is not { } chosen || RiftSession.InRift || _picker is not null || _switching)
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
