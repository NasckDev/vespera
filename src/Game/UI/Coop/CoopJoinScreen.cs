using System;
using System.Collections.Generic;
using Godot;
using Vespera.Core.Coop;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Game.Coop;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.UI;

/// <summary>
/// "Co-op local": cada jogador aperta um botão no PRÓPRIO dispositivo para entrar (teclado, metade direita do teclado,
/// controle 1, controle 2), escolhe a classe com a prévia do caçador (contorno na cor dele; mesma classe = capa em outro
/// tom) e confirma. Com os dois prontos, confirmar de novo (ou Start/Enter) começa. Do título vai para o espaço de save;
/// do menu de pausa (J2 chegando no meio do jogo) o J1 já vem dentro e travado, e o J2 nasce ao lado dele.
/// </summary>
public partial class CoopJoinScreen : UiScreen
{
    private const float StickThreshold = 0.6f;
    private const float StickRelease = 0.3f;

    private static readonly Dictionary<string, string> Blurbs = new()
    {
        ["sword"] = "Equilibrada: combo de 3 golpes\ne corte giratório.",
        ["bow"] = "Longo alcance: a flecha\ncarregada atravessa.",
        ["hammer"] = "Lento e brutal: quebra\npartes em dobro.",
    };

    private readonly CoopRoster _roster = CoopSession.NewRoster();
    private readonly Level? _level;
    private readonly JoinCard[] _cards = new JoinCard[2];
    private readonly HashSet<InputDevice> _stickHeld = [];
    private Label _footer = null!;
    private Label _status = null!;

    /// <summary>Do título: ninguém dentro ainda.</summary>
    public CoopJoinScreen()
    {
    }

    /// <summary>Do menu de pausa: o J1 (<paramref name="lead"/>, com a classe atual) já está dentro, pronto e travado.</summary>
    public CoopJoinScreen(Level level, InputDevice lead, HunterClass leadClass)
    {
        _level = level;
        var slot = _roster.Join(lead, locked: true)!;
        slot.ClassId = HunterSprites.WeaponId(leadClass);
        slot.Ready = true;
    }

    /// <summary>Grupo montado até agora (bots e capturas).</summary>
    public CoopRoster Roster => _roster;

    public bool HotJoin => _level is not null;

    protected override Control? DefaultFocus() => null;

    protected override void Build()
    {
        AddChild(UiKit.MakeBackdrop(showArt: _level is null));
        var root = UiKit.MakeMargin(120, 30, 120, 30);
        AddChild(root);
        var column = UiKit.MakeColumn(14);
        root.AddChild(column);
        column.AddChild(UiKit.MakeLabel("Co-op local", UiTheme.TitleVariation, HorizontalAlignment.Center));
        column.AddChild(UiKit.MakeLabel(
            _level is null ? "Cada jogador aperta um botão no próprio teclado ou controle para entrar." : "O J2 aperta um botão no teclado ou controle dele para entrar.",
            UiTheme.DimVariation, HorizontalAlignment.Center));

        var row = UiKit.MakeRow(70);
        row.Alignment = BoxContainer.AlignmentMode.Center;
        row.SizeFlagsVertical = SizeFlags.ExpandFill;
        column.AddChild(row);
        for (var i = 0; i < _cards.Length; i++)
        {
            _cards[i] = new JoinCard(i);
            row.AddChild(_cards[i]);
        }

        _status = UiKit.MakeLabel(string.Empty, UiTheme.HeadingVariation, HorizontalAlignment.Center);
        column.AddChild(_status);
        _footer = UiKit.MakeLabel(string.Empty, UiTheme.DimVariation, HorizontalAlignment.Center);
        column.AddChild(_footer);
        Refresh();
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsVisibleInTree() || !SceneRouter.Instance.IsActive(this) || Resolve(@event) is not { } command)
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        Apply(command.Device, command.Action);
    }

    /// <summary>Aplica um comando da tela (bots usam isto direto, como se o botão tivesse sido apertado).</summary>
    public void Apply(InputDevice device, JoinAction action)
    {
        var index = _roster.IndexOf(device);
        if (index < 0)
        {
            if (action == JoinAction.Cancel)
            {
                Back();
                return;
            }

            if (_roster.IsFull)
            {
                _status.Text = "Já há dois caçadores. Um deles precisa sair primeiro.";
                return;
            }

            _roster.Join(device);
            UiSfx.Play(UiSound.Confirm);
            Refresh();
            return;
        }

        var slot = _roster.Slots[index];
        switch (action)
        {
            case JoinAction.Left or JoinAction.Right:
                if (_roster.CycleClass(device, action == JoinAction.Left ? -1 : 1))
                {
                    UiSfx.Play(UiSound.Move);
                }

                break;
            case JoinAction.Confirm when !slot.Ready:
                _roster.SetReady(device, true);
                UiSfx.Play(UiSound.Confirm);
                break;
            case JoinAction.Confirm or JoinAction.Start:
                TryStart();
                return;
            case JoinAction.Cancel when slot.Locked:
                Back();
                return;
            case JoinAction.Cancel when slot.Ready:
                _roster.SetReady(device, false);
                UiSfx.Play(UiSound.Back);
                break;
            case JoinAction.Cancel:
                _roster.Leave(device);
                UiSfx.Play(UiSound.Back);
                break;
        }

        Refresh();
    }

    protected override void Back()
    {
        CoopSession.Pending = null;
        SceneRouter.Instance.Pop();
    }

    private void TryStart()
    {
        if (!_roster.CanStart)
        {
            _status.Text = _roster.Count < 2 ? "Falta o segundo caçador entrar." : "Os dois precisam confirmar.";
            UiSfx.Play(UiSound.Back);
            return;
        }

        UiSfx.Play(UiSound.Confirm);
        if (_level is null)
        {
            CoopSession.Pending = _roster;
            SceneRouter.Instance.Push(new SlotScreen(SlotScreen.Mode.Coop));
            return;
        }

        var level = _level;
        SceneRouter.Instance.Pop();
        GetTree().Paused = false;
        SceneRouter.Instance.Pop();
        level.ApplyParty(_roster);
    }

    private void Refresh()
    {
        var split = _roster.KeyboardSplit;
        for (var i = 0; i < _cards.Length; i++)
        {
            var slot = i < _roster.Count ? _roster.Slots[i] : null;
            _cards[i].Show(slot, split, _roster.NeedsCapeTint(i), slot is null ? null : Blurbs.GetValueOrDefault(slot.ClassId, string.Empty));
        }

        _status.Text = _roster.CanStart ? "Tudo pronto! Confirmar de novo (ou Start/Enter) para começar." : string.Empty;
        _status.Modulate = _roster.CanStart ? Palette.Amber : Colors.White;
        _footer.Text = _level is null
            ? "Esc: voltar ao título  ·  B/Esquiva: sair do grupo  ·  ←/→: classe  ·  Ataque/Interagir: confirmar"
            : "Esc / B: voltar à pausa  ·  ←/→: classe  ·  Ataque/Interagir: confirmar";
    }

    private (InputDevice Device, JoinAction Action)? Resolve(InputEvent e)
    {
        switch (e)
        {
            case InputEventKey { Pressed: true, Echo: false } key:
                return ResolveKey(key);
            case InputEventJoypadButton { Pressed: true } button:
                return ResolvePad(InputDevice.Gamepad(button.Device), $"pad:{(int)button.ButtonIndex}", button.ButtonIndex);
            case InputEventJoypadMotion { Axis: JoyAxis.LeftX } motion:
                return ResolveStick(InputDevice.Gamepad(motion.Device), motion.AxisValue);
            default:
                return null;
        }
    }

    private (InputDevice, JoinAction)? ResolveKey(InputEventKey key)
    {
        var physical = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
        if (physical == Key.Escape)
        {
            return (InputDevice.Keyboard, JoinAction.Cancel);
        }

        var code = $"key:{(long)physical}";
        var right = InputSetup.Player2Table.UsesKeyboardCode(code);
        var device = right ? InputDevice.KeyboardRight : InputDevice.Keyboard;
        var table = right ? InputSetup.Player2Table : InputSetup.Table;
        var slots = right ? new[] { BindingSlot.Primary, BindingSlot.Alternate } : new[] { BindingSlot.Primary };
        var action = Match(table, slots, code);

        // Espaço (esquiva) de quem ainda não entrou serve para entrar, não para sair da tela.
        return (device, action == JoinAction.Cancel && _roster.IndexOf(device) < 0 ? JoinAction.Any : action);
    }

    private (InputDevice, JoinAction)? ResolvePad(InputDevice device, string code, JoyButton button)
    {
        if (button == JoyButton.DpadLeft)
        {
            return (device, JoinAction.Left);
        }

        if (button == JoyButton.DpadRight)
        {
            return (device, JoinAction.Right);
        }

        var index = _roster.IndexOf(device);
        var table = InputSetup.TableFor(index < 0 ? _roster.Count : index);
        return (device, Match(table, [BindingSlot.Gamepad], code));
    }

    private (InputDevice, JoinAction)? ResolveStick(InputDevice device, float value)
    {
        if (Mathf.Abs(value) < StickRelease)
        {
            _stickHeld.Remove(device);
            return null;
        }

        if (Mathf.Abs(value) < StickThreshold || !_stickHeld.Add(device) || _roster.IndexOf(device) < 0)
        {
            return null;
        }

        return (device, value < 0 ? JoinAction.Left : JoinAction.Right);
    }

    private static JoinAction Match(BindingTable table, BindingSlot[] slots, string code)
    {
        bool Is(string action)
        {
            foreach (var slot in slots)
            {
                if (table.Get(action, slot) == code)
                {
                    return true;
                }
            }

            return false;
        }

        if (Is(InputActions.MoveLeft))
        {
            return JoinAction.Left;
        }

        if (Is(InputActions.MoveRight))
        {
            return JoinAction.Right;
        }

        if (Is(InputActions.Attack) || Is(InputActions.Interact))
        {
            return JoinAction.Confirm;
        }

        if (Is(InputActions.Dodge))
        {
            return JoinAction.Cancel;
        }

        return Is(InputActions.Pause) ? JoinAction.Start : JoinAction.Any;
    }
}

/// <summary>O que um botão significa na tela de entrada.</summary>
public enum JoinAction
{
    /// <summary>Qualquer outro botão: entra no grupo.</summary>
    Any,
    Left,
    Right,
    Confirm,
    Cancel,
    Start,
}
