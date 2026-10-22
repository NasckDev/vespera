using System;
using Godot;
using Vespera.Core.Combat;
using Vespera.Core.Data;
using Vespera.Game.Combat;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Núcleo quebrável da Lodo-Mãe (GDD §4: 3 núcleos, cada um quebrado reduz as divisões da fase 3). Um brilho âmbar
/// pulsando sobre o corpo; leva golpe à parte (o Martelo quebra em dobro) e, quebrado, estoura e apaga.
/// </summary>
public partial class BossCore : Node2D, IHittable
{
    private readonly LodoMae _boss;
    private readonly Vector2 _anchor;
    private readonly float _radius;
    private readonly DamageCalculator _damage = new(RiftSession.Db.Rules.Combat);
    private float _pulse;
    private float _flash;

    public BossCore(LodoMae boss, Vector2 anchor, float radius, double hp)
    {
        _boss = boss;
        _anchor = anchor;
        _radius = radius;
        Hp = hp;
        MaxHp = hp;
        ZIndex = 1;
    }

    public double Hp { get; private set; }

    public double MaxHp { get; }

    public bool Broken { get; private set; }

    public Team Team => Team.Monsters;

    public Vector2 HitCenter => GlobalPosition;

    public float HitRadius => _radius;

    public bool CanBeHit => !Broken && _boss.CoresExposed;

    public event Action<BossCore>? Shattered;

    public override void _Ready() => _pulse = GD.Randf() * Mathf.Tau;

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _pulse += dt * 4f;
        _flash = Mathf.Max(0f, _flash - (dt * 6f));
        Position = _boss.CoreLocal(_anchor);
        Visible = _boss.BodyVisible;
        QueueRedraw();
    }

    public HitOutcome ReceiveHit(in HitInfo hit)
    {
        if (!CanBeHit)
        {
            return HitOutcome.Miss;
        }

        var ctx = new MonsterHitContext(PartSide.Any, 0, 0, null, [], []);
        var result = _damage.ToMonster(DamageCalculator.PartDamage((int)Math.Round(hit.Damage), hit.PartMult), ctx, RiftSession.Random);
        Hp = Math.Max(0, Hp - result.Damage);
        _flash = 1f;
        _boss.World.Feel.Number(GlobalPosition, result.Damage.ToString(), new Color("ffd27a"), 0.9f);
        if (Hp > 0)
        {
            return new HitOutcome(result.Damage, false, false, true);
        }

        Broken = true;
        _boss.World.Feel.Spark(GlobalPosition, new Color("ffb347"), 26, 420f);
        _boss.World.Feel.Ring(GlobalPosition + new Vector2(0, _radius * 2f), _radius * 4f, new Color("ffdd8a"), 0.4f, 5f);
        _boss.World.Feel.HitStop(0.1);
        _boss.World.Feel.Shake(0.5f);
        CombatSfx.Play(_boss, CombatSound.CoreBreak, Vector2.Zero);
        Shattered?.Invoke(this);
        return new HitOutcome(result.Damage, true, false, true);
    }

    public override void _Draw()
    {
        var r = _radius;
        if (Broken)
        {
            DrawCircle(Vector2.Zero, r * 0.55f, new Color(0.18f, 0.08f, 0.12f, 0.85f));
            DrawArc(Vector2.Zero, r * 0.55f, 0, Mathf.Tau, 20, new Color(0.45f, 0.2f, 0.18f, 0.9f), 1.5f, true);
            return;
        }

        var beat = 0.5f + (0.5f * Mathf.Sin(_pulse));
        var glow = new Color(1f, 0.62f + (0.2f * _flash), 0.2f + (0.6f * _flash), 0.22f + (0.18f * beat));
        DrawCircle(Vector2.Zero, r * (1.5f + (0.25f * beat)), glow);
        DrawCircle(Vector2.Zero, r * 0.85f, new Color(1f, 0.72f, 0.3f, 0.75f));
        DrawCircle(Vector2.Zero, r * 0.5f, new Color(1f, 0.93f, 0.7f, 0.95f));
        var crack = 1f - (float)(Hp / MaxHp);
        if (crack > 0.05f)
        {
            DrawLine(new Vector2(-r * 0.6f, -r * 0.2f), new Vector2(r * 0.1f * crack, r * 0.3f), new Color(0.3f, 0.1f, 0.05f, crack), 1.6f, true);
            DrawLine(new Vector2(r * 0.5f, -r * 0.5f), new Vector2(-r * 0.1f, r * 0.1f * crack), new Color(0.3f, 0.1f, 0.05f, crack), 1.6f, true);
        }

        DrawArc(Vector2.Zero, r * 0.85f, 0, Mathf.Tau, 24, new Color(0.35f, 0.12f, 0.08f, 0.8f), 1.5f, true);
    }
}
