using Godot;
using Vespera.Game.Combat;
using Vespera.Game.World;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Rato-Lanterna (Ato II, rank D): bicho pequeno que anda em enxame e corre em volta do caçador. Verbo:
/// <b>salto-mordida</b>. Aviso: agacha e a barriga acende (contorno âmbar) com um círculo âmbar onde vai cair. Frágil:
/// um golpe derruba; o perigo é o bando.
/// </summary>
public partial class RatoLanterna(LevelWorld world, float sizeFactor = 1f) : Enemy("rato_lanterna", world, sizeFactor)
{
    private const float LeapRange = 260f;
    private const float LeapSeconds = 0.32f;
    private const float RecoverSeconds = 0.5f;
    private const float OrbitDistance = 200f;
    private const float HopHeight = 26f;

    private float _cooldown;
    private float _orbit;
    private Vector2 _from;
    private Vector2 _to;
    private bool _bit;
    private TelegraphMarker? _marker;

    public float StartDelay { get; set; }

    protected override void OnSpawned()
    {
        _cooldown = 0.6f + StartDelay;
        _orbit = GD.Randf() > 0.5f ? 1f : -1f;
        Play("correr");
    }

    protected override Vector2 Think(float dt)
    {
        _cooldown -= dt;
        var target = Target;
        switch (State)
        {
            case EnemyState.Telegraph:
                if (StateTime >= Def.Telegraph)
                {
                    Flash.Tint = 0f;
                    _marker?.Release();
                    _marker = null;
                    _from = GlobalPosition;
                    _bit = false;
                    Enter(EnemyState.Attack);
                    Play("ataque");
                    CombatSfx.Play(this, CombatSound.LodoHop, Vector2.Zero, -8f);
                }

                return Vector2.Zero;
            case EnemyState.Attack:
                return TickLeap();
            case EnemyState.Recover:
            case EnemyState.Hurt:
                if (StateTime >= RecoverSeconds)
                {
                    Enter(EnemyState.Approach);
                    Play("correr");
                }

                return Vector2.Zero;
        }

        if (target is null)
        {
            return Vector2.Zero;
        }

        FaceTowards(target.GlobalPosition);
        var to = target.GlobalPosition - GlobalPosition;
        var distance = to.Length();
        if (_cooldown <= 0f && distance <= LeapRange * A)
        {
            _to = GlobalPosition + to.LimitLength(LeapRange * A);
            _cooldown = (float)Period;
            Enter(EnemyState.Telegraph);
            Play("aviso");
            Flash.Tint = 0.45f;
            _marker = WarnCircle(_to, HitRadius * 1.6f, (float)Def.Telegraph);
            return Vector2.Zero;
        }

        if (Sprite.Animation != "correr")
        {
            Play("correr");
        }

        // Corre em volta (enxame que cerca) e fecha a distância aos poucos.
        var dir = to.Normalized();
        var side = dir.Orthogonal() * _orbit;
        var close = distance > OrbitDistance * A ? 1f : 0.25f;
        return ((dir * close) + (side * 0.8f)).Normalized() * (float)Speed * A;
    }

    private Vector2 TickLeap()
    {
        var t = Mathf.Clamp(StateTime / LeapSeconds, 0f, 1f);
        Sprite.Position = new Vector2(0, -Mathf.Sin(Mathf.Pi * t) * HopHeight * A);
        if (!_bit)
        {
            _bit = TouchPlayer(HitCenter, HitRadius * 1.1f, 1, 160f);
        }

        if (t < 1f)
        {
            return (_to - _from) / LeapSeconds;
        }

        Sprite.Position = Vector2.Zero;
        Enter(EnemyState.Recover);
        Play("parado");
        return Vector2.Zero;
    }

    protected override void OnHurt(in HitInfo hit)
    {
        if (State == EnemyState.Attack)
        {
            return;
        }

        _marker?.Release();
        _marker = null;
        Flash.Tint = 0f;
        Enter(EnemyState.Hurt);
    }

    protected override void OnDied()
    {
        _marker?.Release();
        _marker = null;
    }
}
