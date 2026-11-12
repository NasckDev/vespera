using System.Linq;
using Godot;
using Vespera.Core.Combat;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.Combat;
using Rule = Vespera.Core.Combat.DuoStrike;

namespace Vespera.Game.World.Rift;

/// <summary>
/// Golpe combinado dos Guardiões em dupla (ROTEIRO-V2 §13, ato VII). A regra (quando, quanto tempo de aviso, quanto de dano,
/// o que acerta) está em <see cref="Rule"/>; aqui só o desenho e o dano. Quando os dois se
/// alinham, o chão avisa por 1,3 s: uma faixa âmbar entre os pés dos dois (o elo) e um pulso aos pés de cada um, com um fio
/// magenta ligando os dois Guardiões e um som de sino abafado. Ao fim, quem estiver na faixa ou nos pulsos leva o golpe;
/// sair da zona ou esquivar no instante do golpe basta.
/// </summary>
public partial class GuardianDuoStrike : Node2D
{
    private static readonly Color Link = new("e64a8e");
    private static readonly Color Amber = new("f2a541");
    private const string FirstWarningToast = "Os dois se alinham. Saia do elo entre eles!";

    private readonly LevelWorld _world;
    private readonly Enemy _a;
    private readonly Enemy _b;
    private readonly Rule _rule = new();
    private readonly System.Collections.Generic.List<TelegraphMarker> _markers = [];
    private Vector2 _from;
    private Vector2 _to;
    private float _t;
    private bool _toasted;

    public GuardianDuoStrike(LevelWorld world, Enemy a, Enemy b)
    {
        _world = world;
        _a = a;
        _b = b;
        Name = "GuardianDuoStrike";
        ZIndex = -4;
    }

    /// <summary>O aviso está no chão (QA e bots).</summary>
    public bool Warning => _rule.Warning;

    /// <summary>Quantos golpes combinados já caíram.</summary>
    public int Strikes => _rule.Strikes;

    /// <summary>Quantos caçadores estavam na zona do último golpe, mesmo protegidos pelo tempo intocável (QA e bots).</summary>
    public int LastInZone { get; private set; }

    /// <summary>Quantos caçadores o último golpe acertou (QA e bots).</summary>
    public int LastHits { get; private set; }

    /// <summary>Último ponto A e B do aviso (QA e bots).</summary>
    public (Vector2 From, Vector2 To) Lane => (_from, _to);

    /// <summary>Pula a espera inicial (testes e bots).</summary>
    public void SkipDelay() => _rule.SkipDelay();

    public override void _PhysicsProcess(double delta)
    {
        if (!IsInstanceValid(_a) || !IsInstanceValid(_b))
        {
            return;
        }

        var a = Scale1();
        var input = new DuoInput(
            !_a.Dead,
            !_b.Dead,
            !_a.Dead && !_b.Dead && _a.CanBeHit && _b.CanBeHit,
            _a.GlobalPosition.DistanceTo(_b.GlobalPosition) / a,
            _world.Players.Any(p => IsInstanceValid(p) && !p.Combat.IsDead));
        switch (_rule.Tick(delta, input))
        {
            case DuoEvent.WarningStarted:
                BeginWarning();
                break;
            case DuoEvent.Strike:
                Land();
                break;
            case DuoEvent.Cancelled:
                ReleaseMarkers();
                break;
        }

        if (_rule.Warning)
        {
            _t += (float)delta;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (!_rule.Warning || !IsInstanceValid(_a) || !IsInstanceValid(_b))
        {
            return;
        }

        // O fio liga os dois Guardiões enquanto o aviso corre e fica mais forte perto do golpe.
        var p = Mathf.Clamp(_t / (float)Rule.WarnSeconds, 0f, 1f);
        var pulse = 0.5f + (0.5f * Mathf.Sin(_t * 30f));
        var from = ToLocal(_a.GlobalPosition);
        var to = ToLocal(_b.GlobalPosition);
        DrawLine(from, to, Link with { A = 0.35f + (0.4f * p) }, 3f + (5f * p * pulse), true);
        DrawLine(from, to, Amber with { A = 0.5f + (0.4f * p) }, 1.5f, true);
    }

    private float Scale1() => Mathf.Max(0.01f, _world.Data.ActorScale);

    private void BeginWarning()
    {
        _t = 0f;
        _from = _a.GlobalPosition;
        _to = _b.GlobalPosition;
        var s = Scale1();
        var seconds = (float)Rule.WarnSeconds;
        var dir = (_to - _from).Normalized();
        _markers.Add(Mark(TelegraphMarker.Lane(_from, dir.Angle(), _from.DistanceTo(_to), (float)Rule.LaneWidth * s, seconds)));
        _markers.Add(Mark(TelegraphMarker.Circle(_from, (float)Rule.PulseRadius * s, seconds)));
        _markers.Add(Mark(TelegraphMarker.Circle(_to, (float)Rule.PulseRadius * s, seconds)));
        CombatSfx.Play(this, CombatSound.Telegraph, _from, -2f);
        CombatSfx.Play(this, CombatSound.BossRoar, _to, -8f);
        if (!_toasted)
        {
            _toasted = true;
            _world.Feel.Toast(FirstWarningToast);
        }
    }

    private TelegraphMarker Mark(TelegraphMarker marker)
    {
        _world.AddGroundFx(marker);
        return marker;
    }

    private void ReleaseMarkers()
    {
        foreach (var marker in _markers.Where(IsInstanceValid))
        {
            marker.Release();
        }

        _markers.Clear();
        QueueRedraw();
    }

    /// <summary>O golpe cai na zona marcada no começo do aviso (o chão não anda atrás dos Guardiões).</summary>
    private void Land()
    {
        ReleaseMarkers();
        var s = Scale1();
        var damage = Rule.Damage(_a.Stats.Damage * _a.DamageMult, _b.Stats.Damage * _b.DamageMult)
            * Coop.CoopSession.Rules.BossDamage(Coop.CoopSession.PlayerCount);
        var mid = (_from + _to) / 2f;
        foreach (var at in new[] { _from, _to })
        {
            _world.Feel.Ring(at, (float)Rule.PulseRadius * s, Link, 0.4f, 8f);
            _world.Feel.Spark(at, Link, 16, 300f * s);
        }

        _world.Feel.Spark(mid, Amber, 18, 320f * s, Vector2.Up, 60f);
        _world.Feel.Shake(0.5f);
        CombatSfx.Play(this, CombatSound.BossLand, mid, 0f);
        LastHits = 0;
        LastInZone = 0;
        foreach (var target in _world.Players.Where(p => IsInstanceValid(p) && !p.Combat.IsDead).ToList())
        {
            var c = target.Combat;
            var feet = target.GlobalPosition;
            if (!Rule.Hits(feet.X, feet.Y, _from.X, _from.Y, _to.X, _to.Y, c.HitRadius, s))
            {
                continue;
            }
            LastInZone++;
            if (!c.CanBeHit)
            {
                continue;
            }

            var away = (feet - mid).Normalized();
            if (c.ReceiveHit(new HitInfo(damage, mid, away * 380f * s, 0.08, 1, false, 0)).Landed)
            {
                LastHits++;
            }
        }
    }
}
