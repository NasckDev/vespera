using Godot;
using Vespera.Game.Boot;

namespace Vespera.Game.UI;

/// <summary>
/// Destaque âmbar animado do controle focado: contorno pulsante com brilho suave e dois losangos que balançam nas laterais.
/// Fica como filho do controle e só aparece (e só processa) enquanto ele tem o foco.
/// </summary>
public partial class FocusGlow : Control
{
    private const float PulseSpeed = 4.2f;
    private const float DiamondSize = 9f;
    private const float DiamondGap = 14f;
    private const int GlowLayers = 5;

    private double _time;

    /// <summary>Quanto o contorno avança além dos limites deste controle (para pais que aplicam margens internas).</summary>
    public float Outset { get; set; }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        ShowBehindParent = false;
        Visible = false;
        SetProcess(false);
        var owner = GetParentControl();
        owner.FocusEntered += () => Toggle(true);
        owner.FocusExited += () => Toggle(false);
        owner.VisibilityChanged += () => Toggle(owner.HasFocus());
        InputDevices.Changed += OnDeviceChanged;
    }

    public override void _ExitTree() => InputDevices.Changed -= OnDeviceChanged;

    private void OnDeviceChanged() => QueueRedraw();

    public override void _Process(double delta)
    {
        _time += delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var pulse = 0.5f + (0.5f * Mathf.Sin((float)_time * PulseSpeed));
        var rect = new Rect2(Vector2.Zero, Size).Grow(Outset);
        for (var i = GlowLayers; i >= 1; i--)
        {
            var alpha = (0.05f + (0.05f * pulse)) * (GlowLayers - i + 1) / GlowLayers;
            DrawRect(rect.Grow(i * 3f), new Color(Palette.Amber, alpha), filled: false, width: 3f);
        }

        DrawRect(rect.Grow(1f), new Color(Palette.Amber, 0.7f + (0.3f * pulse)), filled: false, width: 3f);

        if (InputDevices.Current == LastDevice.Mouse)
        {
            return;
        }

        var bob = Mathf.Sin((float)_time * PulseSpeed * 0.5f) * 4f;
        DrawDiamond(new Vector2(-Outset - DiamondGap - bob, Size.Y / 2f));
        DrawDiamond(new Vector2(Size.X + Outset + DiamondGap + bob, Size.Y / 2f));
    }

    private void Toggle(bool on)
    {
        Visible = on;
        SetProcess(on);
        _time = 0;
        QueueRedraw();
    }

    private void DrawDiamond(Vector2 center)
    {
        var points = new[]
        {
            center + new Vector2(0, -DiamondSize), center + new Vector2(DiamondSize, 0),
            center + new Vector2(0, DiamondSize), center + new Vector2(-DiamondSize, 0),
        };
        DrawColoredPolygon(points, Palette.Amber);
        DrawPolyline([.. points, points[0]], Palette.Cream, 2f);
    }
}
