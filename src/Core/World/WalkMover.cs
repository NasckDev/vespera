using Vespera.Core.Common;

namespace Vespera.Core.World;

/// <summary>
/// Movimento de quem não é o jogador (NPCs, caçadores ambiente), igual a <c>moveEnt</c>/<c>randPoint</c>
/// (<c>core.js:249-264</c>): passos de no máximo 3 u, X e depois Y testados com <see cref="WalkAreaExtensions.CanStand"/>,
/// então desliza nas paredes e nunca entra em área bloqueada.
/// </summary>
public static class WalkMover
{
    /// <summary>Tamanho máximo de cada sub-passo.</summary>
    public const float MaxStep = 3f;

    /// <summary>Raio de colisão de NPCs e caçadores: <c>min(6, r·0,5)</c> com r = 8.</summary>
    public const float NpcRadius = 4f;

    /// <summary>Move de (<paramref name="x"/>, <paramref name="y"/>) por (dx, dy) deslizando nas paredes.</summary>
    public static (float X, float Y) Move(IWalkArea area, float x, float y, float dx, float dy, float radius = NpcRadius)
    {
        ArgumentNullException.ThrowIfNull(area);
        var steps = Math.Max(1, (int)MathF.Ceiling(MathF.Max(MathF.Abs(dx), MathF.Abs(dy)) / MaxStep));
        var sx = dx / steps;
        var sy = dy / steps;
        for (var i = 0; i < steps; i++)
        {
            if (area.CanStand(x + sx, y, radius))
            {
                x += sx;
            }

            if (area.CanStand(x, y + sy, radius))
            {
                y += sy;
            }
        }

        return (x, y);
    }

    /// <summary>
    /// <c>randPoint(minD, {r})</c>: até 400 tentativas no retângulo do mapa (margem 10) onde dá para ficar de pé e a
    /// pelo menos <paramref name="minDistance"/> de (<paramref name="awayX"/>, <paramref name="awayY"/>).
    /// Falhou: <c>(awayX, awayY - 60)</c>, como a v2.
    /// </summary>
    public static MapPoint RandomPoint(IWalkArea area, float mapWidth, float mapHeight, IRandom random, float awayX, float awayY, float minDistance = 0f, float radius = 8f)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(random);
        for (var i = 0; i < 400; i++)
        {
            var x = (float)random.Range(10, mapWidth - 10);
            var y = (float)random.Range(10, mapHeight - 10);
            if (!area.CanStand(x, y, radius))
            {
                continue;
            }

            if (minDistance > 0 && Hypot(x - awayX, y - awayY) < minDistance)
            {
                continue;
            }

            return new MapPoint(x, y);
        }

        return new MapPoint(awayX, awayY - 60);
    }

    /// <summary>O segmento inteiro (amostrado a cada 2 u) passa só por pontos onde dá para ficar de pé.</summary>
    public static bool IsClear(IWalkArea area, MapPoint a, MapPoint b, float radius = NpcRadius)
    {
        ArgumentNullException.ThrowIfNull(area);
        var length = Hypot(b.X - a.X, b.Y - a.Y);
        var samples = Math.Max(1, (int)MathF.Ceiling(length / 2f));
        for (var i = 0; i <= samples; i++)
        {
            var t = i / (float)samples;
            if (!area.CanStand(a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t), radius))
            {
                return false;
            }
        }

        return true;
    }

    public static float Hypot(float dx, float dy) => MathF.Sqrt((dx * dx) + (dy * dy));
}
