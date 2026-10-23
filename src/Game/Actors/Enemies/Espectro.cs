using Godot;
using Vespera.Game.Combat;
using Vespera.Game.World;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Espectro (GDD §4): vozes que ninguém mais chama. Verbo: <b>persegue</b> flutuando e atravessa paredes e objetos.
/// Perto, segura o aviso âmbar e investe numa estocada reta; depois recua devagar. Golpes o empurram e o fazem
/// tremeluzir. (Dissipar com a Lanterna fica para a fase 2.)
/// </summary>
public partial class Espectro(LevelWorld world) : Enemy("espectro", world)
{
    private const float BobAmplitude = 4f;
    private const float LungeRange = 170f;
    private const float LungeDistance = 230f;
    private const float LungeSeconds = 0.3f;
    private const float RecoverSeconds = 0.75f;
    private const float KeepDistance = 70f;
    private const float StaggerSeconds = 0.2f;

    private float _cooldown = 1.2f;
    private float _bob;
    private Vector2 _lungeDir;
    private bool _lungeHit;

    public override bool PassesWalls => true;

    protected override void OnSpawned()
    {
        Sprite.Modulate = new Color(1f, 1f, 1f, 0.88f);
        _bob = GD.Randf() * Mathf.Tau;
        Play("mover");
    }

    protected override Vector2 Think(float dt)
    {
        _bob += dt * 2.6f;
        var hover = ScaleRegistry.HoverHeight(MonsterId, A * ScaleRules.ReferenceHuman);
        Sprite.Position = new Vector2(0, -hover - (Mathf.Sin(_bob) * BobAmplitude * A));
        _cooldown -= dt;
        var target = Target;
        switch (State)
        {
            case EnemyState.Telegraph:
                if (StateTime >= Def.Telegraph)
                {
                    Flash.Tint = 0f;
                    Enter(EnemyState.Attack);
                    Play("ataque");
                    _lungeHit = false;
                    CombatSfx.Play(this, CombatSound.GhostLunge, Vector2.Zero);
                }

                return Vector2.Zero;
            case EnemyState.Attack:
                if (!_lungeHit)
                {
                    _lungeHit = TouchPlayer(HitCenter, HitRadius * 0.9f, 1, 280f);
                }

                if (StateTime >= LungeSeconds)
                {
                    Enter(EnemyState.Recover);
                    Play("mover");
                }

                return _lungeDir * (LungeDistance / LungeSeconds) * A;
            case EnemyState.Recover:
            case EnemyState.Hurt:
                if (StateTime >= (State == EnemyState.Hurt ? StaggerSeconds : RecoverSeconds))
                {
                    Enter(EnemyState.Approach);
                }

                return target is null ? Vector2.Zero : (GlobalPosition - target.GlobalPosition).Normalized() * (float)Def.Speed * 0.35f * A;
        }

        if (target is null)
        {
            return Vector2.Zero;
        }

        FaceTowards(target.GlobalPosition);
        var to = target.Combat.HitCenter - HitCenter;
        var distance = to.Length();
        if (_cooldown <= 0f && distance <= LungeRange * A)
        {
            _lungeDir = to.Normalized();
            _cooldown = (float)Def.Period;
            Enter(EnemyState.Telegraph);
            Play("aviso");
            Flash.Tint = 0.5f;
            CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero, -6f);
            return Vector2.Zero;
        }

        if (Sprite.Animation != "mover")
        {
            Play("mover");
        }

        var speed = (float)Def.Speed * A;
        return distance > KeepDistance * A ? to.Normalized() * speed : Vector2.Zero;
    }

    protected override void OnHurt(in HitInfo hit)
    {
        if (State == EnemyState.Attack)
        {
            return;
        }

        Flash.Tint = 0f;
        Enter(EnemyState.Hurt);
        Sprite.Modulate = new Color(1f, 1f, 1f, 0.5f);
        var tween = CreateTween();
        tween.TweenProperty(Sprite, "modulate:a", 0.88f, 0.3f);
    }
}
