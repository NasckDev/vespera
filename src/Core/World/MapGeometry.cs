namespace Vespera.Core.World;

/// <summary>Ponto em unidades de mundo (u = 1 px da imagem do mapa).</summary>
public readonly record struct MapPoint(float X, float Y);

/// <summary>Retângulo <c>[x, y, w, h]</c> em u. Bordas inclusivas, como <c>inShape</c> (<c>core.js:240</c>).</summary>
public readonly record struct MapRect(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;

    public float Bottom => Y + Height;

    public MapPoint Center => new(X + (Width / 2), Y + (Height / 2));

    public bool Contains(float x, float y) => x >= X && x <= Right && y >= Y && y <= Bottom;
}

/// <summary>Círculo <c>{c: [cx, cy, r]}</c> em u. Borda inclusiva (<c>core.js:240</c>).</summary>
public readonly record struct MapCircle(float X, float Y, float Radius)
{
    public bool Contains(float x, float y)
    {
        var dx = x - X;
        var dy = y - Y;
        return (dx * dx) + (dy * dy) <= Radius * Radius;
    }
}

/// <summary>Forma de área andável: retângulo ou círculo.</summary>
public readonly record struct WalkShape
{
    private WalkShape(MapRect rect, MapCircle? circle)
    {
        Rect = rect;
        Circle = circle;
    }

    /// <summary>Válido quando <see cref="IsCircle"/> é falso.</summary>
    public MapRect Rect { get; }

    public MapCircle? Circle { get; }

    public bool IsCircle => Circle is not null;

    public static WalkShape FromRect(MapRect rect) => new(rect, null);

    public static WalkShape FromCircle(MapCircle circle) => new(default, circle);

    public bool Contains(float x, float y) => Circle is { } c ? c.Contains(x, y) : Rect.Contains(x, y);

    /// <summary>Retângulo que envolve a forma.</summary>
    public MapRect Bounds => Circle is { } c ? new MapRect(c.X - c.Radius, c.Y - c.Radius, c.Radius * 2, c.Radius * 2) : Rect;
}
