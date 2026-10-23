using System;
using System.Collections.Generic;
using Godot;

namespace Vespera.Game.UI;

/// <summary>
/// Números de dano por cima do mundo (fora do SubViewport, para o texto sair nítido): nascem no ponto do golpe,
/// saltam, sobem e somem. A posição na tela vem de uma função mundo→tela da fase.
/// </summary>
public partial class FloatingNumbers : Control
{
    private const float Life = 0.85f;
    private const int BaseSize = 58;

    private readonly Func<Vector2, Vector2> _toScreen;
    private readonly List<Entry> _entries = [];
    private Font _font = null!;

    public FloatingNumbers(Func<Vector2, Vector2> toScreen)
    {
        _toScreen = toScreen;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _font = GD.Load<Font>("res://assets/v2/fonts/VT323-Regular.ttf");
    }

    public void Spawn(Vector2 world, string text, Color color, float size)
    {
        var jitter = new Vector2((float)GD.RandRange(-14.0, 14.0), 0);
        _entries.Add(new Entry(world, text, color, size, jitter));
        if (_entries.Count > 40)
        {
            _entries.RemoveAt(0);
        }
    }

    public void Clear() => _entries.Clear();

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            _entries[i].Age += dt;
            if (_entries[i].Age >= Life)
            {
                _entries.RemoveAt(i);
            }
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var e in _entries)
        {
            var k = e.Age / Life;
            var pop = k < 0.12f ? 0.7f + (2.5f * k) : 1f;
            var size = (int)(BaseSize * e.Size * pop);
            var rise = (Mathf.Sin(Mathf.Min(1f, k * 2.2f) * Mathf.Pi * 0.5f) * 70f) + (k * 20f);
            var screen = _toScreen(e.World) + e.Jitter - new Vector2(0, rise);
            var alpha = k > 0.6f ? 1f - ((k - 0.6f) / 0.4f) : 1f;
            var width = _font.GetStringSize(e.Text, HorizontalAlignment.Left, -1, size).X;
            var at = screen - new Vector2(width / 2f, 0);
            DrawStringOutline(_font, at, e.Text, HorizontalAlignment.Left, -1, size, Mathf.Max(4, size / 7), new Color(0.08f, 0.05f, 0.12f, alpha));
            DrawString(_font, at, e.Text, HorizontalAlignment.Left, -1, size, e.Color with { A = alpha });
        }
    }

    private sealed class Entry(Vector2 world, string text, Color color, float size, Vector2 jitter)
    {
        public Vector2 World { get; } = world;

        public string Text { get; } = text;

        public Color Color { get; } = color;

        public float Size { get; } = size;

        public Vector2 Jitter { get; } = jitter;

        public float Age { get; set; }
    }
}
