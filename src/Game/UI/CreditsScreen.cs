using Godot;

namespace Vespera.Game.UI;

/// <summary>Créditos: projeto, licenças, fontes e motor.</summary>
public partial class CreditsScreen : UiScreen
{
    private const string Body =
        "Véspera\n" +
        "Um projeto NasckDev\n\n" +
        "Código: licença MIT\n" +
        "Arte, áudio e lore: CC BY-NC-SA 4.0\n\n" +
        "Fontes (SIL Open Font License 1.1)\n" +
        "Pixelify Sans, The Pixelify Sans Project Authors\n" +
        "VT323, The VT323 Project Authors\n\n" +
        "Motor: Godot Engine 4.7 (licença MIT)";

    private Button _back = null!;

    protected override Control? DefaultFocus() => _back;

    protected override void Build()
    {
        var panel = BuildPage("Créditos", out var column, sideMargin: 260);
        var text = UiKit.MakeLabel(Body, UiTheme.ReadingVariation, HorizontalAlignment.Center);
        text.VerticalAlignment = VerticalAlignment.Center;
        panel.AddChild(text);

        _back = UiKit.MakeButton("Voltar", Back, 360);
        _back.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        column.AddChild(_back);
    }
}
