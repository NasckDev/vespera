using Godot;
using Vespera.Game.Combat;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Golpes no chão dos monstros do Ato II: faixa (raízes em linha) e círculo (maçã que explode). Todo golpe tem aviso
/// âmbar antes (<see cref="TelegraphMarker"/>); aqui só a hora do impacto e o efeito, com partículas já existentes.
/// </summary>
public abstract partial class Enemy
{
    protected static readonly Color RootColor = new("8a5a3c");
    protected static readonly Color RotColor = new("9b4ad0");

    /// <summary>Acerta o caçador se o corpo dele cair na faixa de <paramref name="from"/> até <paramref name="length"/> px.</summary>
    protected bool StrikeLane(Vector2 from, Vector2 dir, float length, float width, double damageMult, float push)
    {
        SparkLane(from, dir, length);
        var target = Target;
        if (target is null || !target.Combat.CanBeHit)
        {
            return false;
        }

        var c = target.Combat;
        var feet = target.GlobalPosition;
        var along = (feet - from).Dot(dir);
        var across = Mathf.Abs((feet - from).Dot(dir.Orthogonal()));
        if (along < -c.HitRadius || along > length + c.HitRadius || across > (width * 0.5f) + c.HitRadius)
        {
            return false;
        }

        var info = new HitInfo(Stats.Damage * damageMult * DamageMult, feet - (dir * 10f), dir * push * A, 0.07, 1, false, 0);
        return c.ReceiveHit(info).Landed;
    }

    /// <summary>Acerta o caçador se os pés dele estiverem a <paramref name="radius"/> do ponto (explosão no chão).</summary>
    protected bool StrikeCircle(Vector2 at, float radius, double damageMult, float push)
    {
        World.Feel.Ring(at, radius, RotColor, 0.4f, 8f * A);
        World.Feel.Spark(at, RotColor, 16, 300f * A);
        var target = Target;
        if (target is null || !target.Combat.CanBeHit)
        {
            return false;
        }

        var c = target.Combat;
        if (target.GlobalPosition.DistanceTo(at) > radius + c.HitRadius)
        {
            return false;
        }

        var dir = (target.GlobalPosition - at).Normalized();
        var info = new HitInfo(Stats.Damage * damageMult * DamageMult, at, dir * push * A, 0.08, 1, false, 0);
        return c.ReceiveHit(info).Landed;
    }

    /// <summary>Raízes brotando ao longo da faixa (terra e lascas).</summary>
    private void SparkLane(Vector2 from, Vector2 dir, float length)
    {
        var steps = Mathf.Max(2, (int)(length / (48f * A)));
        for (var i = 0; i <= steps; i++)
        {
            var at = from + (dir * length * i / steps);
            World.Feel.Spark(at, RootColor, 5, 200f * A, Vector2.Up, 50f);
        }
    }

    /// <summary>Aviso âmbar no chão: faixa.</summary>
    protected TelegraphMarker WarnLane(Vector2 from, Vector2 dir, float length, float width, float seconds)
    {
        var marker = TelegraphMarker.Lane(from, dir.Angle(), length, width, seconds);
        World.AddGroundFx(marker);
        return marker;
    }

    /// <summary>Aviso âmbar no chão: círculo.</summary>
    protected TelegraphMarker WarnCircle(Vector2 at, float radius, float seconds)
    {
        var marker = TelegraphMarker.Circle(at, radius, seconds);
        World.AddGroundFx(marker);
        return marker;
    }
}
