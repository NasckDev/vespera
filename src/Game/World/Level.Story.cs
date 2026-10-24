using System.Collections.Generic;
using Godot;
using Vespera.Core.Story;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Game.Story;
using Vespera.Game.UI;
using Vespera.Game.World.Rift;

namespace Vespera.Game.World;

/// <summary>
/// História dentro da cena de jogo: caixa de diálogo, HUD do objetivo, fala automática de chegada, diário (J) e os
/// eventos que a conversa dispara ao fechar (a forja da Odete).
/// </summary>
public partial class Level
{
    private const float AutoplayDelay = 0.7f;
    private const float DialogueCooldown = 0.3f;
    private const int WeaponChangeFragments = 10;
    private const int MapLumes = 10;
    private const string MapFlag = "mapa_lis";
    private const int SoupLumes = 6;

    private readonly List<string> _pendingEvents = [];
    private DialogueBox _box = null!;
    private StoryHud _storyHud = null!;
    private Npc? _speaking;

    /// <summary>Verdadeiro enquanto uma conversa está aberta (o mundo continua, mas o jogador fica parado).</summary>
    public bool DialogueOpen => _box.IsOpen;

    public DialogueBox Dialogue => _box;

    private bool DialogueBlocksInteraction => _box.IsOpen || _sceneBusy || _box.SecondsSinceClosed < DialogueCooldown;

    /// <summary>Abre a fala <paramref name="id"/>; <paramref name="npc"/> (se houver) para e vira o rosto para o jogador.</summary>
    public bool StartDialogue(string id, Npc? npc = null)
    {
        var runner = StoryService.StartDialogue(id);
        if (runner is null || _box.IsOpen)
        {
            return false;
        }

        CloseReading();
        Player.Frozen = true;
        _speaking = npc;
        npc?.Hold(true, Player.GlobalPosition);
        var deviceId = (Player.Input as MergedInput)?.DeviceId ?? Player.Input.DeviceId;
        _box.Open(runner, StoryService.Script, InteractionHud.InteractKeyName(deviceId));
        return true;
    }

    /// <summary>Abre a fala automática desta fase se a história pede (a chegada à Praça); falso se não há nada a tocar.</summary>
    public bool PlayAutoplay()
    {
        if (!IsInsideTree() || _box.IsOpen || _switching || StoryService.Engine.AutoplayFor(World.Data.Id) is not { } auto)
        {
            return false;
        }

        return BeginAutoplayScene(auto);
    }

    private void BuildStory()
    {
        _storyHud = new StoryHud { Name = "StoryHud" };
        AddChild(_storyHud);
        AddChild(new HintHud(this));
        _box = new DialogueBox { Name = "Dialogue" };
        AddChild(_box);
        _box.Closed += OnDialogueClosed;
        _box.NodeShown += OnDialogueNode;
        StoryService.Engine.EventRaised += OnStoryEvent;
    }

    private void UnbindStory() => StoryService.Engine.EventRaised -= OnStoryEvent;

    /// <summary>A fase de cidade em que o jogador está vira o ponto de volta do "Continuar" (salas da Brecha não contam).</summary>
    private void RecordStoryLevel()
    {
        if (!RiftSession.InRift)
        {
            StoryService.Engine.Progress.LevelId = World.Data.Id;
        }
    }

    private void ScheduleAutoplay()
    {
        if (StoryService.SuppressAutoplay)
        {
            return;
        }

        // A chegada começa na tela preta (a cena faz o fade): sem isto o jogador apareceria um instante no meio da praça.
        if (StoryService.Engine.AutoplayFor(World.Data.Id)?.Stage is "arrival" or "morning")
        {
            _fade.Color = _fade.Color with { A = 1f };
        }

        GetTree().CreateTimer(AutoplayDelay).Timeout += () => PlayAutoplay();
    }

    private void UpdateStory()
    {
        _storyHud.Visible = !_box.IsOpen && (!_sceneBusy || _freeRoam) && !RiftSession.InRift;
        if (_box.IsOpen)
        {
            Player.Frozen = true;
        }
    }

    private bool HandleJournalKey(InputEvent @event)
    {
        if (!@event.IsActionPressed(InputActions.Journal) || !SceneRouter.Instance.IsActive(this) || _box.IsOpen)
        {
            return false;
        }

        GetViewport().SetInputAsHandled();
        GetTree().Paused = true;
        SceneRouter.Instance.Push(ScreenKeys.Journal);
        return true;
    }

    private void OnDialogueClosed()
    {
        Player.Frozen = false;
        ReleaseCamera();
        if (_speaking is { } speaker && IsInstanceValid(speaker))
        {
            speaker.Hold(false);
            _ = speaker.WalkTo(speaker.Home);
        }

        _speaking = null;
        var events = _pendingEvents.ToArray();
        _pendingEvents.Clear();
        GameSession.Autosave();
        ScheduleAutoplay();
        foreach (var name in events)
        {
            if (name == StoryFlags.EventOpenForge)
            {
                ForgeScreen.Open();
            }
            else if (name == StoryFlags.EventChooseWeapon)
            {
                OpenWeaponChange(free: true);
            }
            else if (name == StoryFlags.EventChangeWeapon)
            {
                OpenWeaponChange(free: false);
            }
            else if (name == StoryFlags.EventBuyMap)
            {
                BuyMap();
            }
            else if (name == StoryFlags.EventOpenContracts)
            {
                Hunt.ContractsScreen.Show();
            }
            else if (name == StoryFlags.EventGivePotion)
            {
                GiftPotion();
            }
            else if (name == StoryFlags.EventBuyPotion)
            {
                BuyPotion();
            }
            else if (name == StoryFlags.EventBuySoup)
            {
                BuySoup();
            }
        }
    }

    /// <summary>"Trocar de arma" da Odete: escolha da arma nova, confirmação, e o caçador troca de folha e de golpes na hora.</summary>
    private void OpenWeaponChange(bool free)
    {
        var current = EquippedHunter;
        if (!free && Hunter.Progression.IsActive && Hunter.Progression.State.Fragmentos < WeaponChangeFragments)
        {
            ShowNotice($"Trocar de arma custa {WeaponChangeFragments} Fragmentos. Você tem {Hunter.Progression.State.Fragmentos}.");
            return;
        }

        GetTree().Paused = true;
        SceneRouter.Instance.Push(new ClassSelectScreen(
            chosen => ConfirmWeaponChange(chosen, current, free),
            current,
            free ? "Qual vai levar?" : "Trocar de arma",
            free ? "Levar esta arma" : $"Trocar ({WeaponChangeFragments} Fragmentos)",
            () => GetTree().Paused = false));
    }

    private void ConfirmWeaponChange(HunterClass chosen, HunterClass current, bool free)
    {
        if (chosen == current)
        {
            ClosePauseOverlay();
            return;
        }

        var cost = free ? "De graça, desta vez." : $"Custa {WeaponChangeFragments} Fragmentos.";
        SceneRouter.Instance.Push(new ConfirmScreen(
            $"Levar {HunterSprites.DisplayName(chosen)} no lugar de {HunterSprites.DisplayName(current)}? {cost} A arma atual fica guardada.",
            () => ApplyWeaponChange(chosen, free)));
    }

    private void ApplyWeaponChange(HunterClass chosen, bool free)
    {
        if (!free && Hunter.Progression.IsActive)
        {
            Hunter.Progression.State.Fragmentos -= WeaponChangeFragments;
        }

        if (GameSession.Data is { } data)
        {
            SaveService.ChangeClass(data, HunterSprites.WeaponId(chosen));
            Hunter.Progression.Reset();
            GameSession.Autosave();
        }

        Player.SetHunter(chosen);
        ClosePauseOverlay();
        CombatHud.Toast($"Nova arma: {HunterSprites.DisplayName(chosen)}");
    }

    private void ClosePauseOverlay()
    {
        while (SceneRouter.Instance.OverlayDepth > 0)
        {
            SceneRouter.Instance.Pop();
        }

        GetTree().Paused = false;
    }

    private void OnStoryEvent(string name) => _pendingEvents.Add(name);

    /// <summary>Poção de presente da Tia Nilza (até o limite da bolsa).</summary>
    private void GiftPotion()
    {
        var state = Hunter.HuntService.State;
        state.Potions = System.Math.Min(Hunter.HuntService.Config.Potions.Max, state.Potions + 1);
        Hunter.HuntService.ServeSoup();
        CombatHud.Toast($"Sopa da Nilza: +{Core.Hunt.RunPerks.SoupMaxHpPct:0}% de vida máxima na próxima caçada.");
        CombatHud.Toast($"Você ganhou uma poção! ({state.Potions} na bolsa)");
        GameSession.Autosave();
    }

    /// <summary>Sopa da Nilza comprada (ROTEIRO-V2 §3, 7:00): comida = bônus visível na próxima caçada.</summary>
    private void BuySoup()
    {
        if (Hunter.HuntService.State.SoupReady)
        {
            ShowNotice("Você já comeu. A sopa vale para a próxima caçada.");
            return;
        }

        if (!Hunter.Progression.IsActive || !Hunter.Progression.Engine.TrySpendLumes(SoupLumes))
        {
            ShowNotice($"A sopa custa {SoupLumes} Lumes.");
            return;
        }

        Hunter.HuntService.ServeSoup();
        CombatHud.Toast($"Sopa da Nilza: +{Core.Hunt.RunPerks.SoupMaxHpPct:0}% de vida máxima na próxima caçada. -{SoupLumes} Lumes");
        GameSession.Autosave();
    }

    /// <summary>Mapa da Lis (ROTEIRO-V2 §3, 9:30): paga 10 Lumes. O efeito dentro da Brecha (sala do tesouro marcada) ainda é do lado da caçada.</summary>
    private void BuyMap()
    {
        if (!Hunter.Progression.IsActive || !Hunter.Progression.Engine.TrySpendLumes(MapLumes))
        {
            ShowNotice($"O mapa custa {MapLumes} Lumes.");
            return;
        }

        StoryService.SetFlag(MapFlag);
        Hunter.HuntService.HandMap();
        CombatHud.Toast($"Mapa da Lis: -{MapLumes} Lumes. Na próxima caçada você vê cada sala e onde está o tesouro.");
        GameSession.Autosave();
    }

    private void BuyPotion()
    {
        var (ok, message) = Hunter.HuntService.BuyPotion(withFlower: false);
        ShowNotice(message);
        if (ok)
        {
            StoryService.SetFlag(StoryFlags.PotionBought);
        }
    }
}
