using Godot;

namespace Vespera.Game.Boot;

public enum LastDevice
{
    Keyboard,
    Mouse,
    Pad,
}

/// <summary>
/// Guarda qual dispositivo o jogador usou por último (teclado, mouse ou controle). As telas e as dicas leem isto para
/// mostrar o destaque certo (o mouse destaca o que está sob o cursor; teclado e controle destacam o foco) e para
/// escrever o botão certo ("E" ou "A"). Fica na árvore como nó para escutar todo input sem atrapalhar ninguém.
/// </summary>
public partial class InputDevices : Node
{
    private const float PadAxisThreshold = 0.5f;

    public static LastDevice Current { get; private set; } = LastDevice.Keyboard;

    private const ulong MouseAimMilliseconds = 4000;
    private static ulong _lastMouse;

    /// <summary>O mouse foi usado há pouco (os golpes miram no cursor).</summary>
    public static bool MouseRecently => _lastMouse > 0 && Time.GetTicksMsec() - _lastMouse < MouseAimMilliseconds;

    /// <summary>Coluna das associações a mostrar nas dicas: controle se o último input foi do controle; senão teclado e mouse.</summary>
    public static BindingSlot GlyphSlot => Current == LastDevice.Pad ? BindingSlot.Gamepad : BindingSlot.Primary;

    /// <summary>Avisa a troca de dispositivo (telas redesenham o destaque).</summary>
    public static event System.Action? Changed;

    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

    public override void _Input(InputEvent @event)
    {
        var device = @event switch
        {
            InputEventMouseMotion or InputEventMouseButton => LastDevice.Mouse,
            InputEventKey { Pressed: true } => LastDevice.Keyboard,
            InputEventJoypadButton { Pressed: true } => LastDevice.Pad,
            InputEventJoypadMotion m when Mathf.Abs(m.AxisValue) >= PadAxisThreshold => LastDevice.Pad,
            _ => Current,
        };
        if (@event is InputEventMouseMotion or InputEventMouseButton { Pressed: true })
        {
            _lastMouse = Time.GetTicksMsec();
        }
        else if (device == LastDevice.Pad)
        {
            _lastMouse = 0;
        }

        Set(device);
    }

    /// <summary>Força o dispositivo (testes).</summary>
    public static void Set(LastDevice device)
    {
        if (device == Current)
        {
            return;
        }

        Current = device;
        Changed?.Invoke();
    }

    /// <summary>Nome legível da ação no dispositivo em uso ("E", "A"...).</summary>
    public static string KeyName(string action) =>
        GlyphNames.Describe(InputSetup.Table.Get(action, GlyphSlot), GlyphNames.DetectFamily());
}
