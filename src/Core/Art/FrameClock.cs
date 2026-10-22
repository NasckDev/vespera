namespace Vespera.Core.Art;

/// <summary>
/// Relógio de quadros dos SpriteFrames da arte aprovada (DECISIONS C2-D2): quadro de um laço que avança com a velocidade
/// real do corpo (o pé não escorrega ao acelerar, frear ou levar hitstop) e quadro de uma ação de duração fixa (esquiva,
/// abrir baú). Funções puras, sem alocação.
/// </summary>
public static class FrameClock
{
    /// <summary>
    /// Avança o contador de quadros de um laço: <paramref name="fps"/> na velocidade cheia, proporcional a
    /// <paramref name="speedRatio"/> (|v| / velocidade máxima, 0–1,5). Devolve o contador novo, sempre em [0, <paramref name="count"/>).
    /// </summary>
    public static float Advance(float counter, float dt, float fps, float speedRatio, int count)
    {
        if (count <= 0)
        {
            return 0f;
        }

        var next = counter + (dt * fps * Math.Clamp(speedRatio, 0f, 1.5f));
        return next >= count ? next % count : next;
    }

    /// <summary>Quadro atual de um laço (contador de <see cref="Advance"/>).</summary>
    public static int Loop(float counter, int count) =>
        count <= 0 ? 0 : Math.Clamp((int)MathF.Floor(counter), 0, count - 1);

    /// <summary>Quadro de uma ação de <paramref name="duration"/> s com <paramref name="count"/> quadros, no tempo <paramref name="t"/> (para no último).</summary>
    public static int Once(float t, float duration, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        if (duration <= 0f || t >= duration)
        {
            return count - 1;
        }

        return Math.Clamp((int)MathF.Floor(Math.Max(0f, t) / duration * count), 0, count - 1);
    }
}
