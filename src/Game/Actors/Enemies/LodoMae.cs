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

    public bool BodyVisible => _phase is not Phase.Split;

    public int CoresBroken => _cores.Count(c => c.Broken);

    public int Stage => RiftSession.Rules.BossPhase(TotalHp, MaxHp);

    /// <summary>Vida somada da mãe e das divisões (para a barra do chefe).</summary>
    public double TotalHp => (BodyVisible && !Dead ? Hp : 0) + _children.Where(c => !c.Dead).Sum(c => c.Hp);

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
            var core = new BossCore(this, anchor, 15f * A, tuning.CoreHp);
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
        Flash.Tint = 0.45f;
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

    private Vector2 TickLunge(Player? target)
    {
        if (target is not null && target.Combat.HitCenter.DistanceTo(MouthPoint) < HitRadius * 0.75f && target.Combat.TrySwallow())
        {
            Go(Phase.Swallowing);
            _digestTick = 0f;
            Sprite.Play("parado");
            World.Feel.Shake(0.4f);
            World.Feel.Toast("Engolido! Aguente firme…");
            return Vector2.Zero;
        }

        if (StateTime >= LungeSeconds)
        {
            Go(Phase.Spit);
            Play("parado");
            return Vector2.Zero;
        }

        var ease = 1.3f - StateTime / LungeSeconds;
        return _laneDir * (LaneLength * A / LungeSeconds) * ease;
    }

    private Vector2 MouthPoint => HitCenter + (_laneDir * HitRadius * 0.6f);

    private Vector2 TickSwallow(float dt, Player? target)
    {
        var tuning = RiftSession.Brecha.Boss;
        var hunter = World.Player;
        hunter.Teleport(GlobalPosition + new Vector2(0, 2f));
        _digestTick += dt;
        Sprite.Scale = Vector2.One * Sheet.SpriteScale * (1f + (0.04f * Mathf.Sin(StateTime * 18f)));
        if (_digestTick >= 0.5f)
        {
            _digestTick = 0f;
            hunter.Combat.Digest((int)(tuning.SwallowDamagePerSecond * 0.5));
            Flash.Pulse(new Color(1f, 0.6f, 0.3f), 0.02f, 0.2f, 0.5f);
        }

        if (StateTime >= tuning.SwallowSeconds || hunter.Combat.IsDead)
        {
            Sprite.Scale = Vector2.One * Sheet.SpriteScale;
            var away = (_laneDir == Vector2.Zero ? Vector2.Down : _laneDir) * HitRadius * 1.4f;
            hunter.Teleport(World.ClampToFloor(GlobalPosition + away));
            hunter.Combat.Release(_laneDir * 520f * A);
            CombatSfx.Play(this, CombatSound.Split, Vector2.Zero, -4f);
            Go(Phase.Spit);
        }

        return Vector2.Zero;
    }

    private void SpawnMinions(int count)
    {
        _minions.RemoveAll(m => !IsInstanceValid(m) || m.Dead);
        var max = RiftSession.Brecha.Boss.MaxMinions;
        for (var i = 0; i < count && _minions.Count < max; i++)
        {
            var angle = (Mathf.Tau * i / count) + (GD.Randf() * 0.8f);
            var at = World.ClampToFloor(GlobalPosition + (Vector2.FromAngle(angle) * HitRadius * 1.5f));
            var lodo = new Lodo(World) { StartDelay = 0.5f + (0.3f * i), Position = at };
            World.AddEnemy(lodo);
            _minions.Add(lodo);
        }
    }

    private void BeginSplit()
    {
        _splitHandled = true;
        _marker?.Release();
        if (World.Player.Combat.State == HunterState.Swallowed)
        {
            World.Player.Combat.Release(Vector2.Down * 400f * A);
        }

        Sprite.Position = Vector2.Zero;
        Sprite.Scale = Vector2.One * Sheet.SpriteScale;
        Flash.Tint = 0f;
        var splits = RiftSession.Rules.SplitCount(CoresBroken);
        if (splits == 0)
        {
            _phase = Phase.Wait;
            World.Feel.Toast("Sem núcleos, a Lodo-Mãe não consegue se dividir!");
            _moveCount = 1;
            return;
        }

        Go(Phase.Splitting);
        Play("dividir");
        CombatSfx.Play(this, CombatSound.Split, Vector2.Zero, 3f);
        World.Feel.Toast(splits == 1 ? "A Lodo-Mãe se contorce…" : $"A Lodo-Mãe se divide em {splits}!");
    }

    private void FinishSplit()
    {
        var splits = RiftSession.Rules.SplitCount(CoresBroken);
        var share = Hp / splits;
        for (var i = 0; i < splits; i++)
        {
            var offset = new Vector2((i - ((splits - 1) / 2f)) * HitRadius * 1.1f, 0);
            var child = new Lodo(World, ChildSize) { StartDelay = 0.3f * i, Position = World.ClampToFloor(GlobalPosition + offset), DropsLoot = false };
            child.SetHealth(share);
            World.AddEnemy(child);
            _children.Add(child);
        }

        World.Combat.Unregister(this);
        foreach (var core in _cores)
        {
            World.Combat.Unregister(core);
        }

        Sprite.Visible = false;
        CollisionLayer = 0;
        CollisionMask = 0;
        _phase = Phase.Split;
    }

    private void CheckChildren()
    {
        if (_children.Count == 0 || _children.Any(c => IsInstanceValid(c) && !c.Dead))
        {
            var alive = _children.Where(c => IsInstanceValid(c) && !c.Dead).ToList();
            if (alive.Count > 0)
            {
                GlobalPosition = alive[0].GlobalPosition;
            }

            return;
        }

        Hp = 0;
        Die();
    }

    private void OnCoreShattered(BossCore core)
    {
        World.Combat.Unregister(core);
        var left = _cores.Count(c => !c.Broken);
        RiftRules.AddMaterial(RiftSession.Bag, RiftSession.Brecha.CoreMaterial, 1);
        World.Feel.Toast(left == 0 ? "Último núcleo partido! Ela não vai mais se dividir." : $"Núcleo partido! Restam {left}.");
    }
}
