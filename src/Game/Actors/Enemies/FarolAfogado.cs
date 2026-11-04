using System.Collections.Generic;
using Godot;
using Vespera.Core.Rift;
using Vespera.Game.Combat;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Farol Afogado (Guardião do Ato III, Cais Afogado): faroleiro de bronze com a lanterna do farol no lugar da cabeça.
/// Três fases (limiares em <c>boss.phases</c>, CAMPANHA Ato III):
/// <list type="number">
/// <item><b>Feixe</b>: a lente acende âmbar e uma faixa âmbar marca o caminho do feixe (aviso); quem é tocado é puxado
/// até o pé dele, e logo vem a <b>pisada</b> (círculo âmbar em volta).</item>
/// <item><b>Maré</b>: chama Afogados da água (2 por chamada, máx. <c>boss.maxMinions</c>) e o anel externo alaga
/// (anda mais devagar na água).</item>
/// <item><b>Escuro</b>: a luz apaga (só a lanterna do caçador ilumina) e diz a única fala, "Eu apaguei por uma noite."
/// A arena inteira alaga menos uma ilha seca que muda de lugar a cada 15 s (círculo âmbar no lugar novo antes); fora
/// dela a maré machuca. O aviso dos golpes vira buzina + brilho fraco da lente.</item>
/// </list>
/// </summary>
public partial class FarolAfogado(LevelWorld world) : Enemy("farol_afogado", world)
{
    public const string VoiceDialogue = "ato3/farol";

    // Medidas da arena (data/levels/cais_arena.json, "_inunda"): centro da plataforma, centro seco e anel que alaga.
    private const float BeamLength = 1000f;
    private const float BeamWidth = 95f;
    private const float BeamPull = -900f;
    private const float StompRadius = 240f;
    private const float AttackHold = 0.8f;
    private const float WadeSlow = 0.7f;
    private const float TideTick = 2.2f;
    private const float TideGrace = 3f;
    private const float IslandRadius = 150f;
    private static readonly Color Amber = new("f2a541");
    private static readonly Color Sea = new("3c7a8a");
    private static readonly Color Dark = new(0.16f, 0.18f, 0.3f);

    private readonly FloodPlan _flood = new(836, 455, 325, 210, 500, 245, IslandRadius);
    private readonly List<TelegraphMarker> _markers = [];
    private float _cooldown = 2.4f;
    private bool _stompNext;
    private bool _callNext;
    private float _dark;
    private float _tide;
    private int _island = -1;
    private bool _warned;
    private FloodWater? _water;
    private CanvasModulate? _night;

    /// <summary>1, 2 ou 3.</summary>
    public int Phase { get; private set; } = 1;

    /// <summary>Segundos de escuro (fase 3): a ilha seca muda a cada 15.</summary>
    public float DarkSeconds => _dark;

    public override bool Unstoppable => true;

    public override float BodyLift => Sheet.StandHeight * Sheet.SpriteScale * 0.4f;

    private static BossTuning Tuning => RiftSession.Load("cais_afogado").Boss;

    protected override void OnSpawned()
    {
        Enter(EnemyState.Idle);
        Play("parado");
        _water = new FloodWater(_flood) { Name = "MareDoFarol" };
        World.AddGroundFx(_water);
    }

    protected override Vector2 Think(float dt)
    {
        UpdatePhase();
        TickTide(dt);
        _cooldown -= dt;
        switch (State)
        {
            case EnemyState.Telegraph:
                if (StateTime >= Def.Telegraph)
                {
                    Release();
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

        var target = Target;
        if (target is null)
        {
            return Vector2.Zero;
        }

        FaceTowards(target.GlobalPosition);
        if (_cooldown > 0f)
        {
            return Vector2.Zero;
        }

        _cooldown = (float)Period * (Phase == 3 ? 0.75f : 1f);
        if (Phase >= 2 && _callNext && CountDrowned() < Tuning.MaxMinions)
        {
            CallDrowned();
        }
        else if (_stompNext || GlobalPosition.DistanceTo(target.GlobalPosition) < StompRadius * A * 0.8f)
        {
            StartStomp();
        }
        else
        {
            StartBeam(target);
        }

        _callNext = !_callNext;
        return Vector2.Zero;
    }

    private void UpdatePhase()
    {
        var phases = Tuning.Phases;
        var fraction = Hp / MaxHp;
        var next = fraction <= phases[1] ? 3 : fraction <= phases[0] ? 2 : 1;
        if (next <= Phase)
        {
            return;
        }

        Phase = next;
        World.Feel.Shake(0.5f);
        CombatSfx.Play(this, CombatSound.BossRoar, Vector2.Zero);
        if (Phase == 2)
        {
            World.Feel.Toast("A maré sobe: o anel de fora alaga e o Farol chama os Afogados.");
        }
        else
        {
            GoDark();
        }

        _water?.SetPhase(Phase, _flood.Island(0));
    }

    /// <summary>Fase 3: a lente apaga, a arena escurece e o Farol diz a fala única (ROTEIRO-V2 §13, fala 5).</summary>
    private void GoDark()
    {
        Enter(EnemyState.Recover);
        Play("apagar");
        _dark = 0f;
        _tide = TideGrace;
        _night = new CanvasModulate { Name = "EscuroDoFarol", Color = Dark };
        World.AddChild(_night);
        var line = Story.StoryService.LoadDialogue(VoiceDialogue).Nodes.TryGetValue("a", out var node) ? node.Text : string.Empty;
        World.Feel.Toast(line.Length > 0 ? $"Farol Afogado: “{line}”" : "O Farol apaga.");
        World.Feel.Shake(0.7f);
        Vespera.Game.Boot.Log.Info($"farol: fase 3, escuro — '{line}'");
    }

    private void StartBeam(Player target)
    {
        Enter(EnemyState.Telegraph);
        Play("feixe");
        Flash.Tint = Phase == 3 ? 0.15f : 0.4f;
        HornOrWarn();
        var dir = (target.GlobalPosition - GlobalPosition).Normalized();
        _markers.Add(WarnLane(GlobalPosition, dir, BeamLength * A, BeamWidth * A, (float)Def.Telegraph));
        _beamDir = dir;
        _stompNext = false;
    }

    private Vector2 _beamDir = Vector2.Down;

    private void StartStomp()
    {
        Enter(EnemyState.Telegraph);
        Play("pisada");
        Flash.Tint = Phase == 3 ? 0.15f : 0.4f;
        HornOrWarn();
        _markers.Add(WarnCircle(GlobalPosition, StompRadius * A, (float)Def.Telegraph));
        _beamDir = Vector2.Zero;
        _stompNext = false;
    }

    private void HornOrWarn() =>
        CombatSfx.Play(this, Phase == 3 ? CombatSound.BossRoar : CombatSound.Telegraph, Vector2.Zero, Phase == 3 ? -8f : -4f);

    private void Release()
    {
        Flash.Tint = 0f;
        foreach (var marker in _markers)
        {
            marker.Release();
        }

        _markers.Clear();
        Enter(EnemyState.Attack);
        if (_beamDir == Vector2.Zero)
        {
            CombatSfx.Play(this, CombatSound.BossLand, Vector2.Zero);
            World.Feel.Shake(0.35f);
            World.Feel.Ring(GlobalPosition, StompRadius * A, Sea, 0.45f, 10f * A);
            World.Feel.Spark(GlobalPosition, Sea, 22, 320f * A, Vector2.Up, 120f);
            StrikeCircle(GlobalPosition, StompRadius * A, 1.2, 360f);
            return;
        }

        CombatSfx.Play(this, CombatSound.BowCharged, Vector2.Zero, -2f);
        DrawBeam(_beamDir);
        // Força negativa: o feixe puxa o caçador até o pé do Farol, e a próxima é a pisada.
        if (StrikeLane(GlobalPosition, _beamDir, BeamLength * A, BeamWidth * A, 0.6, BeamPull))
        {
            _stompNext = true;
            _cooldown = Mathf.Min(_cooldown, 0.5f);
        }
    }

    /// <summary>O feixe por código (a tira "feixe" corta a luz na borda da célula): linha âmbar que some.</summary>
    private void DrawBeam(Vector2 dir)
    {
        var beam = new Line2D
        {
            Name = "FeixeDoFarol",
            Width = BeamWidth * A * 0.8f,
            DefaultColor = Amber with { A = 0.75f },
            Points = [GlobalPosition - new Vector2(0, BodyLift), GlobalPosition + (dir * BeamLength * A)],
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round,
        };
        World.AddGroundFx(beam);
        var tween = beam.CreateTween();
        tween.TweenProperty(beam, "modulate:a", 0f, 0.35f);
        tween.TweenCallback(Callable.From(beam.QueueFree));
    }

    private void CallDrowned()
    {
        Enter(EnemyState.Attack);
        Play("chamar");
        CombatSfx.Play(this, CombatSound.BossRoar, Vector2.Zero, -6f);
        var alive = CountDrowned();
        for (var i = 0; i < Tuning.SpawnPerJump && alive < Tuning.MaxMinions; i++, alive++)
        {
            var drowned = EnemyFactory.Create("afogado", World);
            drowned.DropsLoot = false;
            var side = i % 2 == 0 ? -1f : 1f;
            drowned.Position = World.ClampToFloor(GlobalPosition + new Vector2(side * 320f * A, 160f * A));
            World.AddEnemy(drowned);
            World.Feel.Spark(drowned.Position, Sea, 14, 220f * A, Vector2.Up, 60f);
        }
    }

    private int CountDrowned()
    {
        var alive = 0;
        foreach (var e in World.Enemies)
        {
            alive += e is Afogado { Dead: false } ? 1 : 0;
        }

        return alive;
    }

    /// <summary>Água: anda devagar onde está alagado; na fase 3 a ilha muda (aviso antes) e a maré machuca fora dela.</summary>
    private void TickTide(float dt)
    {
        var player = World.Player;
        if (player is null)
        {
            return;
        }

        if (Phase == 3)
        {
            _dark += dt;
            var index = FloodPlan.SpotIndex(_dark);
            if (index != _island)
            {
                _island = index;
                _warned = false;
                _water?.SetPhase(3, _flood.Island(index));
            }

            if (!_warned && FloodPlan.WarnNow(_dark))
            {
                _warned = true;
                var (x, y) = _flood.Island(index + 1);
                _markers.Add(WarnCircle(new Vector2((float)x, (float)y), IslandRadius, (float)FloodPlan.WarnSeconds));
                CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero, -6f);
            }
        }

        var at = player.GlobalPosition;
        var wet = _flood.Flooded(at.X, at.Y, Phase, _dark);
        player.Wade = wet ? WadeSlow : 1f;
        if (Phase < 3 || !wet)
        {
            return;
        }

        _tide -= dt;
        if (_tide <= 0f)
        {
            _tide = TideTick;
            World.Feel.Spark(at, Sea, 8, 160f * A, Vector2.Up, 80f);
            StrikeCircle(at, 10f, 0.35, 0f);
        }
    }

    protected override void OnDied()
    {
        foreach (var marker in _markers)
        {
            marker.Release();
        }

        _markers.Clear();
        _night?.QueueFree();
        _night = null;
        _water?.QueueFree();
        _water = null;
        if (World.Player is { } player)
        {
            player.Wade = 1f;
        }

        World.Feel.Shake(0.8f);
    }

    public override void _ExitTree()
    {
        if (World.Player is { } player && IsInstanceValid(player))
        {
            player.Wade = 1f;
        }

        base._ExitTree();
    }

    /// <summary>Água por cima do chão da arena: fase 2 o anel; fase 3 tudo menos a ilha seca.</summary>
    private sealed partial class FloodWater(FloodPlan plan) : Node2D
    {
        private const int Segments = 48;
        private static readonly Color Water = new(0.24f, 0.48f, 0.54f, 0.42f);
        private static readonly Color Dry = new(0.42f, 0.42f, 0.5f, 0.35f);
        private int _phase = 1;
        private Vector2 _island;

        public void SetPhase(int phase, (double X, double Y) island)
        {
            _phase = phase;
            _island = new Vector2((float)island.X, (float)island.Y);
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_phase <= 1)
            {
                return;
            }

            var c = new Vector2((float)plan.CenterX, (float)plan.CenterY);
            var outer = Ellipse(c, (float)plan.RingRx, (float)plan.RingRy);
            if (_phase == 2)
            {
                var inner = Ellipse(c, (float)plan.DryRx, (float)plan.DryRy);
                for (var i = 0; i < Segments; i++)
                {
                    var j = (i + 1) % Segments;
                    DrawColoredPolygon([outer[i], outer[j], inner[j], inner[i]], Water);
                }

                return;
            }

            DrawColoredPolygon(outer, Water);
            DrawCircle(_island, (float)plan.IslandRadius, Dry);
            DrawArc(_island, (float)plan.IslandRadius, 0f, Mathf.Tau, Segments, Amber with { A = 0.6f }, 3f);
        }

        private static Vector2[] Ellipse(Vector2 c, float rx, float ry)
        {
            var points = new Vector2[Segments];
            for (var i = 0; i < Segments; i++)
            {
                var a = Mathf.Tau * i / Segments;
                points[i] = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
            }

            return points;
        }
    }
}
