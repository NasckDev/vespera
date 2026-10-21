using System.Collections.Generic;
using Godot;

namespace Vespera.Debug;

/// <summary>Desenho de apoio da <see cref="ObjectGallery"/>: chão de cada fileira, footprint, âncora e luz sugerida.</summary>
public partial class GalleryOverlay : Node2D
{
    private static readonly Color Ground = new(0.32f, 0.3f, 0.42f, 1f);
    private static readonly Color Foot = new(0.32f, 0.88f, 0.77f, 0.85f);
    private static readonly Color Anchor = new(1f, 0.92f, 0.5f, 1f);

    /// <summary>Âncora (pé), footprint e luz de um objeto, em u.</summary>
    public readonly record struct Mark(Vector2 Foot, Rect2? Footprint, Vector2? Light, Color LightColor);

    public List<Mark> Marks { get; set; } = [];

    public List<float> Baselines { get; set; } = [];

    public float Width { get; set; } = 711f;

    public override void _Draw()
    {
        foreach (var y in Baselines)
        {
            DrawLine(new Vector2(4, y), new Vector2(Width - 4, y), Ground, 0.6f);
        }

        foreach (var m in Marks)
        {
            if (m.Footprint is { } f)
            {
                DrawRect(f, Foot, filled: false, width: 0.6f);
            }

            DrawCircle(m.Foot, 0.9f, Anchor);
            if (m.Light is { } l)
            {
                DrawArc(l, 2.5f, 0, Mathf.Tau, 16, m.LightColor, 0.7f);
            }
        }
    }
}
