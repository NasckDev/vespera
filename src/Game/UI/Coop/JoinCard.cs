using Godot;
using Vespera.Core.Coop;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Game.Combat;
using Vespera.Game.Coop;
using Vespera.Game.World.Rift;

namespace Vespera.Game.UI;

/// <summary>
/// Cartão de um jogador na tela de entrada. Vazio: como entrar, com o ícone e o botão de cada dispositivo. Ocupado:
/// dispositivo, prévia animada do caçador com o contorno da cor do jogador, a classe (◀ ▶), o que ela faz e o estado
/// (confirmar / PRONTO) com os botões daquele dispositivo.
/// </summary>
public partial class JoinCard : PanelContainer
{
    private const float PreviewScale = 2.0f;

    private readonly int _index;
    private readonly Color _color;
    private Label _tag = null!;
    private Label _device = null!;
    private DeviceGlyph _glyph = null!;
    private VBoxContainer _joined = null!;
    private VBoxContainer _empty = null!;
    private AnimatedSprite2D _sprite = null!;
    private HitFlash _flash = null!;
    private Label _className = null!;
    private Label _blurb = null!;
    private Label _state = null!;
    private Label _hint = null!;
    private string _shownClass = string.Empty;

    public JoinCard(int index)
    {
        _index = index;
        _color = CoopSession.ColorOf(index);
        CustomMinimumSize = new Vector2(720, 650);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>Texto do estado (bots e capturas).</summary>
    public string StateText => _state.Text;

    public override void _Ready()
    {
        var column = UiKit.MakeColumn(6);
        AddChild(column);
        var header = UiKit.MakeRow(18);
        header.Alignment = BoxContainer.AlignmentMode.Center;
        column.AddChild(header);
        _tag = UiKit.MakeLabel(CoopSession.TagOf(_index), UiTheme.TitleVariation, HorizontalAlignment.Center);
        _tag.AddThemeColorOverride("font_color", _color);
        header.AddChild(_tag);
        _device = UiKit.MakeLabel(string.Empty, UiTheme.HeadingVariation);
        _device.VerticalAlignment = VerticalAlignment.Center;
        header.AddChild(_device);

        _joined = UiKit.MakeColumn(4);
        column.AddChild(_joined);
        _glyph = new DeviceGlyph(new Vector2(170, 76)) { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        _joined.AddChild(_glyph);
        var stage = new Control { CustomMinimumSize = new Vector2(680, 250), MouseFilter = MouseFilterEnum.Ignore };
        _sprite = new AnimatedSprite2D { Position = new Vector2(340, 246), Offset = HunterSprites.FootOffset, Scale = Vector2.One * PreviewScale };
        stage.AddChild(_sprite);
        _flash = new HitFlash(_sprite);
        _joined.AddChild(stage);
        _className = UiKit.MakeLabel(string.Empty, UiTheme.HeadingVariation, HorizontalAlignment.Center);
        _joined.AddChild(_className);
        _blurb = UiKit.MakeLabel(string.Empty, null, HorizontalAlignment.Center);
        _joined.AddChild(_blurb);
        _state = UiKit.MakeLabel(string.Empty, UiTheme.HeadingVariation, HorizontalAlignment.Center);
        _joined.AddChild(_state);
        _hint = UiKit.MakeLabel(string.Empty, UiTheme.DimVariation, HorizontalAlignment.Center);
        _joined.AddChild(_hint);

        _empty = UiKit.MakeColumn(14);
        _empty.SizeFlagsVertical = SizeFlags.ExpandFill;
        _empty.Alignment = BoxContainer.AlignmentMode.Center;
        column.AddChild(_empty);
        _empty.AddChild(UiKit.MakeLabel("Aperte um botão para entrar", UiTheme.HeadingVariation, HorizontalAlignment.Center));
        AddWay(DeviceKind.Keyboard, false, "Teclado", $"{Key(InputSetup.Table, InputActions.Attack, BindingSlot.Primary)} ou {Key(InputSetup.Table, InputActions.Dodge, BindingSlot.Primary)}");
        AddWay(DeviceKind.KeyboardRight, true, "Teclado (direita)", $"{Key(InputSetup.Player2Table, InputActions.Attack, BindingSlot.Primary)} ou {Key(InputSetup.Player2Table, InputActions.Interact, BindingSlot.Alternate)}");
        AddWay(DeviceKind.Gamepad, false, "Controle", Key(InputSetup.TableFor(_index), InputActions.Interact, BindingSlot.Gamepad));
    }

    public void Show(RosterSlot? slot, bool split, bool capeTint, string? blurb)
    {
        var joined = slot is not null;
        _joined.Visible = joined;
        _empty.Visible = !joined;
        AddThemeStyleboxOverride("panel", UiTheme.RowFrame(slot?.Ready == true));
        Modulate = joined ? Colors.White : new Color(0.82f, 0.82f, 0.9f);
        _tag.Modulate = joined ? Colors.White : new Color(1, 1, 1, 0.55f);
        if (slot is null)
        {
            _device.Text = "livre";
            return;
        }

        var device = slot.Device;
        _device.Text = CoopSession.DeviceName(device, split);
        _glyph.Set(device.Kind, split, _color);
        var hunter = RiftSession.HunterFor(slot.ClassId);
        if (_shownClass != slot.ClassId)
        {
            _shownClass = slot.ClassId;
            _sprite.SpriteFrames = HunterSprites.Build(hunter);
            _sprite.Play(HunterSprites.IdleAnimation(Facing.Down));
        }

        _flash.Outline = 0.95f;
        _flash.OutlineColor = _color;
        _flash.OutlineWidth = 3f;
        _flash.Cape = capeTint ? 0.85f : 0f;
        _flash.Update(0f);
        _className.Text = slot.Ready || slot.Locked ? HunterSprites.DisplayName(hunter) : $"◀   {HunterSprites.DisplayName(hunter)}   ▶";
        _blurb.Text = (blurb ?? string.Empty) + (capeTint ? "\n(mesma classe: sua capa ganha outro tom)" : string.Empty);
        var table = InputSetup.TableFor(device.Kind == DeviceKind.KeyboardRight ? 1 : _index);
        var slotCol = device.Kind == DeviceKind.Gamepad ? BindingSlot.Gamepad : BindingSlot.Primary;
        var confirm = Key(table, InputActions.Attack, slotCol);
        var back = device.Kind == DeviceKind.Keyboard ? "Esc" : Key(table, InputActions.Dodge, slotCol);
        var left = device.Kind == DeviceKind.Gamepad ? "◀" : Key(table, InputActions.MoveLeft, slotCol);
        var right = device.Kind == DeviceKind.Gamepad ? "▶" : Key(table, InputActions.MoveRight, slotCol);
        if (slot.Ready)
        {
            _state.Text = "PRONTO!";
            _state.AddThemeColorOverride("font_color", _color);
            _hint.Text = slot.Locked ? "já está na caçada" : $"{back}: mudar de ideia";
        }
        else
        {
            _state.Text = $"{confirm}: confirmar";
            _state.AddThemeColorOverride("font_color", Palette.Cream);
            _hint.Text = $"{left} / {right}: classe   ·   {back}: sair";
        }
    }

    private void AddWay(DeviceKind kind, bool split, string name, string keys)
    {
        var row = UiKit.MakeRow(16);
        row.Alignment = BoxContainer.AlignmentMode.Center;
        var glyph = new DeviceGlyph(new Vector2(110, 52));
        glyph.Set(kind, split, _color);
        row.AddChild(glyph);
        var label = UiKit.MakeLabel($"{name}: {keys}", null);
        label.CustomMinimumSize = new Vector2(440, 0);
        row.AddChild(label);
        _empty.AddChild(row);
    }

    private static string Key(BindingTable table, string action, BindingSlot slot) =>
        GlyphNames.Describe(table.Get(action, slot), GlyphNames.DetectFamily());
}
