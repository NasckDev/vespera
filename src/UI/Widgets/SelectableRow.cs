using System;
using Godot;
using Vespera.Autoload;
using Vespera.UI.Common;
using Vespera.UI.Screens;

namespace Vespera.UI.Widgets;

/// <summary>
/// Linha/cartão selecionável de menu (opções de diálogo, título, pausa, abas, células). Foco visível pelo tema
/// (<see cref="NormalStyle"/>/<see cref="FocusStyle"/>), navegação por teclado/gamepad com o foco do Godot, hover do
/// mouse seleciona (como a v2) e clique, Enter/Espaço/A ou E confirmam. Opção desabilitada continua focável e toca
/// <c>hurt</c> ao confirmar (<c>ui.js:50</c>).
/// </summary>
[GlobalClass]
public partial class SelectableRow : PanelContainer
{
    private bool _disabled;
    private bool _hovered;
    private StringName _normalStyle = "RowNormal";
    private StringName _focusStyle = "RowFocus";

    public SelectableRow()
    {
        FocusMode = FocusModeEnum.All;
        MouseFilter = MouseFilterEnum.Stop;
        ThemeTypeVariation = _normalStyle;
    }

    /// <summary>O que fazer ao confirmar (não é chamado se <see cref="Disabled"/>).</summary>
    public Action? OnActivate { get; set; }

    /// <summary>Chamado ao ganhar foco (tooltip, detalhe do item…).</summary>
    public Action? OnFocused { get; set; }

    /// <summary>Teclas ←/→ (ou stick) com foco aqui: −1/+1. Usado por opções de valor (volume, liga/desliga).</summary>
    public Action<int>? OnAdjust { get; set; }

    public StringName NormalStyle
    {
        get => _normalStyle;
        set
        {
            _normalStyle = value;
            UpdateStyle();
        }
    }

    public StringName FocusStyle
    {
        get => _focusStyle;
        set
        {
            _focusStyle = value;
            UpdateStyle();
        }
    }

    public StringName DisabledFocusStyle { get; set; } = "RowFocusDisabled";

    /// <summary>Estilo com o mouse por cima sem foco (null = igual ao normal).</summary>
    public StringName? HoverStyle { get; set; }

    /// <summary>Desenha o losango âmbar à esquerda quando em foco (linhas de lista da v2).</summary>
    public bool Marker { get; set; } = true;

    /// <summary>Toca o som <c>ui</c> ao confirmar.</summary>
    public bool ClickSound { get; set; } = true;

    public bool Disabled
    {
        get => _disabled;
        set
        {
            _disabled = value;
            UpdateStyle();
        }
    }

    /// <summary>Confirma como se o jogador tivesse apertado (também usado pelo smoke test).</summary>
    public void Activate()
    {
        if (FindScreen() is { InputReady: false })
        {
            return; // a v2 ignora entrada nos primeiros 0,15 s de cada tela
        }

        if (_disabled)
        {
            UiSound.Denied();
            return;
        }

        if (ClickSound)
        {
            UiSound.Click();
        }

        OnActivate?.Invoke();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            GrabFocus();
            AcceptEvent();
            Activate();
            return;
        }

        if (@event.IsEcho())
        {
            return;
        }

        if (@event.IsActionPressed("ui_accept") || @event.IsActionPressed(InputActions.Interact))
        {
            AcceptEvent();
            Activate();
            return;
        }

        if (OnAdjust is not null)
        {
            if (@event.IsActionPressed("ui_left") || @event.IsActionPressed(InputActions.MoveLeft))
            {
                AcceptEvent();
                OnAdjust(-1);
            }
            else if (@event.IsActionPressed("ui_right") || @event.IsActionPressed(InputActions.MoveRight))
            {
                AcceptEvent();
                OnAdjust(1);
            }
        }
    }

    public override void _Notification(int what)
    {
        switch ((long)what)
        {
            case NotificationFocusEnter:
                UpdateStyle();
                if (FindScreen() is { InputReady: true })
                {
                    UiSound.Click(); // navegar toca "ui" (ui.js:42)
                }

                OnFocused?.Invoke();
                break;
            case NotificationFocusExit:
                UpdateStyle();
                break;
            case NotificationMouseEnter:
                _hovered = true;
                if (!HasFocus() && GetViewport()?.GuiGetFocusOwner() is not LineEdit)
                {
                    GrabFocus(); // hover do mouse seleciona (ui.js:42)
                }

                UpdateStyle();
                break;
            case NotificationMouseExit:
                _hovered = false;
                UpdateStyle();
                break;
        }
    }

    public override void _Draw()
    {
        if (Marker && HasFocus())
        {
            Diamond.Paint(this, new Vector2(6, Mathf.Round(Size.Y / 2f)), 2.5f, _disabled ? UiPalette.Disabled : UiPalette.Ambar);
        }
    }

    private void UpdateStyle()
    {
        var style = HasFocus() ? (_disabled ? DisabledFocusStyle : FocusStyle) : (_hovered && HoverStyle is not null ? HoverStyle : NormalStyle);
        if (ThemeTypeVariation != style)
        {
            ThemeTypeVariation = style;
        }

        QueueRedraw();
    }

    private Screen? FindScreen()
    {
        for (var node = GetParent(); node is not null; node = node.GetParent())
        {
            if (node is Screen screen)
            {
                return screen;
            }
        }

        return null;
    }
}
