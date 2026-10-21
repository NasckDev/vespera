namespace Vespera.Core.World;

/// <summary>
/// Aceleração e frenagem curtas do jogador (DECISIONS D42). A v2 trocava a velocidade na hora (<c>combat.js:78-92</c>),
/// o que lia como "travado"; aqui a velocidade chega ao alvo em <c>accelSeconds</c> e para em <c>decelSeconds</c>,
/// sem nunca passar da velocidade máxima (o deslize de quina e os testes de colisão dependem disso).
/// </summary>
public static class Locomotion
{
    /// <summary>
    /// Um passo de velocidade: aproxima <paramref name="velocity"/> do alvo (<paramref name="input"/> × <paramref name="maxSpeed"/>)
    /// a <c>maxSpeed / accelSeconds</c> u/s² com input, ou <c>maxSpeed / decelSeconds</c> sem input. Tempos ≤ 0 = instantâneo.
    /// </summary>
    public static MapPoint Step(MapPoint velocity, MapPoint input, float maxSpeed, float accelSeconds, float decelSeconds, float dt)
    {
        var target = new MapPoint(input.X * maxSpeed, input.Y * maxSpeed);
        var hasInput = input.X != 0f || input.Y != 0f;
        var seconds = hasInput ? accelSeconds : decelSeconds;
        if (seconds <= 0f || maxSpeed <= 0f)
        {
            return target;
        }

        var maxDelta = maxSpeed / seconds * dt;
        var dx = target.X - velocity.X;
        var dy = target.Y - velocity.Y;
        var distance = MathF.Sqrt((dx * dx) + (dy * dy));
        if (distance <= maxDelta || distance < 1e-5f)
        {
            return target;
        }

        var k = maxDelta / distance;
        return new MapPoint(velocity.X + (dx * k), velocity.Y + (dy * k));
    }

    /// <summary>Fração da velocidade máxima (0–1) para misturar parado ↔ andando na animação.</summary>
    public static float SpeedFraction(MapPoint velocity, float maxSpeed) =>
        maxSpeed <= 0f ? 0f : Math.Clamp(MathF.Sqrt((velocity.X * velocity.X) + (velocity.Y * velocity.Y)) / maxSpeed, 0f, 1f);
}
