using Godot;

namespace Vespera.Game.UI;

/// <summary>Selo do rank E (CLAUDE.md §6: octógono #8A8FA8) com a letra em Pixelify e brilho âmbar fino.</summary>
public partial class RankSealIcon : Control
{
    private static readonly Color Seal = new("8a8fa8");

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        var c = Size / 2f;
        var r = Mathf.Min(Size.X, Size.Y) / 2f;
        var outer = Octagon(c, r);
        var inner = Octagon(c, r * 0.8f);
        DrawColoredPolygon(Octagon(c + new Vector2(0, 4), r), new Color(0, 0, 0, 0.35f));
        DrawColoredPolygon(outer, Palette.Indigo);
        DrawColoredPolygon(inner, Seal.Darkened(0.35f));
        DrawPolyline([.. outer, outer[0]], Seal, 4f, true);
        DrawPolyline([.. inner, inner[0]], Seal.Lightened(0.3f), 2f, true);
        var font = GD.Load<Font>("res://assets/v2/fonts/PixelifySans.ttf");
        const int GlyphSize = 54;
        var w = font.GetStringSize("E", HorizontalAlignment.Left, -1, GlyphSize).X;
        var at = c + new Vector2(-w / 2f, GlyphSize * 0.36f);
        DrawStringOutline(font, at, "E", HorizontalAlignment.Left, -1, GlyphSize, 8, Palette.Indigo);
        DrawString(font, at, "E", HorizontalAlignment.Left, -1, GlyphSize, Palette.Cream);
    }

    private static Vector2[] Octagon(Vector2 c, float r)
    {
        var points = new Vector2[8];
        for (var i = 0; i < 8; i++)
        {
            points[i] = c + (Vector2.FromAngle((Mathf.Tau * i / 8f) + (Mathf.Pi / 8f)) * r);
        }

        return points;
    }
}

/// <summary>Cristal de Fragmento do contador da HUD (mesmo desenho do Fragmento caído).</summary>
public partial class FragmentIcon : Control
{
    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        var c = Size / 2f;
        var s = Size.Y * 0.36f;
        Vector2[] gem = [c + new Vector2(0, -s * 1.3f), c + new Vector2(s * 0.85f, -s * 0.15f), c + new Vector2(0, s * 1.2f), c + new Vector2(-s * 0.85f, -s * 0.15f)];
        var mid = c + new Vector2(0, -s * 0.05f);
        DrawColoredPolygon(gem, new Color("c2367a"));
        DrawColoredPolygon([gem[0], gem[1], mid], new Color("ff8cc4"));
        DrawColoredPolygon([gem[0], mid, gem[3]], new Color("f2a541"));
        DrawPolyline([gem[0], gem[1], gem[2], gem[3], gem[0]], new Color("1b1f3b"), 2.5f, true);
    }
}

/// <summary>Núcleos da Lodo-Mãe ao lado da barra do chefe: âmbar inteiros, apagados quando quebrados.</summary>
public partial class CoreDots : Control
{
    private int _total;
    private int _broken;

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public void Set(int total, int broken)
    {
        if (total == _total && broken == _broken)
        {
            return;
        }

        _total = total;
        _broken = broken;
        QueueRedraw();
    }

    public override void _Draw()
    {
        for (var i = 0; i < _total; i++)
        {
            var c = new Vector2(16 + (i * 32), Size.Y / 2f);
            var intact = i >= _broken;
            DrawCircle(c, 13, Palette.Indigo);
            DrawCircle(c, 9, intact ? Palette.Amber : new Color(0.25f, 0.15f, 0.2f));
            if (intact)
            {
                DrawCircle(c + new Vector2(-3, -3), 3, new Color(1f, 0.95f, 0.8f));
            }

            DrawArc(c, 13, 0, Mathf.Tau, 24, Palette.Cream, 2f, true);
        }
    }
}
