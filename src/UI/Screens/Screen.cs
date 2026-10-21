using Godot;
using Vespera.UI.Widgets;

namespace Vespera.UI.Screens;

/// <summary>
/// Tela modal da UI (diálogo, mural, inventário, pausa…), o <c>G.ui</c> da v2. Vive numa <see cref="ScreenStack"/>,
/// que entrega a entrada só para a tela do topo. Cada tela é criada uma vez e reaproveitada (abre/fecha sem recriar).
/// </summary>
public partial class Screen : Control
{
    /// <summary>A v2 ignora entrada nos primeiros 0,15 s de cada tela (spec 02 §4).</summary>
    public const double InputDelaySeconds = 0.15;

    public Screen()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        SetProcess(false);
    }

    public ScreenStack? Stack { get; internal set; }

    /// <summary>Segundos desde que a tela abriu (ou voltou ao topo).</summary>
    public double Age { get; private set; }

    public bool InputReady => Age >= InputDelaySeconds;

    public bool IsOpen => Stack is not null;

    /// <summary>Esc/B fecha a tela.</summary>
    public virtual bool CloseOnCancel => true;

    /// <summary>Esconde a tela de baixo enquanto aberta (sub-telas da pausa); o diálogo deixa ver o mundo.</summary>
    public virtual bool HidesBelow => false;

    /// <summary>Tela cheia: esconde os painéis do HUD enquanto aberta (o diálogo e as lojas deixam o HUD à vista).</summary>
    public virtual bool HidesHud => false;

    /// <summary>Trava o jogador e pausa o mundo (todo modal da v2, <c>main.js:91</c>).</summary>
    public virtual bool BlocksGameplay => true;

    /// <summary>WASD/stick navegam como as setas (desligue em telas que digitam texto ou tratam as teclas sozinhas).</summary>
    public virtual bool WasdNavigation => true;

    /// <summary>Controle que recebe o foco ao abrir.</summary>
    protected virtual Control? DefaultFocus => null;

    public void Close() => Stack?.Remove(this);

    /// <summary>Foca o controle padrão (ou o primeiro focável).</summary>
    public void FocusDefault()
    {
        if (DefaultFocus is { } focus && focus.IsVisibleInTree())
        {
            focus.GrabFocus();
            return;
        }

        FindFirstFocusable(this)?.GrabFocus();
    }

    /// <summary>Entrada não tratada pela GUI, só quando esta tela está no topo. True = consumiu.</summary>
    public virtual bool HandleInput(InputEvent @event)
    {
        if (CloseOnCancel && InputReady && !@event.IsEcho() && @event.IsActionPressed("ui_cancel"))
        {
            UiSound();
            Close();
            return true;
        }

        return false;
    }

    public override void _Process(double delta) => Age += delta;

    internal void NotifyOpened()
    {
        Age = 0;
        Visible = true;
        SetProcess(true);
        OnOpened();
        FocusDefault();
    }

    internal void NotifyResumed()
    {
        Age = 0;
        Visible = true;
        OnResumed();
        FocusDefault();
    }

    internal void NotifyClosed()
    {
        Visible = false;
        SetProcess(false);
        OnClosed();
    }

    /// <summary>Recomeça a contagem de <see cref="Age"/> (nova fala na mesma tela).</summary>
    protected void RestartAge() => Age = 0;

    protected virtual void OnOpened()
    {
    }

    /// <summary>Voltou ao topo depois que uma sub-tela fechou.</summary>
    protected virtual void OnResumed()
    {
    }

    protected virtual void OnClosed()
    {
    }

    protected static void UiSound() => Common.UiSound.Click();

    private static Control? FindFirstFocusable(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Control { Visible: true } control)
            {
                if (control.FocusMode == FocusModeEnum.All && control is SelectableRow or BaseButton or LineEdit)
                {
                    return control;
                }

                if (FindFirstFocusable(control) is { } inner)
                {
                    return inner;
                }
            }
        }

        return null;
    }
}
