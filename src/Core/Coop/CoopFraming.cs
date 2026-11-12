using System.Numerics;

namespace Vespera.Core.Coop;

/// <summary>Enquadramento da câmera compartilhada: centro (mundo) e fator do zoom da regra (1 = 110 px de caçador).</summary>
public readonly record struct Framing(Vector2 Center, float ZoomFactor, bool AtFloor);

/// <summary>
/// Matemática da câmera compartilhada e da coleira do co-op (PLANO-V2 camada 8). A câmera centra no grupo e só abre o zoom
/// o necessário para todos caberem com folga, até o piso (caçador ≥ 85 px na tela). Passado o piso, a coleira segura
/// quem tenta sair: é uma borda invisível relativa aos outros jogadores (ninguém arrasta ninguém) e, se alguém já estiver
/// além dela (recuo, teleporte), um puxão suave o traz de volta.
/// </summary>
public static class CoopFraming
{
    /// <param name="points">Pontos a enquadrar (o peito de cada caçador), em px de mundo.</param>
    /// <param name="viewport">Tamanho da tela de jogo em px (1920×1080).</param>
    /// <param name="baseZoom">Zoom da regra (caçador com 110 px).</param>
    /// <param name="minFactor">Piso do fator (85 ÷ 110).</param>
    /// <param name="margin">Folga até a borda, em px de mundo.</param>
    public static Framing Frame(IReadOnlyList<Vector2> points, Vector2 viewport, float baseZoom, float minFactor, float margin)
    {
        if (points.Count == 0 || baseZoom <= 0)
        {
            return new Framing(Vector2.Zero, 1f, false);
        }

        var (min, max) = Bounds(points);
        var span = max - min;
        var need = span + new Vector2(2f * margin);
        var fx = need.X <= 0 ? 1f : viewport.X / (baseZoom * need.X);
        var fy = need.Y <= 0 ? 1f : viewport.Y / (baseZoom * need.Y);
        var wanted = MathF.Min(1f, MathF.Min(fx, fy));
        var factor = Math.Clamp(wanted, minFactor, 1f);
        return new Framing((min + max) / 2f, factor, wanted < minFactor);
    }

    /// <summary>Maior distância entre dois jogadores (por eixo, em px de mundo) que ainda cabe na tela no piso do zoom.</summary>
    public static Vector2 MaxSpan(Vector2 viewport, float baseZoom, float minFactor, float margin)
    {
        var view = viewport / (baseZoom * minFactor);
        return Vector2.Max(Vector2.Zero, view - new Vector2(2f * margin));
    }

    /// <summary>
    /// Ajusta a velocidade de um jogador para ele não abrir o grupo além de <paramref name="maxSpan"/>: a velocidade que
    /// sairia do limite para na borda; já fora dele, não deixa ir mais longe e puxa de volta (<paramref name="pull"/> por segundo,
    /// no máximo <paramref name="maxPull"/> px/s: um puxão suave, não um tranco).
    /// </summary>
    public static Vector2 Constrain(Vector2 position, Vector2 velocity, IReadOnlyList<Vector2> others, Vector2 maxSpan, float pull, float dt,
        float maxPull = float.MaxValue)
    {
        if (others.Count == 0 || dt <= 0)
        {
            return velocity;
        }

        var (min, max) = Bounds(others);
        return new Vector2(
            Axis(position.X, velocity.X, max.X - maxSpan.X, min.X + maxSpan.X, pull, dt, maxPull),
            Axis(position.Y, velocity.Y, max.Y - maxSpan.Y, min.Y + maxSpan.Y, pull, dt, maxPull));
    }

    /// <summary>Está além do limite em algum eixo (para mostrar o aviso de coleira).</summary>
    public static bool Exceeds(Vector2 position, IReadOnlyList<Vector2> others, Vector2 maxSpan, float tolerance = 1f)
    {
        if (others.Count == 0)
        {
            return false;
        }

        var (min, max) = Bounds(others);
        return position.X > min.X + maxSpan.X + tolerance || position.X < max.X - maxSpan.X - tolerance
            || position.Y > min.Y + maxSpan.Y + tolerance || position.Y < max.Y - maxSpan.Y - tolerance;
    }

    private static float Axis(float p, float v, float lo, float hi, float pull, float dt, float maxPull)
    {
        if (lo > hi)
        {
            // Os outros já estão mais afastados que o limite entre si: o meio deles é o único lugar permitido.
            var mid = (lo + hi) / 2f;
            lo = mid;
            hi = mid;
        }

        if (p > hi)
        {
            return MathF.Max(MathF.Min(v, 0f) - ((p - hi) * pull), -MathF.Max(maxPull, -MathF.Min(v, 0f)));
        }

        if (p < lo)
        {
            return MathF.Min(MathF.Max(v, 0f) + ((lo - p) * pull), MathF.Max(maxPull, MathF.Max(v, 0f)));
        }

        var next = p + (v * dt);
        if (next > hi)
        {
            return (hi - p) / dt;
        }

        return next < lo ? (lo - p) / dt : v;
    }

    private static (Vector2 Min, Vector2 Max) Bounds(IReadOnlyList<Vector2> points)
    {
        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        foreach (var p in points)
        {
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }

        return (min, max);
    }
}
