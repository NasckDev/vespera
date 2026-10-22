using System;
using Godot;

namespace Vespera.World;

/// <summary>
/// Textos que sobem no mundo ("+3 Lumes", "+1 Madeira de Cais") com ícone do atlas. Pool fixo: nenhum nó é criado depois
/// do <c>_Ready</c>; o mais antigo é reaproveitado se todos estiverem em uso. Sobem 16 u/s e somem em 1,1 s (v2,
/// <c>combat.js:474</c>).
/// </summary>
public partial class FloatingTextPool : Node2D
{
    public const int Size = 12;
    private const float Life = 1.1f;
    private const float Rise = 16f;
    private const float IconHeight = 10f;

    private readonly Entry[] _entries = new Entry[Size];
    private int _next;

    /// <summary>Quantos textos estão visíveis ou esperando agora (para testes).</summary>
    public int Active
    {
        get
        {
            var n = 0;
            foreach (var e in _entries)
            {
                n += e.Active ? 1 : 0;
            }

            return n;
        }
    }

    /// <summary>Último texto mostrado (para testes).</summary>
    public string LastText { get; private set; } = string.Empty;

    public override void _Ready()
    {
        ZIndex = 40;
        for (var i = 0; i < Size; i++)
        {
            var root = new Node2D { Name = $"Popup{i}", Visible = false };
            var icon = new AtlasSprite { Name = "Icon", ScaleClass = SpriteScaleClass.Fixed, TextureFilter = TextureFilterEnum.LinearWithMipmaps };
            var label = new Label
            {
                Name = "Text",
                Size = new Vector2(160, 12),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            label.AddThemeFontSizeOverride("font_size", 9);
            label.AddThemeConstantOverride("outline_size", 3);
            label.AddThemeColorOverride("font_outline_color", new Color(0.05f, 0.03f, 0.08f, 0.9f));
            root.AddChild(icon);
            root.AddChild(label);
            AddChild(root);
            _entries[i] = new Entry(root, icon, label);
        }
    }

    /// <summary>Mostra <paramref name="text"/> subindo de <paramref name="at"/> (u), com o ícone <paramref name="iconKey"/> do atlas (ou nenhum).</summary>
    public void Show(Vector2 at, string text, string? iconKey, Color color, float delay = 0f)
    {
        var e = _entries[_next];
        _next = (_next + 1) % Size;
        e.Active = true;
        e.Delay = delay;
        e.Age = 0f;
        e.Start = at;
        e.Root.Position = at;
        e.Root.Modulate = Colors.White;
        e.Root.Visible = delay <= 0f;
        e.Label.Text = text;
        e.Label.AddThemeColorOverride("font_color", color);
        e.Icon.Visible = iconKey is not null;
        if (iconKey is not null)
        {
            e.Icon.Key = iconKey;
            if (AtlasLibrary.TryGet(iconKey, out var frame))
            {
                var h = MathF.Max(frame.Texture.GetHeight(), frame.Texture.GetWidth());
                e.Icon.SpriteScale = IconHeight * frame.Density / MathF.Max(1f, h);
            }
        }

        // ícone com o pé na linha do texto, à esquerda; texto logo depois
        e.Icon.Position = new Vector2(-4, 1);
        e.Label.Position = new Vector2(iconKey is null ? -12 : 3, -10);
        LastText = text;
    }

    public override void _Process(double delta)
    {
        var dt = MathF.Min(0.05f, (float)delta);
        foreach (var e in _entries)
        {
            if (!e.Active)
            {
                continue;
            }

            if (e.Delay > 0f)
            {
                e.Delay -= dt;
                e.Root.Visible = e.Delay <= 0f;
                continue;
            }

            e.Age += dt;
            if (e.Age >= Life)
            {
                e.Active = false;
                e.Root.Visible = false;
                continue;
            }

            e.Root.Position = e.Start + new Vector2(0, -Rise * e.Age);
            var fade = e.Age > Life * 0.65f ? 1f - ((e.Age - (Life * 0.65f)) / (Life * 0.35f)) : 1f;
            e.Root.Modulate = new Color(1, 1, 1, fade);
        }
    }

    private sealed class Entry(Node2D root, AtlasSprite icon, Label label)
    {
        public Node2D Root { get; } = root;

        public AtlasSprite Icon { get; } = icon;

        public Label Label { get; } = label;

        public Vector2 Start { get; set; }

        public bool Active { get; set; }

        public float Delay { get; set; }

        public float Age { get; set; }
    }
}
