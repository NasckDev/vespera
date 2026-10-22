using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Data;
using Vespera.Core.Rules;
using Vespera.Core.State;
using Vespera.UI.Common;
using Vespera.UI.Screens;
using Vespera.UI.Town;

namespace Vespera.UI;

/// <summary>
/// Interface dentro do jogo (frente B3): pilha de telas modais (diálogo, mural, inventário, pausa e sub-telas, morte),
/// serviços da cidade (<see cref="TownServices"/>), Brechas vistas pela UI (<see cref="BrechaTracker"/>), missões
/// (cumpre a atual quando não há tela aberta, como <c>updQuests</c>), tutorial da Odete e descanso.
/// Qualquer tela aberta trava o jogador (<c>GameManager.BlockGameplay("ui")</c>) e emite <c>PauseChanged</c>,
/// como o mundo da v2 que para enquanto há <c>G.ui</c> (<c>main.js:91</c>). Contratos em <c>api-interface.md</c>.
/// </summary>
public partial class GameUi : CanvasLayer
{
    public const string BlockReason = "ui";

    private const string SleepBlock = "sleep";
    private const float DeathDelay = 2.4f; // combat.js:55
    private const float TutorialDelay = 0.9f; // main.js:76
    private const float QuestCheckSeconds = 0.25f;
    private const float FadeOutRate = 3.5f; // main.js:81
    private const float FadeInRate = 3f;

    private ColorRect _fade = null!;
    private bool _blocked;
    private bool _paused;
    private float _tutorialT = -1f;
    private float _deathT = -1f;
    private float _questT;
    private bool _deathInBrecha;
    private string? _deathTheme;
    private float _fadeAlpha;
    private float _fadeTarget;
    private Action? _fadeCallback;

    /// <summary>O World dono desta UI (para trocar de mapa na Rua dos Ofícios).</summary>
    [Export]
    public World.World? World { get; set; }

    /// <summary>UI do World atual.</summary>
    public static GameUi? Current { get; private set; }

    /// <summary>O título liga antes de entrar no World em um jogo novo: o tutorial da Odete roda após 0,9 s.</summary>
    public static bool PendingTutorial { get; set; }

    public ScreenStack Stack { get; } = new();

    public DialogScreen Dialog { get; } = new();

    public BoardScreen Board { get; } = new();

    public InventoryScreen Inventory { get; } = new();

    public PauseScreen Pause { get; private set; } = null!;

    public DeathScreen Death { get; } = new();

    public OptionsScreen Options { get; } = new();

    public HelpScreen Help { get; } = new();

    public ControlsScreen Controls { get; } = new();

    public JournalScreen Journal { get; } = new();

    public BrechaTracker Brechas { get; } = new() { Name = "BrechaTracker" };

    public TownServices Town { get; private set; } = null!;

    /// <summary>Resgate da Lanterna já pago nesta sessão (<c>G.rescueFast</c>).</summary>
    public bool RescuePaid { get; set; }

    /// <summary>O mundo está pausado por uma tela da UI.</summary>
    public bool Paused => _paused;

    /// <summary>Morte registrada e tela ainda por abrir (2,4 s).</summary>
    public bool DeathPending => _deathT >= 0f;

    public override void _EnterTree() => Current = this;

    public override void _Ready()
    {
        Layer = 20;
        Town = new TownServices(this);
        Pause = new PauseScreen(this);
        AddChild(Brechas);
        AddChild(Stack);
        Stack.Adopt(Dialog, Board, Inventory, Pause, Death, Options, Help, Controls, Journal);
        _fade = new ColorRect { Color = new Color(8 / 255f, 6 / 255f, 14 / 255f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1, Visible = false };
        AddChild(_fade);

        Stack.Changed += OnStackChanged;
        var bus = EventBus.Instance;
        bus.InteractionRequested += OnInteractionRequested;
        bus.DialogueRequested += OnDialogueRequested;
        bus.ChoiceRequested += OnChoiceRequested;
        bus.ScreenRequested += OnScreenRequested;
        bus.PlayerDied += OnPlayerDied;
        bus.BrechaStarted += OnBrechaStarted;
        bus.LanternStateChanged += OnLanternStateChanged;
        bus.GameLoaded += OnGameLoaded;

        if (PendingTutorial)
        {
            PendingTutorial = false;
            _tutorialT = TutorialDelay;
        }
    }

    public override void _ExitTree()
    {
        Stack.Changed -= OnStackChanged;
        var bus = EventBus.Instance;
        bus.InteractionRequested -= OnInteractionRequested;
        bus.DialogueRequested -= OnDialogueRequested;
        bus.ChoiceRequested -= OnChoiceRequested;
        bus.ScreenRequested -= OnScreenRequested;
        bus.PlayerDied -= OnPlayerDied;
        bus.BrechaStarted -= OnBrechaStarted;
        bus.LanternStateChanged -= OnLanternStateChanged;
        bus.GameLoaded -= OnGameLoaded;
        GameManager.Instance?.UnblockGameplay(BlockReason);
        GameManager.Instance?.UnblockGameplay(SleepBlock);
        if (_paused)
        {
            _paused = false;
            bus.EmitSignal(EventBus.SignalName.PauseChanged, false);
        }

        if (Current == this)
        {
            Current = null;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Stack.Count > 0 || @event.IsEcho() || !CanOpenMenus)
        {
            return;
        }

        if (@event.IsActionPressed(InputActions.Pause))
        {
            GetViewport().SetInputAsHandled();
            OpenPause();
        }
        else if (@event.IsActionPressed(InputActions.Inventory))
        {
            GetViewport().SetInputAsHandled();
            OpenInventory();
        }
    }

    public override void _Process(double delta)
    {
        var dt = Mathf.Min(0.05f, (float)delta);
        if (_tutorialT >= 0f && Stack.Count == 0 && !(World?.IsTransitioning ?? false))
        {
            _tutorialT -= dt;
            if (_tutorialT < 0f)
            {
                Tutorial();
            }
        }

        if (_deathT >= 0f)
        {
            _deathT -= dt;
            if (_deathT < 0f)
            {
                OpenDeath();
            }
        }

        _questT += dt;
        if (_questT >= QuestCheckSeconds)
        {
            _questT = 0f;
            CheckQuest();
        }

        UpdateFade(dt);
    }

    // --- API para telas e serviços ---

    /// <summary>Mostra uma fala (abre o diálogo se preciso). Ver <see cref="DialogScreen.Show"/>.</summary>
    public void ShowDialog(string speaker, string text, IReadOnlyList<DialogChoice>? choices = null, Action? next = null, Action? cancel = null)
    {
        if (!Dialog.IsOpen)
        {
            Stack.Push(Dialog);
        }

        Dialog.Show(speaker, text, choices, next, cancel);
    }

    public void OpenBoard()
    {
        // O "openBoard" chega duas vezes desde a integração: pela interação (TownServices) e pela Cidade, que responde
        // ao mesmo InteractionRequested com ScreenRequested("board") (api-cidade.md). Uma tela só.
        if (Board.IsOpen)
        {
            return;
        }

        UiSound.Open();
        Stack.Push(Board);
    }

    public void OpenInventory()
    {
        UiSound.Open();
        Stack.Push(Inventory);
    }

    public void OpenPause()
    {
        UiSound.Click();
        Stack.Push(Pause);
    }

    /// <summary>Fecha todas as telas (smoke test, troca de cena).</summary>
    public void CloseAll() => Stack.Clear();

    public void Toast(string title, string sub = "", Color? color = null, string? icon = null, float seconds = HudKit.ToastBox.DefaultSeconds) =>
        Hud.Current?.Toast(title, sub, color, icon, seconds);

    public void Note(string text, string? icon = null) => Hud.Current?.Note(text, icon);

    /// <summary>Vai para um mapa pelo spawn do maps.json (Rua dos Ofícios → lojas, <c>world.js:272</c>).</summary>
    public void GoToMap(string mapId)
    {
        if (World is { } world && global::Vespera.World.World.Catalog.TryGet(mapId, out var map))
        {
            world.GoMap(mapId, map.Spawn.X, map.Spawn.Y);
        }
    }

    /// <summary>Dormir na Estalagem (<c>restDialog</c>, <c>world.js:264-268</c>): escurece, paga, novo dia, HP cheio, salva.</summary>
    public void Sleep(ServiceDef rest)
    {
        var state = GameManager.Instance.State;
        if (!state.CanPay(rest.Cost))
        {
            UiSound.Denied();
            return;
        }

        GameManager.Instance.BlockGameplay(SleepBlock);
        FadeThen(() =>
        {
            if (state.TryBuyService(rest) == PurchaseResult.Ok)
            {
                UiSound.Coin();
                GameManager.Instance.SaveGame();
            }

            GameManager.Instance.UnblockGameplay(SleepBlock);
        });
    }

    // --- interno ---

    private bool CanOpenMenus =>
        !(World?.IsTransitioning ?? false) && !GameManager.Instance.State.IsDead && _deathT < 0f && _fadeCallback is null;

    private void OnStackChanged()
    {
        if (Hud.Current is { } hud)
        {
            hud.ChromeVisible = !Stack.HidesHud;
        }

        var block = Stack.BlocksGameplay;
        if (block != _blocked)
        {
            _blocked = block;
            if (block)
            {
                GameManager.Instance.BlockGameplay(BlockReason);
            }
            else
            {
                GameManager.Instance.UnblockGameplay(BlockReason);
            }
        }

        if (block != _paused)
        {
            _paused = block;
            EventBus.Instance.EmitSignal(EventBus.SignalName.PauseChanged, block);
        }
    }

    private void OnInteractionRequested(string kind, string id)
    {
        if (Stack.Count == 0)
        {
            Town.Handle(kind, id);
        }
    }

    /// <summary><c>DialogueRequested(fala, linhas)</c>: uma fala por vez; no fim emite <c>DialogueFinished</c>.</summary>
    private void OnDialogueRequested(string speaker, string[] lines)
    {
        void ShowLine(int i)
        {
            if (i >= lines.Length)
            {
                EventBus.Instance.EmitSignal(EventBus.SignalName.DialogueFinished);
                return;
            }

            ShowDialog(speaker, TownServices.Fill(lines[i]), null, () => ShowLine(i + 1));
        }

        ShowLine(0);
    }

    /// <summary><c>ChoiceRequested</c> → opções do diálogo → <c>ChoiceMade(id, índice)</c> (Esc = −1).</summary>
    private void OnChoiceRequested(string requestId, string speaker, string text, string[] options)
    {
        var choices = new List<DialogChoice>(options.Length);
        for (var i = 0; i < options.Length; i++)
        {
            var index = i;
            choices.Add(new DialogChoice(options[i], Run: () => EventBus.Instance.EmitSignal(EventBus.SignalName.ChoiceMade, requestId, index)));
        }

        ShowDialog(speaker, text, choices, cancel: () => EventBus.Instance.EmitSignal(EventBus.SignalName.ChoiceMade, requestId, -1));
    }

    private void OnScreenRequested(string screenId)
    {
        switch (screenId)
        {
            case "board":
                OpenBoard();
                break;
            case "inventory":
                OpenInventory();
                break;
            case "pause":
                OpenPause();
                break;
            case "options":
                Stack.Push(Options);
                break;
            case "journal":
                Stack.Push(Journal);
                break;
            case "help":
                Stack.Push(Help);
                break;
            case "controls":
                Stack.Push(Controls);
                break;
            case "death":
                OpenDeath();
                break;
            default:
                Log.Warning($"ScreenRequested: tela '{screenId}' não existe.");
                break;
        }
    }

    private void OnPlayerDied()
    {
        if (_deathT >= 0f || Death.IsOpen)
        {
            return;
        }

        _deathInBrecha = Brechas.InsideTheme is not null;
        _deathTheme = Brechas.InsideTheme;
        _deathT = DeathDelay;
    }

    private void OpenDeath()
    {
        _deathT = -1f;
        Stack.Clear();
        Death.Setup(_deathInBrecha, _deathTheme is null ? null : BrechaTracker.Bairro(_deathTheme));
        Stack.Push(Death);
    }

    private void OnBrechaStarted(string themeId, int rank) => GameManager.Instance.State.CountQuest(QuestCounters.Entered); // extra.js:126

    private void OnLanternStateChanged(bool lost)
    {
        if (!lost)
        {
            RescuePaid = false;
        }
    }

    private void OnGameLoaded()
    {
        RescuePaid = false;
        _deathT = -1f;
    }

    /// <summary>
    /// Só o smoke desliga (DECISIONS INT-D7): nos cenários das outras frentes, a fala de missão cumprida abria sozinha
    /// no meio do cenário e travava o bot. No jogo é sempre true.
    /// </summary>
    public static bool AutoCompleteQuests { get; set; } = true;

    /// <summary><c>updQuests</c> (<c>extra.js:117-121</c>): cumpre a missão atual quando não há tela aberta.</summary>
    private void CheckQuest()
    {
        var state = GameManager.Instance.State;
        if (!AutoCompleteQuests || Stack.Count > 0 || (World?.IsTransitioning ?? false) || state.IsDead || _deathT >= 0f || _fadeCallback is not null)
        {
            return;
        }

        if (state.TryCompleteQuest() is not QuestDef quest)
        {
            return;
        }

        UiSound.Play("rank");
        Toast(UiText.QuestDoneTitle + quest.Name, state.QuestRules.RewardText(quest), UiPalette.Ambar, "ic_scroll", 4.5f);
        GameManager.Instance.SaveGame();
        if (quest.End is { } end)
        {
            ShowDialog(end.Who, TownServices.Fill(end.Text));
        }
    }

    /// <summary>Tutorial do jogo novo (<c>extra.js:129-136</c>): 3 falas da Odete e a nota do Diário.</summary>
    private void Tutorial()
    {
        var lines = Database.Db.Npcs.Dialogue["odete"]["tutorial"];
        void ShowLine(int i)
        {
            if (i >= lines.Count)
            {
                Note(TownServices.Line("odete", "tutorialNote"), "ic_scroll");
                return;
            }

            ShowDialog("Odete", TownServices.Fill(lines[i]), null, () => ShowLine(i + 1));
        }

        ShowLine(0);
    }

    private void FadeThen(Action callback)
    {
        _fadeCallback = callback;
        _fadeTarget = 1f;
        _fade.Visible = true;
    }

    private void UpdateFade(float dt)
    {
        if (!_fade.Visible)
        {
            return;
        }

        if (_fadeTarget > _fadeAlpha)
        {
            _fadeAlpha = Mathf.Min(1f, _fadeAlpha + (dt * FadeOutRate));
            if (_fadeAlpha >= 1f && _fadeCallback is { } callback)
            {
                _fadeCallback = null;
                callback();
                _fadeTarget = 0f;
            }
        }
        else
        {
            _fadeAlpha = Mathf.Max(0f, _fadeAlpha - (dt * FadeInRate));
            if (_fadeAlpha <= 0f)
            {
                _fade.Visible = false;
            }
        }

        _fade.Color = _fade.Color with { A = _fadeAlpha };
    }
}
