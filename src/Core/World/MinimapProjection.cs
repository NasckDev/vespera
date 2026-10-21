namespace Vespera.Core.World;

/// <summary>
/// Projeção do mapa (u) num retângulo de minimapa (px de interface), mantendo a proporção e centralizando
/// (DECISIONS D47). Pura: o World desenha, o teste confere os cantos.
/// </summary>
public readonly record struct MinimapProjection(float MapWidth, float MapHeight, MapRect Panel)
{
    /// <summary>px de minimapa por u.</summary>
    public float Scale => MathF.Min(Panel.Width / MathF.Max(1f, MapWidth), Panel.Height / MathF.Max(1f, MapHeight));

    /// <summary>Retângulo ocupado pela imagem do mapa dentro do painel (centralizado).</summary>
    public MapRect ImageRect
    {
        get
        {
            var s = Scale;
            var w = MapWidth * s;
            var h = MapHeight * s;
            return new MapRect(Panel.X + ((Panel.Width - w) / 2f), Panel.Y + ((Panel.Height - h) / 2f), w, h);
        }
    }

    public MapPoint ToPanel(float x, float y)
    {
        var r = ImageRect;
        var s = Scale;
        return new MapPoint(r.X + (Math.Clamp(x, 0f, MapWidth) * s), r.Y + (Math.Clamp(y, 0f, MapHeight) * s));
    }

    /// <summary>Retângulo da vista da câmera (u) no painel, cortado nas bordas do mapa.</summary>
    public MapRect ViewToPanel(MapRect view)
    {
        var a = ToPanel(view.X, view.Y);
        var b = ToPanel(view.Right, view.Bottom);
        return new MapRect(a.X, a.Y, b.X - a.X, b.Y - a.Y);
    }
}
