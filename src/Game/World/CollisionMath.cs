using System;

namespace Vespera.Game.World;

/// <summary>Geometria pura do colisor do pé (cápsula deitada) contra retângulos de parede e pegadas.</summary>
public static class CollisionMath
{
    /// <summary>
    /// Verdadeiro se a cápsula horizontal (segmento de <paramref name="x0"/> a <paramref name="x1"/> na altura
    /// <paramref name="y"/>, raio <paramref name="radius"/>) penetra o retângulo mais que <paramref name="tolerance"/>.
    /// </summary>
    public static bool CapsuleHitsRect(float x0, float x1, float y, float radius, RectF rect, float tolerance)
    {
        var dx = MathF.Max(0f, MathF.Max(rect.X - x1, x0 - rect.Right));
        var dy = MathF.Max(0f, MathF.Max(rect.Y - y, y - rect.Bottom));
        var inside = dx == 0f && dy == 0f;
        if (inside)
        {
            return true;
        }

        return MathF.Sqrt((dx * dx) + (dy * dy)) < radius - tolerance;
    }

    /// <summary>Cápsula do pé de um caçador com o pé em (<paramref name="footX"/>, <paramref name="footY"/>).</summary>
    public static (float X0, float X1, float Y, float R) FeetCapsule(float footX, float footY, float halfWidth, float halfHeight)
    {
        var r = halfHeight;
        return (footX - halfWidth + r, footX + halfWidth - r, footY - halfHeight, r);
    }

    /// <summary>Verdadeiro se a cápsula do pé penetra alguma célula bloqueada da grade.</summary>
    public static bool CapsuleHitsGrid(LevelGrid grid, (float X0, float X1, float Y, float R) capsule, float tolerance)
    {
        var c0 = (int)MathF.Floor((capsule.X0 - capsule.R) / grid.Cell);
        var c1 = (int)MathF.Floor((capsule.X1 + capsule.R) / grid.Cell);
        var r0 = (int)MathF.Floor((capsule.Y - capsule.R) / grid.Cell);
        var r1 = (int)MathF.Floor((capsule.Y + capsule.R) / grid.Cell);
        for (var r = r0; r <= r1; r++)
        {
            for (var c = c0; c <= c1; c++)
            {
                if (grid.IsBlocked(c, r) && CapsuleHitsRect(capsule.X0, capsule.X1, capsule.Y, capsule.R, grid.CellRect(c, r), tolerance))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
