using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Combat;
using Vespera.Core.Combat.Ai;
using Vespera.Core.Common;
using Vespera.Core.Data;
using Vespera.Core.Rules;
using Vespera.Core.State;
using Vespera.Core.World;
using Vespera.Enemies;
using Vespera.Items;
using Vespera.Player;
using Vespera.World.Dungeon;

namespace Vespera.Combat;

/// <summary>
/// Dono do combate da cena World (frente B1): relógio com hitstop, monstros (IA pura do Core), projéteis, perigos,
/// efeitos, números de dano, itens no chão, props da Brecha, o combate do jogador e o fluxo das Brechas, morte/Lanterna
/// e invasão. Um único <c>_PhysicsProcess</c> avança tudo na ordem do <c>update</c> da v2 (<c>main.js:104-111</c>), sem
/// alocar por frame; os sistemas visuais usam pools de structs. Congela quando o gameplay está bloqueado (UI, pausa).
/// Fica em <c>World.tscn</c> logo depois de <c>Entities</c>; conversa com as outras frentes só pelo <see cref="EventBus"/>.
/// </summary>
public partial class CombatDirector : Node2D, IMonsterWorld
{
    private const float MaxDelta = 0.05f; // main.js:18

    private readonly List<Enemy> _enemies = new(64);
    private readonly List<Pickup> _pickups = new(64);
    private readonly List<DungeonProp> _props = new(32);
    private readonly List<Vector3> _avoid = new(32);
    private SeededRandom _fxRandom = new(1);
    private GameDatabase _db = null!;
    private DamageCalculator _calc = null!;
    private MonsterScaling _scaling = null!;
    private LootTables _loot = null!;
    private readonly Dictionary<string, ChaseField> _chaseFields = [];
    private ChaseField? _chase;
    private float _time;
    private bool _paused;

    public static CombatDirector? Instance { get; private set; }

    [Export]
    public World.World World { get; set; } = null!;

    public CombatClock Clock { get; } = new();

    /// <summary>Chão: telegraphs, porta, abaixo das entidades.</summary>
    public FxLayer Ground { get; private set; } = null!;

    /// <summary>Efeitos normais acima das entidades (arcos, anéis, partículas sem brilho).</summary>
    public FxLayer Fx { get; private set; } = null!;

    /// <summary>Efeitos aditivos (partículas com brilho, rastros).</summary>
    public FxLayer Glow { get; private set; } = null!;

    public ProjectileSystem Projectiles { get; private set; } = null!;

    public HazardSystem Hazards { get; private set; } = null!;

    public CombatOverlay Overlay { get; private set; } = null!;

    public PlayerCombat PlayerCombat { get; private set; } = null!;

    public BrechaController Brecha { get; private set; } = null!;

    public IReadOnlyList<Enemy> Enemies => _enemies;

    public IReadOnlyList<Pickup> Pickups => _pickups;

    public IReadOnlyList<DungeonProp> Props => _props;

    public GameState State => GameManager.Instance.State;

    public GameDatabase Db => _db;

    public DamageCalculator Calculator => _calc;

    public LootTables Loot => _loot;

    /// <summary>RNG das regras (o do <see cref="GameState"/>, D8).</summary>
    public IRandom Rng => State.Random;

    /// <summary>RNG só visual (física dos drops), para não mexer na sequência das regras.</summary>
    public IRandom FxRandom => _fxRandom;

    /// <summary>Relógio de jogo (segundos de combate, já com hitstop).</summary>
    public float Time => _time;

    public Player.Player Player => World.Player;

    public Vector2 PlayerFoot => World.Player.GlobalPosition;

    public bool PlayerDead => PlayerCombat.Dead;

    public bool PlayerDashing => PlayerCombat.State.IsDashing;

    /// <summary>Monstros vivos (sem bonecos) + nascendo.</summary>
    public int AliveMonsters
    {
        get
        {
            var n = Hazards.PendingSpawns;
            foreach (var e in _enemies)
            {
                if (!e.Body.Dead && !e.Body.IsDummy)
                {
                    n++;
                }
            }

            return n;
        }
    }

    public int AliveNonBossCount
    {
        get
        {
            var n = 0;
            foreach (var e in _enemies)
            {
                if (!e.Body.Dead && !e.Body.IsBoss && !e.Body.IsDummy)
                {
                    n++;
                }
            }

            return n;
        }
    }

    public override void _EnterTree() => Instance = this;

    public override void _Ready()
    {
        _db = GameManager.Db;
        _calc = new DamageCalculator(_db.Rules.Combat);
        _scaling = new MonsterScaling(_db);
        _loot = new LootTables(_db);
        _fxRandom = new SeededRandom((int)(Godot.Time.GetTicksUsec() & int.MaxValue));
        ProcessPriority = 10; // depois do Player (movimento) e do World (saídas)

        var glow = CreateGlowTexture();
        var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        Ground = new FxLayer { Name = "Ground", ZIndex = -5 };
        Hazards = new HazardSystem { Name = "Hazards", ZIndex = -5 };
        Fx = new FxLayer { Name = "Fx" };
        Glow = new FxLayer { Name = "Glow", Material = additive };
        Projectiles = new ProjectileSystem { Name = "Projectiles", Material = additive };
        AddChild(Ground);
        AddChild(Hazards);
        AddChild(Fx);
        AddChild(Projectiles);
        AddChild(Glow);
        Hazards.Setup(this, glow);
        Projectiles.Setup(this, glow);
        var overlayLayer = new CanvasLayer { Name = "CombatOverlay", Layer = 5 };
        Overlay = new CombatOverlay { Name = "Overlay" };
        overlayLayer.AddChild(Overlay);
        AddChild(overlayLayer);
        Overlay.Setup(this);

        PlayerCombat = new PlayerCombat(this, glow);
        Brecha = new BrechaController(this);
        Ground.DrawExtra = DrawGround;
        Fx.DrawExtra = DrawAir;

        var bus = EventBus.Instance;
        bus.MapChanged += OnMapChanged;
        bus.GameLoaded += OnGameLoaded;
        bus.PauseChanged += OnPauseChanged;
        Brecha.Connect(bus);
        PlayerCombat.OnGameLoaded();
    }

    public override void _ExitTree()
    {
        var bus = EventBus.Instance;
        bus.MapChanged -= OnMapChanged;
        bus.GameLoaded -= OnGameLoaded;
        bus.PauseChanged -= OnPauseChanged;
        Brecha?.Disconnect(bus);
        PlayerCombat?.Dispose();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void _Input(InputEvent @event) => PlayerCombat.HandleInput(@event);

    public override void _PhysicsProcess(double delta)
    {
        var realDt = MathF.Min(MaxDelta, (float)delta);
        if (_paused || GameManager.Instance.IsGameplayBlocked)
        {
            PlayerCombat.Frozen();
            return;
        }

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var dt = (float)Clock.Step(realDt);
        _time += dt;
        PlayerCombat.Step(dt);
        Mark(0, ref started);
        StepEnemies(dt);
        Mark(1, ref started);
        Projectiles.Step(dt);
        Hazards.Step(dt);
        Mark(2, ref started);
        Ground.Step(dt);
        Fx.Step(dt);
        Glow.Step(dt);
        Mark(3, ref started);
        StepPickups(dt);
        Overlay.Step(dt);
        Brecha.Step(dt, realDt);
        Mark(4, ref started);
        LastStepMs = _sectionMs[0] + _sectionMs[1] + _sectionMs[2] + _sectionMs[3] + _sectionMs[4];
        LastStepBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
    }

    private readonly double[] _sectionMs = new double[5];

    /// <summary>Tempo por seção do último frame: jogador, monstros, projéteis+perigos, efeitos, itens+Brecha (ms).</summary>
    public IReadOnlyList<double> LastSectionMs => _sectionMs;

    private void Mark(int section, ref long since)
    {
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        _sectionMs[section] = (now - since) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        since = now;
    }

    /// <summary>Bytes gerenciados alocados no último frame de combate (diagnóstico: deve ser 0 sem eventos).</summary>
    public long LastStepBytes { get; private set; }

    /// <summary>Tempo do último frame de combate (diagnóstico de desempenho no smoke).</summary>
    public double LastStepMs { get; private set; }

    // --- Monstros ---

    /// <summary>Cria o monstro agora (sem telegraph), escalado pelo rank da Brecha/invasão atual.</summary>
    public Enemy SpawnMonsterNow(string monsterId, Vector2 at, bool chase, bool invasion)
    {
        var def = _db.Monsters[monsterId];
        Enemy.Preload(def);
        var (rank, season) = Brecha.ScalingContext;
        var scaled = _scaling.Scale(def, rank, season);
        var body = new MonsterBody(scaled, at.X, at.Y, Enemy.HitHeightFor(def), Rng) { Invasion = invasion };
        if (chase)
        {
            body.ForceState(MonsterState.Chase);
        }

        return AddEnemy(body);
    }

    /// <summary>Telegraph de 0,9 s (ou <paramref name="delay"/>) e depois o monstro (<c>spawnTele</c>).</summary>
    public void SpawnTelegraphed(string monsterId, Vector2 at, float delay = 0.9f, bool chase = false, bool invasion = false)
    {
        Enemy.Preload(_db.Monsters[monsterId]);
        Hazards.SpawnMonster(monsterId, at, delay, chase, invasion);
    }

    /// <summary>Carrega os sprites de todos os monstros de um tema (ondas, extra, elite, chefe e invocações).</summary>
    public void PreloadTheme(Core.Data.ThemeDef theme)
    {
        foreach (var mob in theme.Mobs)
        {
            Enemy.Preload(_db.Monsters[mob.Monster]);
        }

        Enemy.Preload(_db.Monsters[theme.Extra]);
        Enemy.Preload(_db.Monsters[theme.Elite]);
        var boss = _db.Monsters[theme.Boss];
        Enemy.Preload(boss);
        if (boss.Summon is { } summon)
        {
            Enemy.Preload(_db.Monsters[summon]);
        }
    }

    public Enemy AddEnemy(MonsterBody body)
    {
        var enemy = Enemy.Create(body);
        World.Entities.AddChild(enemy);
        _enemies.Add(enemy);
        return enemy;
    }

    private void StepEnemies(float dt)
    {
        var foot = PlayerFoot;
        _chase?.Update(foot.X, foot.Y);
        var view = new PlayerView(foot.X, foot.Y, PlayerCombat.Aim, PlayerCombat.Dead);
        var rng = Rng;
        for (var i = 0; i < _enemies.Count; i++)
        {
            var m = _enemies[i].Body;
            MonsterAi.Step(m, dt, in view, this, rng);
            for (var j = 0; j < _enemies.Count; j++)
            {
                MonsterAi.Separate(m, _enemies[j].Body, this);
            }

            if (!m.Dead && !m.IsDummy)
            {
                if (m.Stun > 0 && _fxRandom.NextDouble() < dt * 6)
                {
                    Glow.Burst(new Vector2((float)m.X, (float)(m.Y - m.HitHeight - 6)), 1, CombatPalette.Stun, new BurstOptions { V1 = 15, Gravity = 0 });
                }

                if (m.IsElite && _fxRandom.NextDouble() < 0.3)
                {
                    var at = new Vector2((float)(m.X + _fxRandom.Range(-10, 10)), (float)(m.Y - _fxRandom.Range(5, 25)));
                    Glow.Burst(at, 1, CombatPalette.Brechas, new BurstOptions { V1 = 10, Gravity = -30 });
                }
            }
        }

        for (var i = _enemies.Count - 1; i >= 0; i--)
        {
            var e = _enemies[i];
            var m = e.Body;
            if (m.Dead && m.DeadT > (m.IsBoss ? 3.2 : 1.6))
            {
                _enemies.RemoveAt(i);
                RemoveNode(e);
                continue;
            }

            e.Sync(_time, foot);
        }
    }

    // --- IMonsterWorld ---

    /// <summary>
    /// <c>moveEnt</c> (<c>core.js:249-253</c>): passos de até 3 u, eixo a eixo, contra o mapa. Props sólidos (barris,
    /// baús) não travam o passo: depois dele o monstro é empurrado para fora da elipse do prop (D46), o que o faz
    /// deslizar em volta em vez de empacar encostado (na v2, colado num caixote, o passo de 0,6 u caía na faixa mais
    /// larga da elipse e o monstro ficava parado para sempre).
    /// </summary>
    public void Move(MonsterBody m, double dx, double dy)
    {
        var r = Math.Min(6, m.Radius * 0.5);
        var steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dy)) / 3));
        var sx = dx / steps;
        var sy = dy / steps;
        for (var i = 0; i < steps; i++)
        {
            if (CanStand(m.X + sx, m.Y, r, fly: true))
            {
                m.X += sx;
            }

            if (CanStand(m.X, m.Y + sy, r, fly: true))
            {
                m.Y += sy;
            }
        }

        if (!m.Fly)
        {
            PushOutOfProps(m, r);
        }
    }

    /// <summary>Tira o monstro de dentro da elipse dos props sólidos (raio do prop + raio de movimento), se o mapa deixar.</summary>
    private void PushOutOfProps(MonsterBody m, double r)
    {
        for (var i = 0; i < _props.Count; i++)
        {
            var pp = _props[i];
            if (!pp.BlocksMovement)
            {
                continue;
            }

            var ex = m.X - pp.Position.X;
            var ey = m.Y - pp.Position.Y;
            var need = pp.SolidRadius + r;
            var q = Math.Sqrt((ex * ex) + (ey * ey * 2.2));
            if (q >= need)
            {
                continue;
            }

            if (q < 0.01)
            {
                ex = 1;
                ey = 0;
                q = 1;
            }

            var nx = pp.Position.X + (ex * need / q);
            var ny = pp.Position.Y + (ey * need / q);
            if (CanStand(nx, ny, r, fly: true))
            {
                m.X = nx;
                m.Y = ny;
            }
        }
    }

    /// <summary>Linha reta bloqueada → segue o campo de perseguição (D43). Voadores vão reto.</summary>
    public void ChaseDirection(MonsterBody m, ref double ux, ref double uy)
    {
        if (m.Fly || _chase is null)
        {
            return;
        }

        var p = PlayerFoot;
        if (_chase.LineClear((float)m.X, (float)m.Y, p.X, p.Y) && !PropOnLine((float)m.X, (float)m.Y, p.X, p.Y))
        {
            return;
        }

        if (_chase.TryDirection((float)m.X, (float)m.Y, out var fx, out var fy))
        {
            ux = fx;
            uy = fy;
            Unstick(m, ref ux, ref uy);
        }
    }

    private static readonly double[] UnstickTurns = [0.785, -0.785, 1.571, -1.571, 2.356, -2.356, 3.1416];

    /// <summary>
    /// O campo aponta para o centro da célula vizinha, mas o monstro pode estar num canto da célula (quina de parede)
    /// onde nem x nem y andam: gira a direção em passos de 45° até achar uma que ande no <see cref="Move"/> (D46).
    /// </summary>
    private void Unstick(MonsterBody m, ref double ux, ref double uy)
    {
        var r = Math.Min(6, m.Radius * 0.5);
        if (AxisMoves(m, ux, uy, r))
        {
            return;
        }

        var a = Math.Atan2(uy, ux);
        foreach (var turn in UnstickTurns)
        {
            var cx = Math.Cos(a + turn);
            var cy = Math.Sin(a + turn);
            if (AxisMoves(m, cx, cy, r))
            {
                ux = cx;
                uy = cy;
                return;
            }
        }
    }

    private bool AxisMoves(MonsterBody m, double ux, double uy, double r)
    {
        const double probe = 2;
        return (Math.Abs(ux) > 0.2 && CanStand(m.X + (ux * probe), m.Y, r, fly: true))
            || (Math.Abs(uy) > 0.2 && CanStand(m.X, m.Y + (uy * probe), r, fly: true));
    }

    /// <summary>Algum prop sólido corta a reta (o monstro empacaria nele).</summary>
    private bool PropOnLine(float x0, float y0, float x1, float y1)
    {
        var a = new Vector2(x0, y0);
        var b = new Vector2(x1, y1);
        for (var i = 0; i < _props.Count; i++)
        {
            var pp = _props[i];
            if (pp.BlocksMovement && Geometry2D.GetClosestPointToSegment(pp.Position, a, b).DistanceTo(pp.Position) < pp.SolidRadius + 5)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Diagnóstico do smoke: o que o campo de perseguição vê na posição do monstro.</summary>
    public string DescribeChase(MonsterBody m)
    {
        if (_chase is null)
        {
            return "sem campo";
        }

        var p = PlayerFoot;
        var ok = _chase.TryDirection((float)m.X, (float)m.Y, out var ux, out var uy);
        return $"dist={_chase.Distance((float)m.X, (float)m.Y)} line={_chase.LineClear((float)m.X, (float)m.Y, p.X, p.Y)} dir={ok}({ux:0.00},{uy:0.00}) stand={CanStand(m.X, m.Y, Math.Min(6, m.Radius * 0.5), m.Fly)} reach={_chase.Reachable((float)m.X, (float)m.Y, p.X, p.Y)}";
    }

    public bool CanStand(double x, double y, double radius, bool fly)
    {
        var fx = (float)x;
        var fy = (float)y;
        var r = (float)radius;
        return Walkable(fx - r, fy, fly) && Walkable(fx + r, fy, fly) && Walkable(fx, fy - (r * 0.6f), fly) && Walkable(fx, fy + (r * 0.6f), fly);
    }

    public bool HurtPlayer(double damage, double sourceX, double sourceY) => PlayerCombat.Hurt(damage, new Vector2((float)sourceX, (float)sourceY));

    public bool HurtPlayer(double damage, Vector2 source) => PlayerCombat.Hurt(damage, source);

    public void FireProjectile(MonsterBody owner, MonsterProjectileKind kind, string color, double x, double y, double vx, double vy, double radius, double damage, double life)
    {
        var at = new Vector2((float)x, (float)y);
        Projectiles.Fire(
            kind == MonsterProjectileKind.Chain ? ProjectileKind.MonsterChain : ProjectileKind.MonsterBolt,
            at,
            new Vector2((float)vx, (float)vy),
            (float)radius,
            damage,
            (float)life,
            CombatPalette.Of(color),
            source: owner,
            origin: at);
    }

    public void Summon(MonsterBody summoner, string monsterId, int count, double minDistance, double yRange, bool chase)
    {
        for (var i = 0; i < count; i++)
        {
            var at = RandPoint((float)minDistance, 8, (float)(summoner.Y - yRange), (float)(summoner.Y + yRange));
            SpawnTelegraphed(monsterId, at, 0.9f, chase);
        }
    }

    public void Sound(MonsterBody m, MonsterSound sound) => Sfx(
        sound switch
        {
            MonsterSound.Growl => "growl",
            MonsterSound.Magic => "magic",
            MonsterSound.Swing => "swing",
            _ => "boom",
        },
        new Vector2((float)m.X, (float)m.Y));

    void IMonsterWorld.Fx(MonsterBody m, MonsterFx fx, double x, double y, double angle)
    {
        var at = new Vector2((float)x, (float)y);
        switch (fx)
        {
            case MonsterFx.SwipeArc:
                Fx.Arc(at, (float)angle, 2.2f, 46, 0.2f, CombatPalette.Brecha, 6, false);
                break;
            case MonsterFx.SweepArc:
                Fx.Arc(at, (float)angle, 3f, 66, 0.25f, CombatPalette.Of(m.Def.Color ?? "#E64A8E"), 8, false);
                break;
            case MonsterFx.SlamImpact:
                Fx.Ring(at, 6, 40, 0.3f, CombatPalette.SlamFill with { A = 0.9f });
                Fx.Burst(at, 18, CombatPalette.Slam, new BurstOptions { V1 = 110, Up = 40, Gravity = 200 });
                break;
            case MonsterFx.SpitFlash:
                Glow.Burst(at, 8, CombatPalette.Single(CombatPalette.Of(m.Def.Bolt ?? "#ff7a2a")), new BurstOptions { V1 = 60 });
                break;
            case MonsterFx.TeleportPuff:
                Glow.Burst(at, 20, CombatPalette.Teleport, new BurstOptions { V1 = 80 });
                break;
            case MonsterFx.ChargeTrail:
                if (_fxRandom.NextDouble() < 0.6)
                {
                    Glow.Burst(at, 2, CombatPalette.Single(CombatPalette.Of(m.Def.Color ?? "#E64A8E")), new BurstOptions { V1 = 40 });
                }

                break;
        }
    }

    public void Shake(double amount) => EventBus.Instance.EmitSignal(EventBus.SignalName.CameraShakeRequested, (float)amount);

    public void Toast(string title, string subtitle) =>
        EventBus.Instance.EmitSignal(EventBus.SignalName.ToastRequested, subtitle.Length > 0 ? $"{title}\n{subtitle}" : title);

    public void Note(string message, string icon) => EventBus.Instance.EmitSignal(EventBus.SignalName.NoteRequested, message, icon);

    public void SpawnLure(double x, double y, double damage) => Hazards.AnglerLure(new Vector2((float)x, (float)y), damage);

    public void SpawnTideWave(double x, double y, double damage) => Hazards.TideWave(new Vector2((float)x, (float)y), damage);

    public void SpawnShard(double x, double y, double delay, double damage, string color) =>
        Hazards.Shard(new Vector2((float)x, (float)y), (float)delay, damage, CombatPalette.Of(color));

    public void SpawnOrbitLantern(MonsterBody boss, double angle, double firstShot) => Hazards.OrbitLantern(boss, (float)angle, (float)firstShot);

    // --- Mundo: andável, pontos aleatórios, props ---

    public bool IsWalkable(float x, float y) => World.WalkArea is { } area && area.IsWalkable(x, y);

    /// <summary><c>walkable(x, y, fly)</c>: área do mapa e, para quem não voa, props sólidos.</summary>
    public bool Walkable(float x, float y, bool fly)
    {
        if (!IsWalkable(x, y))
        {
            return false;
        }

        if (fly)
        {
            return true;
        }

        for (var i = 0; i < _props.Count; i++)
        {
            if (_props[i].Blocks(x, y))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// <c>randPoint</c> (<c>core.js:254-264</c>): até 400 tentativas de um ponto onde dá para ficar (raio
    /// <paramref name="radius"/>), a ≥ <paramref name="minDistance"/> do jogador e longe dos pontos em
    /// <see cref="AvoidPoints"/>. Sem sucesso: 60 u acima do jogador, corrigido para o andável mais perto (D6).
    /// </summary>
    public Vector2 RandPoint(float minDistance, float radius = 8, float y0 = float.NaN, float y1 = float.NaN, bool useAvoid = false)
    {
        var map = World.Map;
        var p = PlayerFoot;
        var top = float.IsNaN(y0) || y0 == 0 ? 10 : y0;
        var bottom = float.IsNaN(y1) || y1 == 0 ? map.ImageHeight - 10 : y1;
        var rng = Rng;
        for (var i = 0; i < 400; i++)
        {
            var x = (float)rng.Range(10, map.ImageWidth - 10);
            var y = (float)rng.Range(top, bottom);
            if (!CanStand(x, y, radius, false))
            {
                continue;
            }

            if (minDistance > 0 && p.DistanceTo(new Vector2(x, y)) < minDistance)
            {
                continue;
            }

            if (useAvoid && IsAvoided(x, y))
            {
                continue;
            }

            if (_chase is not null && !_chase.Reachable(x, y, p.X, p.Y))
            {
                continue; // D43: só onde dá para chegar a pé até o jogador (ilhas da máscara travariam a onda)
            }

            return new Vector2(x, y);
        }

        return World.CorrectToWalkable(new Vector2(p.X, p.Y - 60));
    }

    /// <summary>Pontos (x, y, raio) que <see cref="RandPoint"/> evita com <c>useAvoid</c>.</summary>
    public List<Vector3> AvoidPoints => _avoid;

    private bool IsAvoided(float x, float y)
    {
        foreach (var a in _avoid)
        {
            if (new Vector2(x - a.X, y - a.Y).Length() < a.Z)
            {
                return true;
            }
        }

        return false;
    }

    public DungeonProp AddProp(DungeonProp prop)
    {
        World.Entities.AddChild(prop);
        _props.Add(prop);
        if (prop.BlocksMovement)
        {
            _chase?.SetProp(prop.Position.X, prop.Position.Y, prop.SolidRadius, true);
        }
        if (prop.ChestInteraction is { } chest)
        {
            chest.Callback = () => OpenChest(prop);
        }

        return prop;
    }

    public Pickup AddPickup(Pickup pickup)
    {
        World.Entities.AddChild(pickup);
        _pickups.Add(pickup);
        if (pickup.Interaction is { } it)
        {
            it.Callback = () => CollectByInteract(pickup);
        }

        return pickup;
    }

    public void Sfx(string id, Vector2 at) => AudioManager.Instance?.PlaySfx(id, at);

    // --- Eventos ---

    private void OnMapChanged(string mapId, string mapName)
    {
        // O World já liberou as entidades do mapa anterior.
        _enemies.Clear();
        _pickups.Clear();
        _props.Clear();
        Projectiles.Clear();
        Hazards.Clear();
        Ground.Clear();
        Fx.Clear();
        Glow.Clear();
        Overlay.Clear();
        Clock.Reset();
        if (!_chaseFields.TryGetValue(mapId, out _chase) && World.WalkArea is { } area)
        {
            _chase = new ChaseField(area);
            _chaseFields[mapId] = _chase;
        }

        _chase?.ClearProps();
        _chase?.UpdateNow(PlayerFoot.X, PlayerFoot.Y); // atrás do fade

        PlayerCombat.OnMapChanged();
        Brecha.OnMapChanged(mapId);
    }

    private void OnGameLoaded()
    {
        Brecha.OnGameLoaded();
        PlayerCombat.OnGameLoaded();
    }

    private void OnPauseChanged(bool paused) => _paused = paused;

    private static void RemoveNode(Node node)
    {
        if (IsInstanceValid(node))
        {
            node.GetParent()?.RemoveChild(node);
            node.QueueFree();
        }
    }

    private static GradientTexture2D CreateGlowTexture() => new()
    {
        Gradient = new Gradient
        {
            Offsets = [0f, 1f],
            Colors = [new Color(1, 1, 1, 1), new Color(1, 1, 1, 0)],
        },
        Fill = GradientTexture2D.FillEnum.Radial,
        FillFrom = new Vector2(0.5f, 0.5f),
        FillTo = new Vector2(1f, 0.5f),
        Width = 64,
        Height = 64,
    };
}
