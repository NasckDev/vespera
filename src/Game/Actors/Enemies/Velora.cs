using System.Collections.Generic;
using Godot;
using Vespera.Core.Rift;
using Vespera.Game.Combat;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// A Velória (Guardiã do Ato IV, Cemitério das Lanternas): colosso de cera com centenas de chamas. Três fases (limiares
/// em <c>boss.phases</c>, CAMPANHA Ato IV):
/// <list type="number">
/// <item><b>Vigília</b>: atira velas (círculo âmbar onde cada uma cai) que viram Velinhas; perto dela, uma varrida de
/// cera (círculo âmbar em volta). Partes: 3 candelabros (<c>boss.cores</c>, <c>boss.coreHp</c> de dano cada); cada um
/// quebrado = uma Velinha a menos por arremesso.</item>
/// <item><b>Cera</b>: também derrete numa poça que queima e anda atrás do caçador; ressurge onde a poça parar (círculo
/// âmbar de 0,8 s antes).</item>
/// <item><b>Uma vela</b>: a arena apaga (só a lanterna do caçador ilumina) e restam 5 velas; a Velória se esconde numa
/// delas. As erradas tremem quando a lanterna passa; a certa não. Apagar a certa faz ela ressurgir ali e ficar à mercê
/// por uns segundos; apagar uma errada solta uma labareda (círculo âmbar). Quando resta uma vela acesa ela diz a única
/// fala: "Qual delas é você?" (<c>ato4/velora</c>).</item>
/// </list>
/// </summary>
public partial class Velora(LevelWorld world) : Enemy("velora", world)
{
    public const string VoiceDialogue = "ato4/velora";

    private const float SweepRange = 280f;
    private const float SweepRadius = 240f;
    private const float CandleRadius = 75f;
    private const float CandleFlight = 0.5f;
    private const float CandleSpread = 300f;
    private const float PuddleRadius = 130f;
    private const float PuddleSpeed = 150f;
    private const float PuddleSeconds = 4.5f;
    private const float PuddleTick = 0.6f;
    private const float RiseRadius = 210f;
    private const float RiseWarning = 0.8f;
    private const float ExposedSeconds = 8f;
    private const float WrongFlareRadius = 120f;
    private const float WrongFlareWarning = 0.6f;
    private const int CandleCount = 5;
    private const float Darkness = 0.78f;
    private const float LanternRadius = 2.6f;
    private const float AttackHold = 0.9f;

    private static readonly Color WaxColor = new("f4e7c6");
    private static readonly Color Amber = new("f2a541");

    private readonly List<(TelegraphMarker Marker, Vector2 At, float Time)> _landing = [];
    private readonly List<(TelegraphMarker Marker, Vector2 At, float Time)> _flares = [];
    private readonly List<VeloraCandle> _candles = [];
    private TelegraphMarker? _sweepMarker;
    private TelegraphMarker? _riseMarker;
    private WaxPuddle? _puddle;
    private float _cooldown = 1.8f;
    private int _turn;
    private double _candelabroDamage;
    private float _melt = -1f;
    private float _puddleTick;
    private float _rise = -1f;
    private float _exposed;
    private bool _spoke;

    /// <summary>1, 2 ou 3.</summary>
    public int Phase { get; private set; } = 1;

    /// <summary>Candelabros quebrados (0 a <c>boss.cores</c>).</summary>
    public int CandelabrosBroken { get; private set; }

    /// <summary>Derretida em poça (fase 2): não leva golpe.</summary>
    public bool Melted => _melt >= 0f;

    /// <summary>Escondida numa das velas (fase 3, fora da janela em que ressurgiu).</summary>
    public bool Hidden { get; private set; }

    /// <summary>Velas acesas da fase 3 (capturas e testes).</summary>
    public IReadOnlyList<VeloraCandle> Candles => _candles;

    public override bool Unstoppable => true;

    public override bool CanBeHit => base.CanBeHit && !Melted && !Hidden;

    public override float BodyLift => Sheet.StandHeight * Sheet.SpriteScale * 0.45f;

    private static BossTuning Tuning => RiftSession.Load("cemiterio_das_lanternas").Boss;

    protected override void OnSpawned()
    {
        Enter(EnemyState.Idle);
        Play("parado");
        CombatSfx.Play(this, CombatSound.BossRoar, Vector2.Zero, -4f);
    }

    protected override Vector2 Think(float dt)
    {
        TickLanding(dt);
        TickFlares(dt);
        UpdatePhase();
        if (Phase == 3)
        {
            LanternGloom.Request(World, Darkness, LanternRadius);
            foreach (var candle in _candles)
            {
                candle.Lantern = Target?.GlobalPosition;
            }
        }

        if (Melted)
        {
            return TickMelt(dt);
        }

        if (_rise >= 0f)
        {
            TickRise(dt);
            return Vector2.Zero;
        }

        if (Hidden)
        {
            return Vector2.Zero;
        }

        if (Phase == 3)
        {
            _exposed -= dt;
            if (_exposed <= 0f && State is EnemyState.Idle or EnemyState.Approach)
            {
                Hide();
                return Vector2.Zero;
            }
        }

        _cooldown -= dt;
        switch (State)
        {
            case EnemyState.Telegraph:
                if (StateTime >= Def.Telegraph)
                {
                    ReleaseSweep();
                }

                return Vector2.Zero;
            case EnemyState.Attack:
            case EnemyState.Recover:
                if (StateTime >= AttackHold)
                {
                    Enter(EnemyState.Idle);
                    Play("parado");
                }

                return Vector2.Zero;
        }

        if (Target is not { } target)
        {
            return Vector2.Zero;
        }

        FaceTowards(target.GlobalPosition);
        var to = target.GlobalPosition - GlobalPosition;
        if (_cooldown <= 0f && ChooseAttack(target, to.Length()))
        {
            return Vector2.Zero;
        }

        if (to.Length() < SweepRange * A * 0.7f)
        {
            PlayIf("parado");
            return Vector2.Zero;
        }

        PlayIf("andar");
        return to.Normalized() * (float)Speed * A;
    }

    private bool ChooseAttack(Player target, float distance)
    {
        _cooldown = (float)Period * (Phase == 3 ? 0.8f : 1f);
        var turn = _turn++;
        if (Phase == 2 && turn % 3 == 2)
        {
            StartMelt();
            return true;
        }

        if (distance <= SweepRange * A && turn % 2 == 0)
        {
            StartSweep();
            return true;
        }

        ThrowCandles(target);
        return true;
    }

    private void UpdatePhase()
    {
        var phases = Tuning.Phases;
        var fraction = Hp / MaxHp;
        var next = fraction <= phases[1] ? 3 : fraction <= phases[0] ? 2 : 1;
        if (next <= Phase || Melted || _rise >= 0f)
        {
            return;
        }

        Phase = next;
        World.Feel.Shake(0.5f);
        CombatSfx.Play(this, CombatSound.BossRoar, Vector2.Zero);
        if (Phase == 2)
        {
            Story.StoryService.SetFlag("velora_fase_2");
            World.Feel.Toast("A cera esquenta: a Velória derrete e anda pelo chão. Fuja da poça.");
            return;
        }

        Story.StoryService.SetFlag("velora_fase_3");
        CancelAttack();
        Play("apagar");
        World.Feel.Toast("A arena apaga. Restam cinco velas. Uma delas não treme.");
        Hide();
    }

    // ---------------------------------------------------------------- fase 1: velas e varrida

    private void ThrowCandles(Player target)
    {
        Enter(EnemyState.Attack);
        Play("lancar");
        CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero, -4f);
        var count = Mathf.Max(1, Tuning.SpawnPerJump - CandelabrosBroken + (Phase == 3 ? 1 : 0));
        for (var i = 0; i < count; i++)
        {
            var spread = i == 0 ? Vector2.Zero : new Vector2(GD.Randf() - 0.5f, GD.Randf() - 0.5f) * CandleSpread * A * 2f;
            var at = World.ClampToFloor(target.GlobalPosition + spread);
            var seconds = (float)Def.Telegraph + CandleFlight + (i * 0.15f);
            _landing.Add((WarnCircle(at, CandleRadius * A, seconds), at, seconds));
        }
    }

    private void TickLanding(float dt)
    {
        for (var i = _landing.Count - 1; i >= 0; i--)
        {
            var (marker, at, time) = _landing[i];
            time -= dt;
            if (time > 0f)
            {
                _landing[i] = (marker, at, time);
                continue;
            }

            marker.Release();
            _landing.RemoveAt(i);
            World.Feel.Spark(at, Amber, 12, 220f * A, Vector2.Up, 70f);
            StrikeCircle(at, CandleRadius * A, 0.8, 200f);
            // A vela que caiu vira Velinha (menos uma por candelabro quebrado; nunca mais que boss.maxMinions vivas).
            if (!Dead && CountVelinhas() < Tuning.MaxMinions && CandelabrosBroken < Tuning.Cores)
            {
                var velinha = EnemyFactory.Create("velinha", World);
                velinha.DropsLoot = false;
                velinha.Position = at;
                World.AddEnemy(velinha);
            }
        }
    }

    private int CountVelinhas()
    {
        var alive = 0;
        foreach (var e in World.Enemies)
        {
            alive += e is Velinha { Dead: false } ? 1 : 0;
        }

        return alive;
    }

    private void StartSweep()
    {
        Enter(EnemyState.Telegraph);
        Play("lancar");
        Flash.Tint = 0.35f;
        CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero);
        _sweepMarker = WarnCircle(GlobalPosition, SweepRadius * A, (float)Def.Telegraph);
    }

    private void ReleaseSweep()
    {
        Flash.Tint = 0f;
        _sweepMarker?.Release();
        _sweepMarker = null;
        Enter(EnemyState.Recover);
        CombatSfx.Play(this, CombatSound.Slam, Vector2.Zero);
        World.Feel.Shake(0.3f);
        World.Feel.Ring(GlobalPosition, SweepRadius * A, WaxColor, 0.4f, 9f * A);
        StrikeCircle(GlobalPosition, SweepRadius * A, 1.2, 360f);
    }

    // ---------------------------------------------------------------- fase 2: poça de cera

    private void StartMelt()
    {
        Enter(EnemyState.Attack);
        Play("derreter");
        CombatSfx.Play(this, CombatSound.Swallow, Vector2.Zero, -4f);
        _melt = 0f;
        _puddleTick = PuddleTick;
        _puddle = new WaxPuddle(PuddleRadius * A) { Name = "PocaDeCera", GlobalPosition = GlobalPosition };
        World.AddGroundFx(_puddle);
        Bar.Conceal();
        SetSolid(false);
    }

    private Vector2 TickMelt(float dt)
    {
        _melt += dt;
        // Meio segundo derretendo no lugar; depois some na poça e anda.
        if (_melt > 0.6f && Sprite.Visible)
        {
            Sprite.Visible = false;
        }

        if (_puddle is { } puddle)
        {
            puddle.GlobalPosition = GlobalPosition;
            _puddleTick -= dt;
            if (_puddleTick <= 0f && Target is { } burned && burned.GlobalPosition.DistanceTo(GlobalPosition) <= PuddleRadius * A)
            {
                _puddleTick = PuddleTick;
                TouchPlayer(burned.Combat.HitCenter, 1f, 0.35, 0f);
                World.Feel.Spark(burned.GlobalPosition, Amber, 6, 140f * A, Vector2.Up, 40f);
            }
        }

        if (_melt < PuddleSeconds)
        {
            return _melt > 0.6f && Target is { } target ? (target.GlobalPosition - GlobalPosition).Normalized() * PuddleSpeed * A : Vector2.Zero;
        }

        _melt = -1f;
        StartRise(GlobalPosition);
        return Vector2.Zero;
    }

    private void StartRise(Vector2 at)
    {
        GlobalPosition = World.ClampToFloor(at);
        _rise = 0f;
        _riseMarker = WarnCircle(GlobalPosition, RiseRadius * A, RiseWarning);
        CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero);
    }

    private void TickRise(float dt)
    {
        _rise += dt;
        if (_rise < RiseWarning)
        {
            return;
        }

        _rise = -1f;
        _riseMarker?.Release();
        _riseMarker = null;
        _puddle?.QueueFree();
        _puddle = null;
        Hidden = false;
        Sprite.Visible = true;
        SetSolid(true);
        Play("ressurgir");
        Enter(EnemyState.Recover);
        CombatSfx.Play(this, CombatSound.BossLand, Vector2.Zero);
        World.Feel.Shake(0.4f);
        World.Feel.Ring(GlobalPosition, RiseRadius * A, WaxColor, 0.45f, 10f * A);
        StrikeCircle(GlobalPosition, RiseRadius * A, 1.2, 380f);
        Bar.Set((float)(Hp / MaxHp));
        if (Phase == 3)
        {
            _exposed = ExposedSeconds;
            World.Feel.Toast("A Velória saiu da vela. Bata agora!");
        }
    }

    // ---------------------------------------------------------------- fase 3: uma vela

    private void Hide()
    {
        Hidden = true;
        Sprite.Visible = false;
        Bar.Conceal();
        SetSolid(false);
        Enter(EnemyState.Idle);
        ClearCandles();
        var center = World.PlayArea.GetCenter();
        var rx = Mathf.Min(360f * A, World.PlayArea.Size.X * 0.32f);
        var ry = Mathf.Min(180f * A, World.PlayArea.Size.Y * 0.3f);
        var trueIndex = (int)(GD.Randi() % CandleCount);
        var human = World.Data.HumanHeight;
        var start = GD.Randf() * Mathf.Tau;
        for (var i = 0; i < CandleCount; i++)
        {
            var angle = start + (Mathf.Tau * i / CandleCount);
            var at = World.ClampToFloor(center + new Vector2(Mathf.Cos(angle) * rx, Mathf.Sin(angle) * ry));
            var candle = new VeloraCandle(i == trueIndex, human * 0.45f, human * 1.6f) { Name = $"Vela{i}", Position = at };
            candle.Snuffed += OnSnuffed;
            World.Entities.AddChild(candle);
            World.Combat.Register(candle);
            _candles.Add(candle);
        }
    }

    private void OnSnuffed(VeloraCandle candle)
    {
        World.Combat.Unregister(candle);
        CombatSfx.Play(candle, CombatSound.Hit, Vector2.Zero, -4f);
        if (candle.IsTrue)
        {
            ClearCandles();
            StartRise(candle.GlobalPosition);
            return;
        }

        // Errada: a vela apagada solta uma labareda (círculo âmbar antes).
        _flares.Add((WarnCircle(candle.GlobalPosition, WrongFlareRadius * A, WrongFlareWarning), candle.GlobalPosition, WrongFlareWarning));
        var lit = 0;
        foreach (var c in _candles)
        {
            lit += c.Lit ? 1 : 0;
        }

        if (lit == 1 && !_spoke)
        {
            _spoke = true;
            Speak();
        }
    }

    private void TickFlares(float dt)
    {
        for (var i = _flares.Count - 1; i >= 0; i--)
        {
            var (marker, at, time) = _flares[i];
            time -= dt;
            if (time > 0f)
            {
                _flares[i] = (marker, at, time);
                continue;
            }

            marker.Release();
            _flares.RemoveAt(i);
            World.Feel.Spark(at, Amber, 16, 260f * A, Vector2.Up, 80f);
            StrikeCircle(at, WrongFlareRadius * A, 0.8, 260f);
        }
    }

    /// <summary>A única fala (ROTEIRO-V2 §13 Ato IV, fala 5), quando resta uma vela acesa.</summary>
    private void Speak()
    {
        var line = Story.StoryService.LoadDialogue(VoiceDialogue).Nodes.TryGetValue("a", out var node) ? node.Text : "Qual delas é você?";
        World.Feel.Toast($"Velória: “{line}”");
        World.Feel.Shake(0.4f);
        Story.StoryService.SetFlag("velora_falou");
        Boot.Log.Info($"velora: fase 3, resta uma vela — '{line}'");
    }

    private void ClearCandles()
    {
        foreach (var candle in _candles)
        {
            if (IsInstanceValid(candle))
            {
                World.Combat.Unregister(candle);
                candle.QueueFree();
            }
        }

        _candles.Clear();
    }

    private void CancelAttack()
    {
        Flash.Tint = 0f;
        _sweepMarker?.Release();
        _sweepMarker = null;
    }

    protected override void OnHurt(in HitInfo hit)
    {
        var count = Tuning.Cores;
        if (CandelabrosBroken >= count || Tuning.CoreHp <= 0)
        {
            return;
        }

        _candelabroDamage += hit.Damage;
        if (_candelabroDamage < Tuning.CoreHp)
        {
            return;
        }

        _candelabroDamage = 0;
        CandelabrosBroken++;
        World.Feel.Shake(0.25f);
        World.Feel.Spark(HitCenter - new Vector2(0, BodyLift), Amber, 18, 300f * A, Vector2.Up, 70f);
        World.Feel.Toast(CandelabrosBroken < count
            ? $"Um candelabro da Velória caiu ({CandelabrosBroken}/{count}). Menos velas por arremesso."
            : "Os três candelabros caíram. As velas não acendem mais.");
    }

    /// <summary>Escondida ou derretida não ocupa lugar: o caçador passa por onde ela estava.</summary>
    private void SetSolid(bool solid) => CollisionLayer = solid ? EnemyLayer : 0;

    private void PlayIf(string anim)
    {
        if (Sprite.Animation != anim)
        {
            Play(anim);
        }
    }

    protected override void OnDied()
    {
        foreach (var (marker, _, _) in _landing)
        {
            marker.Release();
        }

        foreach (var (marker, _, _) in _flares)
        {
            marker.Release();
        }

        _landing.Clear();
        _flares.Clear();
        _sweepMarker?.Release();
        _riseMarker?.Release();
        _puddle?.QueueFree();
        _puddle = null;
        ClearCandles();
        Sprite.Visible = true;
        World.Feel.Shake(0.8f);
        World.Feel.Toast("As velas apagam todas de uma vez. A cera esfria no altar.");
    }

    /// <summary>Poça de cera quente (fase 2): círculo claro com borda âmbar que pulsa; queima quem pisa.</summary>
    private sealed partial class WaxPuddle(float radius) : Node2D
    {
        private float _t;

        public override void _Process(double delta)
        {
            _t += (float)delta;
            QueueRedraw();
        }

        public override void _Draw()
        {
            var pulse = 0.5f + (0.5f * Mathf.Sin(_t * 5f));
            DrawCircle(Vector2.Zero, radius, new Color(0.96f, 0.9f, 0.76f, 0.55f));
            DrawCircle(Vector2.Zero, radius * 0.6f, new Color(1f, 0.78f, 0.45f, 0.45f));
            DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 40, Amber with { A = 0.6f + (0.4f * pulse) }, 4f);
        }
    }
}
