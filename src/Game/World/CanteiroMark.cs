using Godot;
using Vespera.Core.Horta;
using Vespera.Game.Components;
using Vespera.Game.Hunter;

namespace Vespera.Game.World;

/// <summary>
/// Marca de um canteiro da Horta sobre os canteiros pintados do Pomar Alto: terra revolvida, placa de madeira e a
/// planta (muda enquanto cresce, planta cheia e brilhando quando dá para colher). Placeholder de código: não há arte
/// própria da Horta (ver assets/README.md). Também mantém o aviso de interação (plantar, esperar ou colher).
/// </summary>
public partial class CanteiroMark : Node2D
{
    public const string ZoneKind = "canteiro";

    private const float Top = 8f;
    private static readonly Color Soil = new("3a2414");
    private static readonly Color SoilLight = new("5a3a22");
    private static readonly Color Wood = new("8a5a3c");
    private static readonly Color WoodDark = new("4a2c1a");
    private static readonly Color Glow = new("f2a541");
    private static readonly Color Leaf = new("5c8c4a");

    private readonly Interactable _area;
    private readonly float _scale;
    private (PlotStatus Status, string? Seed, int Left) _key = (PlotStatus.Empty, null, -1);
    private double _clock;

    public CanteiroMark(Interactable area, float actorScale)
    {
        _area = area;
        _scale = actorScale;
        ZIndex = 1;
    }

    public override void _Ready()
    {
        Scale = Vector2.One * _scale;
        Refresh();
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        var plot = Plot;
        var key = (HortaService.StatusOf(_area.Index), plot?.Seed, plot is null ? -1 : HuntService.Horta.DaysLeft(plot));
        if (key != _key)
        {
            Refresh();
        }

        if (_key.Status == PlotStatus.Ready)
        {
            QueueRedraw();
        }
    }

    private PlotState? Plot => _area.Index >= 0 && _area.Index < HuntService.HortaState.Plots.Count ? HuntService.HortaState.Plots[_area.Index] : null;

    private void Refresh()
    {
        var plot = Plot;
        _key = (HortaService.StatusOf(_area.Index), plot?.Seed, plot is null ? -1 : HuntService.Horta.DaysLeft(plot));
        _area.Prompt = HortaService.PlotPrompt(_area.Index);
        QueueRedraw();
    }

    public override void _Draw()
    {
        // Terra revolvida (oval) com a placa de madeira ao lado; a planta nasce da terra.
        var center = new Vector2(0, Top + 14);
        DrawEllipse(Soil, center, 40, 13);
        DrawEllipse(SoilLight, center + new Vector2(0, -2), 32, 8);
        DrawRect(new Rect2(-34, Top + 2, 5, 24), WoodDark);
        DrawRect(new Rect2(-40, Top - 6, 17, 11), Wood);
        DrawRect(new Rect2(-40, Top - 6, 17, 2), WoodDark);
        if (_key.Status == PlotStatus.Empty || _key.Seed is null)
        {
            return;
        }

        var pulse = (float)(0.5 + (0.5 * Mathf.Sin((float)_clock * 4f)));
        var ready = _key.Status == PlotStatus.Ready;
        var color = SeedColor(_key.Seed);
        var height = ready ? 30f : 14f;
        if (ready)
        {
            DrawEllipse(new Color(Glow.R, Glow.G, Glow.B, 0.18f + (0.22f * pulse)), center + new Vector2(0, -14), 46, 34);
        }

        for (var i = -1; i <= 1; i++)
        {
            var x = i * 13f;
            DrawLine(center + new Vector2(x, 2), center + new Vector2(x, -height + (Mathf.Abs(i) * 5f)), Leaf, 3f);
            DrawEllipse(Leaf, center + new Vector2(x - 5, -height * 0.55f), 6, 3);
            DrawEllipse(Leaf, center + new Vector2(x + 5, -height * 0.7f), 6, 3);
            DrawEllipse(ready ? color : color.Darkened(0.35f), center + new Vector2(x, -height + (Mathf.Abs(i) * 5f)), ready ? 7 : 4, ready ? 7 : 4);
        }

        if (!ready)
        {
            var font = ThemeDB.FallbackFont;
            DrawString(font, new Vector2(-40, Top + 4), _key.Left.ToString(), HorizontalAlignment.Center, 17, 14, Colors.White);
        }
    }

    /// <summary>Elipse cheia (o desenho do canteiro é todo feito de formas simples).</summary>
    private void DrawEllipse(Color color, Vector2 center, float rx, float ry)
    {
        const int Steps = 20;
        var points = new Vector2[Steps];
        for (var i = 0; i < Steps; i++)
        {
            var a = Mathf.Tau * i / Steps;
            points[i] = center + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
        }

        DrawColoredPolygon(points, color);
    }

    private static Color SeedColor(string seedId) => seedId switch
    {
        "abobora_roxa" => new Color("9b6bd6"),
        "erva_de_lanterna" => new Color("f2d36a"),
        _ => new Color("e8895a"),
    };
}
