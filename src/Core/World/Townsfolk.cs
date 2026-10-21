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
/// velocidade da v2, espera <c>R(2, 5)</c> s e olha para baixo. Diferenças pretendidas (D6/D41): começa num waypoint
/// sorteado e vai para o seguinte (a v2 sorteava ponto e alvo separados), segue o <see cref="WalkerRoute"/> e anda com
/// <see cref="WalkMover"/>, então nunca atravessa parede.
/// </summary>
public sealed class TownWalker
{
    /// <summary>Chegou ao waypoint a menos de 3 u (<c>d &lt; 3</c>).</summary>
    public const float ArriveDistance = 3f;

    /// <summary>Falar com o NPC o faz esperar 4 s (<c>c.wait = 4</c>).</summary>
    public const double TalkWaitSeconds = 4;

    private static readonly NumRange WaitAtWaypoint = new(2, 5);
    private static readonly NumRange InitialWait = new(0, 2);

    private readonly IRandom _random;
    private int _legPoint;

    public TownWalker(WalkerDef def, WalkerRoute route, IRandom random)
    {
        Def = def;
        Route = route;
        _random = random;
        var n = route.Waypoints.Count;
        var start = n == 0 ? 0 : random.RangeInt(0, n - 1);
        Leg = start;
        var at = n == 0 ? new MapPoint((float)def.Path[0].X, (float)def.Path[0].Y) : route.Waypoints[start];
        X = at.X;
        Y = at.Y;
        Wait = random.Range(InitialWait);
        _legPoint = 1;
    }

    public WalkerDef Def { get; }

    public WalkerRoute Route { get; }

    public float X { get; private set; }

    public float Y { get; private set; }

    /// <summary>Trecho atual: de <c>Waypoints[Leg]</c> para o seguinte.</summary>
    public int Leg { get; private set; }

    public double Wait { get; private set; }

    public TownFacing Facing { get; private set; } = TownFacing.Down;

    public double WalkT { get; private set; }

    public bool IsMoving => Wait <= 0;

    /// <summary>Próxima fala (<c>c.line++ % n</c>).</summary>
    public int LineIndex { get; private set; }

    public void Update(IWalkArea area, double dt)
    {
        ArgumentNullException.ThrowIfNull(area);
        if (Wait > 0)
        {
            Wait -= dt;
            return;
        }

        var leg = Route.Legs.Count > 0 ? Route.Legs[Leg] : null;
        if (leg is null || _legPoint >= leg.Count)
        {
            ArriveAtWaypoint();
            return;
        }

        var target = leg[_legPoint];
        var dx = target.X - X;
        var dy = target.Y - Y;
        var d = WalkMover.Hypot(dx, dy);
        var isLast = _legPoint == leg.Count - 1;
        if (d < (isLast ? ArriveDistance : 1f))
        {
            _legPoint++;
            if (_legPoint >= leg.Count)
            {
                ArriveAtWaypoint();
            }

            return;
        }

        var step = (float)(Def.Speed * dt);
        if (step > d)
        {
            step = d;
        }

        (X, Y) = WalkMover.Move(area, X, Y, dx / d * step, dy / d * step);
        WalkT += dt;
        Facing = MathF.Abs(dx) > MathF.Abs(dy) ? (dx < 0 ? TownFacing.Left : TownFacing.Right) : (dy < 0 ? TownFacing.Up : TownFacing.Down);
    }

    /// <summary>
    /// Jogador falou com o NPC: devolve a fala da vez, o NPC para 4 s e vira para o jogador (na v2 virava para baixo).
    /// </summary>
    public string Talk(float playerX, float playerY)
    {
        Wait = Math.Max(Wait, TalkWaitSeconds);
        var dx = playerX - X;
        var dy = playerY - Y;
        Facing = MathF.Abs(dy) >= MathF.Abs(dx) ? (dy < 0 ? TownFacing.Up : TownFacing.Down) : (dx < 0 ? TownFacing.Left : TownFacing.Right);
        var line = Def.Lines[LineIndex % Def.Lines.Count];
        LineIndex++;
        return line;
    }

    private void ArriveAtWaypoint()
    {
        var n = Math.Max(1, Route.Waypoints.Count);
        Leg = (Leg + 1) % n;
        _legPoint = 1;
        Wait = _random.Range(WaitAtWaypoint);
        Facing = TownFacing.Down;
    }
}

/// <summary>
/// Caçador ambiente (spec 01 §6.2, <c>updHunters</c>, <c>world.js:169-179</c>, <c>main.js:109</c>): espera, sorteia um
/// ponto do mapa, anda a 42 u/s deslizando nas paredes; ao chegar espera <c>R(1,5; 5)</c> s e às vezes fala; travado,
/// espera 0,5 s e sorteia outro ponto. Não é interagível.
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

    public AmbientHunter(HunterDef def, float x, float y, SocialFile social, IRandom random)
    {
        Def = def;
        _social = social;
        _random = random;
        X = TargetX = x;
        Y = TargetY = y;
        Wait = random.Range(FirstWait);
    }

    public HunterDef Def { get; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float TargetX { get; private set; }

    public float TargetY { get; private set; }

    public double Wait { get; private set; }

    public TownFacing Facing { get; private set; } = TownFacing.Down;

    public double WalkT { get; private set; }

    public bool IsMoving => Wait <= 0;

    /// <summary>Fala atual (null = sem balão).</summary>
    public string? Say { get; private set; }

    public double SayT { get; private set; }

    /// <summary>Mostra um balão por 3 s (chat simulado ou fala aleatória).</summary>
    public void Speak(string text)
    {
        Say = text;
        SayT = SayDuration;
    }

    public void Update(IWalkArea area, float mapWidth, float mapHeight, double dt)
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
            if (Wait <= 0)
            {
                var q = WalkMover.RandomPoint(area, mapWidth, mapHeight, _random, X, Y, radius: 6f);
                TargetX = q.X;
                TargetY = q.Y;
            }

            return;
        }

        var dx = TargetX - X;
        var dy = TargetY - Y;
        var d = WalkMover.Hypot(dx, dy);
        if (d < TownWalker.ArriveDistance)
        {
            Wait = _random.Range(ArrivedWait);
            Facing = TownFacing.Down;
            if (_random.Chance(IdleLineChance))
            {
                Speak(_random.Pick(_social.IdleLines));
            }

            return;
        }

        var ox = X;
        var oy = Y;
        var step = (float)(Speed * dt);
        (X, Y) = WalkMover.Move(area, X, Y, dx / d * step, dy / d * step);
        WalkT += dt;
        if (WalkMover.Hypot(X - ox, Y - oy) < 0.2f * dt * Speed)
        {
            Wait = StuckWait;
        }

        Facing = MathF.Abs(dx) > MathF.Abs(dy) ? (dx < 0 ? TownFacing.Left : TownFacing.Right) : (dy < 0 ? TownFacing.Up : TownFacing.Down);
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
