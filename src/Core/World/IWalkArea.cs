namespace Vespera.Core.World;

/// <summary>
/// Área andável de um mapa em unidades de mundo. Implementada por <see cref="MaskWalkArea"/> (mapas hd, máscara)
/// e <see cref="ShapeWalkArea"/> (mapas com formas <c>walk</c>/<c>block</c>). Mesma ideia do <see cref="WalkGrid"/>,
/// mas em u em vez de células.
/// </summary>
public interface IWalkArea
{
    /// <summary>Largura da área coberta (u). Fora de [0, Width] × [0, Height] é bloqueado.</summary>
    float Width { get; }

    float Height { get; }

    /// <summary>Teste de ponto (<c>walkable</c> de <c>core.js:242</c>, sem props).</summary>
    bool IsWalkable(float x, float y);

    /// <summary>
    /// Retângulos que cobrem toda a área bloqueada (com uma moldura em volta), para gerar colisores estáticos.
    /// Garantia: todo ponto não andável dentro da moldura está dentro de algum retângulo.
    /// </summary>
    IReadOnlyList<MapRect> BlockedRects();
}

/// <summary>Regras do protótipo derivadas do teste de ponto.</summary>
public static class WalkAreaExtensions
{
    /// <summary>Raio de colisão do jogador (<c>combat.js:3</c>).</summary>
    public const float PlayerRadius = 4f;

    /// <summary>Moldura bloqueada em volta das áreas, em u.</summary>
    public const float FrameMargin = 16f;

    /// <summary><c>canStand</c> (<c>core.js:248</c>): 4 sondas em (x±r, y) e (x, y±0,6r).</summary>
    public static bool CanStand(this IWalkArea area, float x, float y, float radius = PlayerRadius) =>
        area.IsWalkable(x - radius, y)
        && area.IsWalkable(x + radius, y)
        && area.IsWalkable(x, y - (radius * 0.6f))
        && area.IsWalkable(x, y + (radius * 0.6f));

    /// <summary>
    /// Ponto mais próximo (distância euclidiana, amostrado em passos de <paramref name="step"/> u) onde dá para ficar
    /// de pé. Usado para corrigir spawns e chegadas em cima de parede (DECISIONS D6). Null se nada em
    /// <paramref name="maxDistance"/>.
    /// </summary>
    public static MapPoint? NearestWalkable(this IWalkArea area, float x, float y, float radius = PlayerRadius, float maxDistance = 96f, float step = 2f)
    {
        ArgumentNullException.ThrowIfNull(area);
        if (area.CanStand(x, y, radius))
        {
            return new MapPoint(x, y);
        }

        var maxRing = (int)MathF.Ceiling(maxDistance / step);
        MapPoint? best = null;
        var bestDistanceSq = float.MaxValue;
        var stopRing = maxRing;
        for (var ring = 1; ring <= stopRing; ring++)
        {
            for (var dy = -ring; dy <= ring; dy++)
            {
                for (var dx = -ring; dx <= ring; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring)
                    {
                        continue;
                    }

                    var px = x + (dx * step);
                    var py = y + (dy * step);
                    var distanceSq = (dx * dx * step * step) + (dy * dy * step * step);
                    if (distanceSq >= bestDistanceSq || distanceSq > maxDistance * maxDistance || !area.CanStand(px, py, radius))
                    {
                        continue;
                    }

                    bestDistanceSq = distanceSq;
                    best = new MapPoint(px, py);
                }
            }

            if (best is not null && stopRing == maxRing)
            {
                // Um anel mais externo ainda pode ter um ponto mais perto (cantos do quadrado): procura até ring·√2.
                stopRing = Math.Min(maxRing, (int)MathF.Ceiling(ring * MathF.Sqrt(2)));
            }
        }

        return best;
    }
}
