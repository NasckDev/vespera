using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.UI.Common;

namespace Vespera.UI.Screens;

/// <summary>
/// Pilha de telas modais. Só a do topo recebe entrada; Esc/B fecha a do topo. Também navega com WASD/stick
/// (os <c>move_*</c>) além das setas/d-pad que o foco do Godot já entende. Avisa quando a pilha muda
/// (<see cref="Changed"/>) para o dono travar o jogo e emitir <c>PauseChanged</c>.
/// </summary>
public partial class ScreenStack : Control
{
    private readonly List<Screen> _screens = [];

    public ScreenStack()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        SetProcess(false);
    }

    /// <summary>Disparado depois de abrir ou fechar uma tela. Quem assina remove em _ExitTree.</summary>
    public event Action? Changed;

    public int Count => _screens.Count;

    public Screen? Top => _screens.Count > 0 ? _screens[^1] : null;

    /// <summary>Alguma tela aberta trava o jogo.</summary>
    public bool BlocksGameplay
    {
        get
        {
            foreach (var screen in _screens)
            {
                if (screen.BlocksGameplay)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Alguma tela aberta é cheia e esconde o HUD.</summary>
    public bool HidesHud
    {
        get
        {
            foreach (var screen in _screens)
            {
                if (screen.HidesHud)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Controles que ficam por baixo das telas (o menu do título): perdem o foco enquanto há tela aberta.</summary>
    public Control? BaseLayer { get; set; }

    public bool Contains(Screen screen) => _screens.Contains(screen);

    /// <summary>Guarda a tela como filha (escondida) para ser aberta depois e liberada junto com a pilha.</summary>
    public void Adopt(params Screen[] screens)
    {
        foreach (var screen in screens)
        {
            if (screen.GetParent() != this)
            {
                screen.GetParent()?.RemoveChild(screen);
                AddChild(screen);
            }
        }
    }

    /// <summary>Abre a tela no topo (adiciona como filha na primeira vez). Já aberta: vai para o topo.</summary>
    public void Push(Screen screen)
    {
        if (screen.GetParent() != this)
        {
            screen.GetParent()?.RemoveChild(screen);
            AddChild(screen);
        }

        if (_screens.Remove(screen))
        {
            screen.NotifyClosed();
        }

        if (Top is { } below)
        {
            below.FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Disabled;
            if (screen.HidesBelow)
            {
                below.Visible = false;
            }
        }

        if (BaseLayer is not null)
        {
            BaseLayer.FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Disabled;
        }

        _screens.Add(screen);
        screen.Stack = this;
        screen.FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Inherited;
        MoveChild(screen, -1);
        screen.NotifyOpened();
        SetProcess(true);
        Changed?.Invoke();
    }

    /// <summary>Fecha a tela (em qualquer posição); a de baixo volta a receber foco.</summary>
    public void Remove(Screen screen)
    {
        var index = _screens.IndexOf(screen);
        if (index < 0)
        {
            return;
        }

        var wasTop = index == _screens.Count - 1;
        _screens.RemoveAt(index);
        screen.Stack = null;
        screen.NotifyClosed();
        if (wasTop && Top is { } top)
        {
            top.FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Inherited;
            top.NotifyResumed();
        }

        RestoreBaseLayer();
        SetProcess(_screens.Count > 0);
        Changed?.Invoke();
    }

    /// <summary>Fecha tudo (voltar ao título, morte, troca de cena).</summary>
    public void Clear()
    {
        while (Top is { } top)
        {
            top.Stack = null;
            _screens.RemoveAt(_screens.Count - 1);
            top.NotifyClosed();
        }

        RestoreBaseLayer();
        SetProcess(false);
        Changed?.Invoke();
    }

    private void RestoreBaseLayer()
    {
        if (_screens.Count == 0 && BaseLayer is not null)
        {
            BaseLayer.FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Inherited;
        }
    }

    public override void _Input(InputEvent @event) => InputHints.Observe(@event);

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Top is not { } top)
        {
            return;
        }

        if (top.HandleInput(@event))
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (top.BlocksGameplay && IsGameplayEvent(@event))
        {
            GetViewport().SetInputAsHandled(); // com tela aberta nada chega no jogo
        }
    }

    public override void _Process(double delta)
    {
        // WASD e stick esquerdo navegam como as setas (a v2 aceita W/S em todos os menus).
        if (Top is not { InputReady: true, WasdNavigation: true } || GetViewport().GuiGetFocusOwner() is LineEdit)
        {
            return;
        }

        Navigate(InputActions.MoveUp, UiUp, Side.Top);
        Navigate(InputActions.MoveDown, UiDown, Side.Bottom);
        Navigate(InputActions.MoveLeft, UiLeft, Side.Left);
        Navigate(InputActions.MoveRight, UiRight, Side.Right);
    }

    // StringName fixo: a conversão implícita de "ui_up" criava um StringName novo por chamada (4 por quadro com tela
    // aberta, ~380 B/quadro no teste de QA de alocação).
    private static readonly StringName UiUp = "ui_up";
    private static readonly StringName UiDown = "ui_down";
    private static readonly StringName UiLeft = "ui_left";
    private static readonly StringName UiRight = "ui_right";

    private void Navigate(StringName move, StringName ui, Side side)
    {
        if (!Input.IsActionJustPressed(move) || Input.IsActionJustPressed(ui))
        {
            return;
        }

        var owner = GetViewport().GuiGetFocusOwner();
        if (owner is null)
        {
            Top?.FocusDefault();
            return;
        }

        if (owner is Widgets.SelectableRow { OnAdjust: { } adjust } && side is Side.Left or Side.Right)
        {
            adjust(side == Side.Left ? -1 : 1);
            return;
        }

        if (owner.FindValidFocusNeighbor(side) is { } next)
        {
            next.GrabFocus();
        }
    }

    private static bool IsGameplayEvent(InputEvent @event) =>
        @event is InputEventKey or InputEventMouseButton or InputEventJoypadButton or InputEventAction;
}
