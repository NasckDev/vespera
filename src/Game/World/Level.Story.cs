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
        if (StoryService.Engine.AutoplayFor(World.Data.Id)?.Dialogue == "chegada")
        {
            _fade.Color = _fade.Color with { A = 1f };
        }

        GetTree().CreateTimer(AutoplayDelay).Timeout += () => PlayAutoplay();
    }

    private void UpdateStory()
    {
        _storyHud.Visible = !_box.IsOpen && !_sceneBusy && !RiftSession.InRift;
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
            else if (name == StoryFlags.EventChangeWeapon)
            {
                OpenWeaponChange();
            }
        }
    }

    /// <summary>"Trocar de arma" da Odete: escolha da arma nova, confirmação, e o caçador troca de folha e de golpes na hora.</summary>
    private void OpenWeaponChange()
    {
        GetTree().Paused = true;
        var current = RiftSession.Hunter;
        SceneRouter.Instance.Push(new ClassSelectScreen(
            chosen => ConfirmWeaponChange(chosen, current),
            current,
            "Trocar de arma",
            "Trocar para esta arma",
            () => GetTree().Paused = false));
    }

    private void ConfirmWeaponChange(HunterClass chosen, HunterClass current)
    {
        if (chosen == current)
        {
            ClosePauseOverlay();
            return;
        }

        SceneRouter.Instance.Push(new ConfirmScreen(
            $"Trocar {HunterSprites.DisplayName(current)} por {HunterSprites.DisplayName(chosen)}? A arma nova começa no nível 1; a atual fica guardada.",
            () => ApplyWeaponChange(chosen)));
    }

    private void ApplyWeaponChange(HunterClass chosen)
    {
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
}
