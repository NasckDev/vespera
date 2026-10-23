using System.Collections.Generic;
using System.Linq;
using Godot;
using Vespera.Core.Rift;
using Vespera.Game.Combat;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Lodo-Mãe, Guardiã dos Esgotos do Mercado (GDD §4). Fase 1: pula e esparrama Lodos. Fase 2: alterna o pulo com a
/// investida de engolir (quem é pego fica 2 s dentro dela). Fase 3: divide-se em até três — cada núcleo quebrado tira
/// uma divisão. Todo golpe forte tem 0,6 s de aviso âmbar no chão. Não recua com golpes.
/// </summary>
public partial class LodoMae(LevelWorld world) : Enemy("lodo_mae", world)
{
    private const float IntroSeconds = 1.6f;
    private const float AirSeconds = 0.75f;
    private const float LandRecover = 0.7f;
    private const float HopReach = 520f;
    private const float HopHeight = 120f;
    private const float LaneLength = 430f;
    private const float LungeSeconds = 0.5f;
    private const float SpitRecover = 1.1f;
    private const float SplitSeconds = 0.9f;
    private const float ChildSize = 2.3f;

    private readonly List<BossCore> _cores = [];
    private readonly List<Lodo> _children = [];
    private readonly List<Enemy> _minions = [];
    private Phase _phase = Phase.Intro;
    private TelegraphMarker? _marker;
    private Vector2 _from;
    private Vector2 _to;
    private Vector2 _laneDir;
    private int _moveCount;
    private float _digestTick;
    private bool _splitHandled;

    private enum Phase
    {
        Intro,
        Wait,
        HopAim,
        HopAir,
        HopLand,
        LaneAim,
        Lunge,
        Swallowing,
        Spit,
        Splitting,
        Split,
    }

    public string DisplayName => Def.Name;

    public override bool Unstoppable => true;

    public override bool CanBeHit => base.CanBeHit && _phase is not (Phase.Intro or Phase.Splitting or Phase.Split);

    /// <summary>Núcleos acertáveis (no chão e com o corpo visível).</summary>
    public bool CoresExposed => !Dead && BodyVisible && _phase is not (Phase.HopAir or Phase.Intro);

    public bool BodyVisible => _phase is not (Phase.Split or Phase.Splitting);

    public int CoresBroken => _cores.Count(c => c.Broken);

    public int Stage => RiftSession.Rules.BossPhase(TotalHp, MaxHp);

    /// <summary>Vida somada da mãe e das divisões (para a barra do chefe).</summary>
    public double TotalHp => (_phase != Phase.Split && !Dead ? Hp : 0) + _children.Where(c => !c.Dead).Sum(c => c.Hp);

    public bool Engaged => _phase != Phase.Intro;

    /// <summary>Posição local de um núcleo (âncora em frações da altura do corpo, x espelhado com o sprite).</summary>
    public Vector2 CoreLocal(Vector2 anchor)
    {
        var h = Sheet.StandHeight * Sheet.SpriteScale;
        var x = anchor.X * h * (Sprite.FlipH ? -1f : 1f);
        return new Vector2(x, -anchor.Y * h) + Sprite.Position;
    }

    protected override void OnSpawned()
    {
        var tuning = RiftSession.Brecha.Boss;
        Vector2[] anchors = [new(0.04f, 0.66f), new(-0.2f, 0.4f), new(0.24f, 0.34f)];
        foreach (var anchor in anchors.Take(tuning.Cores))
        {
            var core = new BossCore(this, anchor, 19f * A, tuning.CoreHp);
            core.Shattered += OnCoreShattered;
            AddChild(core);
            World.Combat.Register(core);
            _cores.Add(core);
        }

        Play("parado");
        CombatSfx.Play(this, CombatSound.BossRoar, Vector2.Zero);
        World.Feel.Shake(0.5f);
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        foreach (var core in _cores)
        {
            World.Combat.Unregister(core);
        }
    }

    protected override Vector2 Think(float dt)
    {
        var target = Target;
        if (!_splitHandled && _phase is not (Phase.Intro or Phase.HopAir or Phase.Swallowing) && Stage == 3)
        {
            BeginSplit();
        }

        switch (_phase)
        {
            case Phase.Intro:
                if (StateTime >= IntroSeconds)
                {
                    Go(Phase.Wait);
                }

                return Vector2.Zero;
            case Phase.Wait:
                if (target is not null)
                {
                    FaceTowards(target.GlobalPosition);
                }

                if (StateTime >= WaitSeconds() && target is not null)
                {
                    ChooseMove(target);
                }

                return Vector2.Zero;
            case Phase.HopAim:
                if (StateTime >= Def.Telegraph)
                {
                    Flash.Tint = 0f;
                    Go(Phase.HopAir);
                    Sprite.Frame = 2;
                    Sprite.Play();
                    CombatSfx.Play(this, CombatSound.LodoHop, Vector2.Zero, 4f);
                }

                return Vector2.Zero;
            case Phase.HopAir:
                return TickAir();
            case Phase.HopLand:
            case Phase.Spit:
                if (StateTime >= (_phase == Phase.Spit ? SpitRecover : LandRecover))
                {
                    Go(Phase.Wait);
                    Play("parado");
                }

                return Vector2.Zero;
            case Phase.LaneAim:
                if (StateTime >= Def.Telegraph)
                {
                    Flash.Tint = 0f;
                    _marker?.Release();
                    Go(Phase.Lunge);
                    Play("engolir");
                    CombatSfx.Play(this, CombatSound.Swallow, Vector2.Zero);
                }

                return Vector2.Zero;
            case Phase.Lunge:
                return TickLunge(target);
            case Phase.Swallowing:
                return TickSwallow(dt, target);
            case Phase.Splitting:
                if (StateTime >= SplitSeconds)
                {
                    FinishSplit();
                }

                return Vector2.Zero;
            default:
                CheckChildren();
                return Vector2.Zero;
        }
    }

    protected override void OnDied()
    {
        _marker?.Release();
        foreach (var minion in _minions.Where(m => !m.Dead).ToList())
        {
            minion.DropsLoot = false;
            minion.Die();
        }

        World.Feel.SlowMotion(0.3f, 1.2);
        World.Feel.Shake(0.9f);
        CombatSfx.Play(this, CombatSound.BossRoar, Vector2.Zero, 3f);
    }

    private float WaitSeconds() => (float)Def.Period * (CoresBroken == RiftSession.Brecha.Boss.Cores && Stage == 3 ? 0.4f : 0.55f);

    private void Go(Phase phase)
    {
        _phase = phase;
        Enter(phase switch
        {
            Phase.Wait => EnemyState.Idle,
            Phase.HopAim or Phase.LaneAim => EnemyState.Telegraph,
            Phase.HopAir or Phase.Lunge or Phase.Swallowing => EnemyState.Attack,
            _ => EnemyState.Recover,
        });
    }

    private void ChooseMove(Player target)
    {
        _moveCount++;
        var swallow = Stage >= 2 && _moveCount % 2 == 0;
        FaceTowards(target.GlobalPosition);
        Flash.Tint = 0.3f;
        CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero);
        if (swallow)
        {
            _laneDir = (target.GlobalPosition - GlobalPosition).Normalized();
            var width = HitRadius * 1.6f;
            _marker = TelegraphMarker.Lane(GlobalPosition, _laneDir.Angle(), LaneLength * A, width, (float)Def.Telegraph);
            World.AddGroundFx(_marker);
            Go(Phase.LaneAim);
            Play("engolir");
            Sprite.Pause();
            return;
        }

        _from = GlobalPosition;
        _to = World.ClampToFloor(_from + (target.GlobalPosition - _from).LimitLength(HopReach * A));
        _marker = TelegraphMarker.Circle(_to, LandRadius, (float)Def.Telegraph);
        World.AddGroundFx(_marker);
        Go(Phase.HopAim);
        Play("pulo");
        Sprite.Pause();
        Sprite.Frame = 1;
    }

    private float LandRadius => (float)Def.ContactRadius * A * 1.2f;

    private Vector2 TickAir()
    {
        var t = Mathf.Clamp(StateTime / AirSeconds, 0f, 1f);
        Sprite.Position = new Vector2(0, -Mathf.Sin(Mathf.Pi * t) * HopHeight * A);
        if (t < 1f)
        {
            return (_to - _from) / AirSeconds;
        }

        Sprite.Position = Vector2.Zero;
        _marker?.Release();
        TouchPlayer(GlobalPosition - new Vector2(0, LandRadius * 0.3f), LandRadius, 1, 420f);
        World.Feel.Ring(GlobalPosition, LandRadius * 1.3f, new Color("c06a3a"), 0.45f, 8f);
        World.Feel.Spark(GlobalPosition, new Color("c06a3a"), 24, 380f, Vector2.Up, 150f);
        World.Feel.Shake(0.65f);
        CombatSfx.Play(this, CombatSound.BossLand, Vector2.Zero);
        if (Stage == 1 || _moveCount % 3 == 0)
        {
            SpawnMinions(RiftSession.Brecha.Boss.SpawnPerJump);
        }

        Go(Phase.HopLand);
        return Vector2.Zero;
    }

    private void OnCoreShattered(BossCore core)
    {
        World.Combat.Unregister(core);
        var left = _cores.Count(c => !c.Broken);
        RiftRules.AddMaterial(RiftSession.Bag, RiftSession.Brecha.CoreMaterial, 1);
        World.Feel.Toast(left == 0 ? "Último núcleo partido! Ela não vai mais se dividir." : $"Núcleo partido! Restam {left}.");
    }
}
