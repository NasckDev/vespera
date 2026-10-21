namespace Vespera.Core.World;

/// <summary>
/// Quando um oclusor do layout fica translúcido (DECISIONS D44): o pé do jogador está acima da base do oclusor (ele está
/// atrás) e o corpo dele cai dentro do recorte. Sem alocação: o polígono é lido como lista e o corpo é amostrado em
/// alguns pontos fixos.
/// </summary>
public static class OcclusionRules
{
    /// <summary>Atrás do objeto = pé acima da base (y menor), como no y-sort.</summary>
    public static bool IsBehind(float footY, float baseY) => footY < baseY;

    /// <summary>Retângulo do corpo: largura <paramref name="width"/> centrada no pé, altura <paramref name="height"/> para cima.</summary>
    public static MapRect BodyRect(float footX, float footY, float width, float height) =>
        new(footX - (width / 2f), footY - height, width, height);

    /// <summary>
    /// O recorte cobre o corpo? Primeiro o retângulo envolvente, depois 7 pontos do corpo (cabeça, peito, cintura nos
    /// dois lados e no meio) contra o polígono.
    /// </summary>
    public static bool Covers(IReadOnlyList<MapPoint> polygon, MapRect polygonBounds, MapRect body)
    {
        if (!PolygonMath.Overlaps(polygonBounds, body))
        {
            return false;
        }

        var cx = body.X + (body.Width / 2f);
        var left = body.X + (body.Width * 0.2f);
        var right = body.Right - (body.Width * 0.2f);
        var head = body.Y + (body.Height * 0.12f);
        var chest = body.Y + (body.Height * 0.4f);
        var waist = body.Y + (body.Height * 0.7f);
        return PolygonMath.Contains(polygon, cx, head)
            || PolygonMath.Contains(polygon, cx, chest)
            || PolygonMath.Contains(polygon, cx, waist)
            || PolygonMath.Contains(polygon, left, chest)
            || PolygonMath.Contains(polygon, right, chest)
            || PolygonMath.Contains(polygon, left, waist)
            || PolygonMath.Contains(polygon, right, waist);
    }

    /// <summary>Aproximação exponencial (fade suave e independente do FPS).</summary>
    public static float Approach(float current, float target, float rate, float dt) =>
        target + ((current - target) * MathF.Exp(-rate * dt));
}
