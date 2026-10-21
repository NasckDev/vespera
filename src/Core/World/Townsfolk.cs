using Vespera.Core.Common;
using Vespera.Core.Data;

namespace Vespera.Core.World;

/// <summary>Para onde um NPC/caçador olha (<c>dir</c> da v2).</summary>
public enum TownFacing
{
    Down,
    Up,
    Left,
    Right,
}

/// <summary>
/// NPC com rotina (spec 01 §6.1, <c>updNpcs</c>, <c>world.js:159-168</c>): espera, anda até o próximo waypoint na
/// velocidade da v2, espera <c>R(2, 5)</c> s e olha para baixo. Diferenças pretendidas (D6/B2-D41/B2-D48): começa num waypoint
/// sorteado e vai para o seguinte (a v2 sorteava ponto e alvo separados), segue o <see cref="WalkerRoute"/> com o
/// <see cref="WalkFollower"/> (nunca atravessa parede, desvia de quem está no caminho) e, se travar, replaneja dali com o
/// <see cref="WalkPlanner"/>; sem caminho depois de <see cref="MaxReplans"/> tentativas, desiste do trecho e segue para
/// o próximo waypoint.
/// </summary>
public sealed class TownWalker
{
    /// <summary>Chegou ao waypoint a menos de 3 u (<c>d &lt; 3</c>).</summary>
    public const float ArriveDistance = 3f;

    /// <summary>Falar com o NPC o faz esperar 4 s (<c>c.wait = 4</c>).</summary>
    public const double TalkWaitSeconds = 4;

    /// <summary>Replanejamentos por trecho antes de desistir dele.</summary>
    public const int MaxReplans = 3;

    private static readonly NumRange WaitAtWaypoint = new(2, 5);
    private static readonly NumRange InitialWait = new(0, 2);

    private readonly IRandom _random;
    private readonly WalkFollower _body;
    private bool _legStarted;
    private int _replans;

    public TownWalker(WalkerDef def, WalkerRoute route, IRandom random, WalkPlanner? planner = null)
    {
        Def = def;
        Route = route;
        _random = random;
        Planner = planner;
        var n = route.Waypoints.Count;
        var start = n == 0 ? 0 : random.RangeInt(0, n - 1);
        Leg = start;
        var at = n == 0 ? new MapPoint((float)def.Path[0].X, (float)def.Path[0].Y) : route.Waypoints[start];
        _body = new WalkFollower(at.X, at.Y);
        Wait = random.Range(InitialWait);
    }

    public WalkerDef Def { get; }

    public WalkerRoute Route { get; }

    /// <summary>Planejador da área atual (criado sob demanda; o diretor compartilha um por mapa).</summary>
    public WalkPlanner? Planner { get; set; }

    public float X => _body.X;

    public float Y => _body.Y;

    /// <summary>Trecho atual: de <c>Waypoints[Leg]</c> para o seguinte.</summary>
    public int Leg { get; private set; }

    public double Wait { get; private set; }

    public TownFacing Facing => _body.Facing;

    public double WalkT => _body.WalkT;

    public bool IsMoving => Wait <= 0;

    /// <summary>Quantas vezes replanejou desde que nasceu (testes).</summary>
    public int Replans { get; private set; }

    /// <summary>Caminho que está seguindo agora.</summary>
    public IReadOnlyList<MapPoint> CurrentPath => _body.Path;

    /// <summary>Próxima fala (<c>c.line++ % n</c>).</summary>
    public int LineIndex { get; private set; }

    /// <summary>
    /// Um passo. <paramref name="others"/> são as posições dos outros corpos do mapa (moradores e jogador) e
    /// <paramref name="self"/> o índice deste NPC nela (−1 se não está).
    /// </summary>
    public void Update(IWalkArea area, double dt, IReadOnlyList<MapPoint>? others = null, int self = -1)
    {
        ArgumentNullException.ThrowIfNull(area);
        if (Wait > 0)
        {
            Wait -= dt;
            _body.Separate(area, others, self, dt);
            return;
        }

        if (Route.Waypoints.Count < 2)
        {
            Wait = _random.Range(WaitAtWaypoint);
            return;
        }

        if (!_legStarted && !StartLeg(area))
        {
            return;
        }

        switch (_body.Advance(area, (float)Def.Speed, dt, others, self, ArriveDistance))
        {
            case WalkStatus.Arrived:
                ArriveAtWaypoint();
                break;
            case WalkStatus.Yielding:
                Wait = WalkFollower.YieldSeconds;
                break;
            case WalkStatus.Stuck:
                Replan(area);
                break;
        }
    }

    /// <summary>
    /// Jogador falou com o NPC: devolve a fala da vez, o NPC para 4 s e vira para o jogador (na v2 virava para baixo).
    /// </summary>
    public string Talk(float playerX, float playerY)
    {
        Wait = Math.Max(Wait, TalkWaitSeconds);
        var dx = playerX - X;
        var dy = playerY - Y;
        _body.Facing = MathF.Abs(dy) >= MathF.Abs(dx) ? (dy < 0 ? TownFacing.Up : TownFacing.Down) : (dx < 0 ? TownFacing.Left : TownFacing.Right);
        var line = Def.Lines[LineIndex % Def.Lines.Count];
        LineIndex++;
        return line;
    }

    private MapPoint Target => Route.Waypoints[(Leg + 1) % Route.Waypoints.Count];

    private WalkPlanner PlannerFor(IWalkArea area)
    {
        if (Planner is null || !ReferenceEquals(Planner.Area, area))
        {
            Planner = new WalkPlanner(area, _body.Radius);
        }

        return Planner;
    }

    /// <summary>
    /// Começa o trecho: usa o percurso pronto quando dá para pegá-lo daqui em linha reta; senão (desviou, desistiu do
    /// trecho anterior, o percurso não existe nesta máscara) planeja daqui até o próximo waypoint.
    /// </summary>
    private bool StartLeg(IWalkArea area)
    {
        _legStarted = true;
        _replans = 0;
        if (!area.CanStand(X, Y, _body.Radius) && PlannerFor(area).Settle(new MapPoint(X, Y), 24f) is { } safe)
        {
            _body.Place(safe.X, safe.Y); // nunca deveria acontecer; não deixa o NPC preso dentro da parede
        }

        var leg = Route.Legs[Leg];
        if (leg is { Count: > 1 })
        {
            var here = new MapPoint(X, Y);
            if (WalkMover.Hypot(leg[0].X - X, leg[0].Y - Y) < 0.5f)
            {
                _body.SetPath(leg, 1);
                return true;
            }

            var planner = PlannerFor(area);
            if (planner.IsClear(here, leg[1]))
            {
                _body.SetPath(leg, 1);
                return true;
            }

            if (planner.IsClear(here, leg[0]))
            {
                _body.SetPath(leg, 0);
                return true;
            }
        }

        return Replan(area);
    }

    /// <summary>Replaneja daqui até o waypoint do trecho; sem caminho (ou travou demais), desiste dele.</summary>
    private bool Replan(IWalkArea area)
    {
        _replans++;
        Replans++;
        if (_replans <= MaxReplans && _body.Plan(PlannerFor(area), Target))
        {
            return true;
        }

        ArriveAtWaypoint();
        return false;
    }

    private void ArriveAtWaypoint()
    {
        var n = Math.Max(1, Route.Waypoints.Count);
        Leg = (Leg + 1) % n;
        _legStarted = false;
        _body.ClearPath();
        Wait = _random.Range(WaitAtWaypoint);
        _body.Facing = TownFacing.Down;
    }
}

/// <summary>
/// Caçador ambiente (spec 01 §6.2, <c>updHunters</c>, <c>world.js:169-179</c>, <c>main.js:109</c>): espera, sorteia um
/// ponto do mapa e anda até ele a 42 u/s; ao chegar espera <c>R(1,5; 5)</c> s e às vezes fala; travado, espera 0,5 s e
/// sorteia outro ponto. Não é interagível. B2-D48: em vez da reta que escorrega na parede (e empacava em toda quina),
/// vai pelo caminho do <see cref="WalkPlanner"/> com o <see cref="WalkFollower"/> (desvia de quem está na frente).
/// </summary>
public sealed class AmbientHunter
{
    public const float Speed = 42f;

    /// <summary>Balão dura 3 s.</summary>
    public const double SayDuration = 3;

    /// <summary>Chance por segundo de um balão aleatório (<c>dt · 0,04</c>).</summary>
    public const double RandomLinePerSecond = 0.04;

    /// <summary>Chance de falar ao chegar ao destino.</summary>
    public const double IdleLineChance = 0.25;

    private static readonly NumRange FirstWait = new(0, 3);
    private static readonly NumRange ArrivedWait = new(1.5, 5);
    private const double StuckWait = 0.5;

    private readonly IRandom _random;
    private readonly SocialFile _social;
    private readonly WalkFollower _body;
    private double _hold;

    public AmbientHunter(HunterDef def, float x, float y, SocialFile social, IRandom random, WalkPlanner? planner = null)
    {
        Def = def;
        _social = social;
        _random = random;
        Planner = planner;
        _body = new WalkFollower(x, y);
        TargetX = x;
        TargetY = y;
        Wait = random.Range(FirstWait);
    }

    public HunterDef Def { get; }

    /// <summary>Planejador da área atual (criado sob demanda; o diretor compartilha um por mapa).</summary>
    public WalkPlanner? Planner { get; set; }

    public float X => _body.X;

    public float Y => _body.Y;

    public float TargetX { get; private set; }

    public float TargetY { get; private set; }

    public double Wait { get; private set; }

    public TownFacing Facing => _body.Facing;

    public double WalkT => _body.WalkT;

    public bool IsMoving => Wait <= 0 && _hold <= 0;

    /// <summary>Fala atual (null = sem balão).</summary>
    public string? Say { get; private set; }

    public double SayT { get; private set; }

    /// <summary>Mostra um balão por 3 s (chat simulado ou fala aleatória).</summary>
    public void Speak(string text)
    {
        Say = text;
        SayT = SayDuration;
    }

    /// <summary>
    /// Um passo. <paramref name="others"/> são as posições dos outros corpos do mapa e <paramref name="self"/> o índice
    /// deste caçador nela (−1 se não está).
    /// </summary>
    public void Update(IWalkArea area, float mapWidth, float mapHeight, double dt, IReadOnlyList<MapPoint>? others = null, int self = -1)
    {
        ArgumentNullException.ThrowIfNull(area);
        SayT -= dt;
        if (SayT <= 0)
        {
            Say = null;
        }

        if (Say is null && _random.Chance(dt * RandomLinePerSecond))
        {
            Speak(_random.Pick(_social.RandomLines));
        }

        if (Wait > 0)
        {
            Wait -= dt;
            _body.Separate(area, others, self, dt);
            if (Wait <= 0)
            {
                var q = WalkMover.RandomPoint(area, mapWidth, mapHeight, _random, X, Y, radius: 6f);
                TargetX = q.X;
                TargetY = q.Y;
                if (Planner is null || !ReferenceEquals(Planner.Area, area))
                {
                    Planner = new WalkPlanner(area, _body.Radius);
                }

                if (!_body.Plan(Planner, q))
                {
                    Wait = StuckWait;
                }
            }

            return;
        }

        if (_hold > 0)
        {
            _hold -= dt;
            return;
        }

        switch (_body.Advance(area, Speed, dt, others, self, TownWalker.ArriveDistance))
        {
            case WalkStatus.Arrived:
                Wait = _random.Range(ArrivedWait);
                _body.Facing = TownFacing.Down;
                if (_random.Chance(IdleLineChance))
                {
                    Speak(_random.Pick(_social.IdleLines));
                }

                break;
            case WalkStatus.Yielding:
                _hold = WalkFollower.YieldSeconds;
                break;
            case WalkStatus.Stuck:
                _body.ClearPath();
                Wait = StuckWait;
                break;
        }
    }

    /// <summary>
    /// Quantos caçadores ambiente o mapa recebe (<c>setupMap</c>, <c>world.js:15</c>): lojas 0; Guilda 1 depois da
    /// primeira Brecha fechada; demais <c>RI(1, 2)</c>; Brechas 0.
    /// </summary>
    public static int CountFor(MapDef map, int brechasClosed, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(random);
        if (map.Dungeon || map.Shop)
        {
            return 0;
        }

        if (map.Id == "guild")
        {
            return brechasClosed > 0 ? 1 : 0;
        }

        return random.RangeInt(1, 2);
    }

    /// <summary>
    /// Sorteia <paramref name="count"/> caçadores sem repetir, fora quem usa a arma do jogador
    /// (<c>h[2] !== G.s.cur</c>). Embaralha como a v2 (Fisher–Yates em vez do <c>sort</c> aleatório, D6).
    /// </summary>
    public static List<HunterDef> Pick(SocialFile social, string playerWeapon, int count, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(social);
        var pool = social.Hunters.Where(h => h.Weapon != playerWeapon).ToList();
        for (var i = pool.Count - 1; i > 0; i--)
        {
            var j = random.RangeInt(0, i);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool.Take(Math.Max(0, count)).ToList();
    }
}

/// <summary>Chat simulado de Lumeira (spec 01 §6.3, <c>world.js:154</c>, <c>openBrecha</c>, <c>rupture</c>).</summary>
public static class TownChat
{
    /// <summary>Primeira mensagem 7 s depois de começar (<c>chatT = 7</c>).</summary>
    public const double FirstDelay = 7;

    /// <summary>Intervalo entre mensagens: <c>R(14, 28)</c> s.</summary>
    public static readonly NumRange Interval = new(14, 28);

    /// <summary>Uma linha de <c>CHAT</c> com <c>{b}</c> e <c>{c}</c> preenchidos.</summary>
    public static string RandomLine(SocialFile social, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(social);
        var line = random.Pick(social.Chat);
        var bairro = random.Pick(social.ChatBairros);
        var casa = random.Pick(social.Casas);
        return line.Replace("{b}", bairro, StringComparison.Ordinal).Replace("{c}", casa, StringComparison.Ordinal);
    }

    /// <summary>Comentário de um caçador quando uma Brecha abre (<c>world.js:46</c>).</summary>
    public static string OpenedLine(string bairro, IRandom random) => random.RangeInt(0, 2) switch
    {
        0 => $"abriu uma no {bairro}!",
        1 => $"Brecha nova, {bairro}. quem vem?",
        _ => $"de novo o {bairro}...",
    };

    /// <summary>Uma Casa fechou a Brecha (<c>world.js:149</c>).</summary>
    public static string ClosedByCasaLine(string casa, string bairro) => $"a {casa} fechou a Brecha do {bairro}";

    /// <summary>Ruptura: grito da Iara (<c>world.js:52</c>).</summary>
    public const string RuptureLine = "ROMPEU! tem lobo na Rua da Forja, todo mundo pra lá";

    /// <summary>Ruptura: aviso da Lúcia (<c>world.js:52</c>).</summary>
    public const string RuptureLuciaLine = "Tranquem as portas. O pão fica mais caro até isso passar.";
}
