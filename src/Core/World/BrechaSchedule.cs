using Vespera.Core.Common;
using Vespera.Core.Data;
using Vespera.Core.Rules;

namespace Vespera.Core.World;

public enum BrechaStatus
{
    /// <summary>Portal aberto na Rua da Forja, timer correndo.</summary>
    Open,

    /// <summary>O timer zerou: monstros invadem a Rua da Forja até a invasão ser resolvida.</summary>
    Ruptured,

    /// <summary>Fechada (chefe derrotado, Casa NPC ou invasão resolvida).</summary>
    Closed,
}

/// <summary>Por que uma Brecha mudou de estado.</summary>
public enum BrechaEventKind
{
    Opened,

    /// <summary>Uma Casa NPC fechou a Brecha (<c>npcAt</c>).</summary>
    ClosedByCasa,

    /// <summary>O timer zerou sem ninguém entrar: ruptura.</summary>
    Ruptured,

    /// <summary>O jogador derrotou o chefe.</summary>
    Cleared,

    /// <summary>A invasão da ruptura foi resolvida.</summary>
    InvasionResolved,
}

/// <summary>Uma Brecha da sessão (<c>b</c> de <c>openBrecha</c>, <c>world.js:34-48</c>).</summary>
public sealed class BrechaInstance
{
    public required int Id { get; init; }

    public required ThemeDef Theme { get; init; }

    public string ThemeId => Theme.Id;

    public string Bairro => Theme.Bairro;

    public required SeasonDef Season { get; init; }

    /// <summary>Título por tema e estação (ex.: "Cais Enevoado").</summary>
    public required string Title { get; init; }

    /// <summary>Índice do rank (0 = E … 9 = Alvor).</summary>
    public required int Rank { get; init; }

    /// <summary>Spot do portal na Rua da Forja (0 ou 1).</summary>
    public required int Spot { get; init; }

    /// <summary>Segundos de sessão em que abriu (<c>openT</c>).</summary>
    public required double OpenedAt { get; init; }

    /// <summary>Tempo total até romper.</summary>
    public required double MaxTime { get; init; }

    /// <summary>Segundos até romper (<c>b.t</c>). Só desce com o portal aberto e o jogador fora dela.</summary>
    public double TimeLeft { get; internal set; }

    /// <summary>Quando <see cref="TimeLeft"/> chega aqui, uma Casa fecha a Brecha (null = ninguém fecha).</summary>
    public double? NpcCloseAt { get; init; }

    public BrechaStatus Status { get; internal set; } = BrechaStatus.Open;

    /// <summary>Casa que fechou (só com <see cref="BrechaEventKind.ClosedByCasa"/>).</summary>
    public string? ClosedBy { get; internal set; }

    public bool IsOpen => Status == BrechaStatus.Open;
}

public readonly record struct BrechaEvent(BrechaEventKind Kind, BrechaInstance Brecha);

/// <summary>
/// Agenda das Brechas da sessão (spec 01 §1.9; <c>updWorld</c>, <c>openBrecha</c>, <c>rupture</c>, <c>world.js:34-155</c>):
/// quando a próxima abre, timers, fechamento por Casa NPC, ruptura e os atalhos de <c>nextBrecha</c> depois de
/// cada desfecho. Não salva nada (a v2 não salva Brechas); só o <c>firstDone</c> vai para o save (D6).
/// Sorteios na ordem do protótipo, pelo <see cref="IRandom"/> injetado.
/// </summary>
public sealed class BrechaSchedule
{
    /// <summary>Brechas fechadas somem da lista 600 s depois de abrirem.</summary>
    public const double ClosedKeepSeconds = 600;

    /// <summary>A lista guarda só as 5 mais recentes (<c>slice(-5)</c>).</summary>
    public const int MaxListed = 5;

    /// <summary>Dia novo: a próxima abre em no máximo 15 s (<c>newDay</c>).</summary>
    public const double NewDayMaxDelay = 15;

    private static readonly NumRange AfterCasaClose = new(40, 70);
    private static readonly NumRange AfterFinishRun = new(30, 50);
    private static readonly NumRange AfterInvasion = new(35, 55);

    private readonly GameDatabase _db;
    private readonly BrechaRules _rules;
    private readonly SeasonCalendar _calendar;
    private readonly IRandom _random;
    private readonly List<BrechaInstance> _brechas = [];
    private readonly List<string> _openThemes = [];
    private int _nextId = 1;

    public BrechaSchedule(GameDatabase db, IRandom random, bool firstDone)
    {
        _db = db;
        _rules = new BrechaRules(db);
        _calendar = new SeasonCalendar(db.SeasonData);
        _random = random;
        FirstDone = firstDone;
        NextOpenIn = _rules.InitialOpenDelay(newGame: !firstDone);
    }

    /// <summary>Segundos de sessão (<c>G.t</c>) contados pelo <see cref="Tick"/>.</summary>
    public double Time { get; private set; }

    /// <summary>Segundos até a próxima abertura (<c>nextBrecha</c>).</summary>
    public double NextOpenIn { get; private set; }

    /// <summary>A primeira Brecha (Mercado, 300 s, sem bônus de rank) já abriu.</summary>
    public bool FirstDone { get; private set; }

    /// <summary>Brecha em que o jogador está (o timer dela não corre), ou null.</summary>
    public BrechaInstance? Running { get; private set; }

    /// <summary>Brechas listadas (mais antiga primeiro), como <c>G.brechas</c>.</summary>
    public IReadOnlyList<BrechaInstance> Brechas => _brechas;

    public int OpenCount
    {
        get
        {
            var n = 0;
            foreach (var b in _brechas)
            {
                if (b.IsOpen)
                {
                    n++;
                }
            }

            return n;
        }
    }

    /// <summary>Brecha aberta no spot, ou null.</summary>
    public BrechaInstance? OpenAt(int spot)
    {
        foreach (var b in _brechas)
        {
            if (b.IsOpen && b.Spot == spot)
            {
                return b;
            }
        }

        return null;
    }

    public BrechaInstance? Find(int id)
    {
        foreach (var b in _brechas)
        {
            if (b.Id == id)
            {
                return b;
            }
        }

        return null;
    }

    /// <summary>A Brecha que rompeu e ainda não foi resolvida, ou null.</summary>
    public BrechaInstance? Ruptured
    {
        get
        {
            foreach (var b in _brechas)
            {
                if (b.Status == BrechaStatus.Ruptured)
                {
                    return b;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Avança <paramref name="dt"/> s (<c>updWorld</c>): abre a próxima na hora, desconta os timers das Brechas abertas
    /// em que o jogador não está, fecha por Casa ou rompe. Os acontecimentos vão para <paramref name="events"/>
    /// (a lista não é limpa aqui, para não alocar).
    /// </summary>
    public void Tick(double dt, int playerRankIndex, int day, List<BrechaEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        Time += dt;
        NextOpenIn -= dt;
        if (NextOpenIn <= 0)
        {
            if (TryOpen(playerRankIndex, day) is { } opened)
            {
                events.Add(new BrechaEvent(BrechaEventKind.Opened, opened));
            }

            NextOpenIn = _rules.RollOpenDelay(_random);
        }

        foreach (var b in _brechas)
        {
            if (!b.IsOpen || b == Running || b.OpenedAt >= Time)
            {
                continue; // a que abriu neste passo começa a contar no próximo
            }

            b.TimeLeft -= dt;
            if (b.NpcCloseAt is { } npcAt && b.TimeLeft <= npcAt)
            {
                b.Status = BrechaStatus.Closed;
                b.ClosedBy = _random.Pick(_db.Social.Casas);
                events.Add(new BrechaEvent(BrechaEventKind.ClosedByCasa, b));
                NextOpenIn = Math.Min(NextOpenIn, _random.Range(AfterCasaClose));
            }
            else if (b.TimeLeft <= 0)
            {
                b.TimeLeft = 0;
                b.Status = BrechaStatus.Ruptured;
                events.Add(new BrechaEvent(BrechaEventKind.Ruptured, b));
            }
        }

        Prune();
    }

    /// <summary>
    /// <c>openBrecha(first)</c>: no máximo 2 abertas; spot livre; tema, rank, timer e <c>npcAt</c> pelas
    /// <see cref="BrechaRules"/>. Devolve null se já houver 2 abertas.
    /// </summary>
    public BrechaInstance? TryOpen(int playerRankIndex, int day)
    {
        _openThemes.Clear();
        var usedSpot0 = false;
        var usedSpot1 = false;
        foreach (var b in _brechas)
        {
            if (!b.IsOpen)
            {
                continue;
            }

            _openThemes.Add(b.ThemeId);
            usedSpot0 |= b.Spot == 0;
            usedSpot1 |= b.Spot == 1;
        }

        if (_openThemes.Count >= _rules.Def.MaxOpen || (usedSpot0 && usedSpot1))
        {
            return null;
        }

        var first = !FirstDone;
        var theme = _rules.ChooseTheme(playerRankIndex, _openThemes, first, _random);
        var season = _calendar.SeasonOf(day);
        var rank = _rules.RollRank(theme, playerRankIndex, first, _random);
        var timer = _rules.RollTimer(first, _random);
        var npcAt = _rules.RollNpcCloseAt(first, _random);
        var brecha = new BrechaInstance
        {
            Id = _nextId++,
            Theme = theme,
            Season = season,
            Title = BrechaRules.Title(theme, season.Id),
            Rank = rank,
            Spot = usedSpot0 ? 1 : 0,
            OpenedAt = Time,
            MaxTime = timer,
            TimeLeft = timer,
            NpcCloseAt = npcAt,
        };
        _brechas.Add(brecha);
        FirstDone = true;
        return brecha;
    }

    /// <summary>O jogador entrou pelo portal do <paramref name="spot"/>: o timer dela para (<c>G.run.b === b</c>).</summary>
    public BrechaInstance? EnterRun(int spot)
    {
        Running = OpenAt(spot);
        return Running;
    }

    /// <summary>
    /// A caçada terminou. <paramref name="cleared"/> = chefe derrotado: a Brecha fecha e a próxima abre em até
    /// <c>R(30, 50)</c> s (<c>bossDefeated</c> + <c>finishRun</c>). Fuga ou morte: a Brecha continua aberta e o timer volta a correr.
    /// </summary>
    public BrechaInstance? EndRun(bool cleared)
    {
        var run = Running;
        Running = null;
        if (run is null || !cleared)
        {
            return run;
        }

        run.Status = BrechaStatus.Closed;
        NextOpenIn = Math.Min(NextOpenIn, _random.Range(AfterFinishRun));
        return run;
    }

    /// <summary>Invasão resolvida (<c>resolveInvasion</c>): a Brecha rompida fecha; a próxima abre em até <c>R(35, 55)</c> s.</summary>
    public BrechaInstance? ResolveInvasion()
    {
        var ruptured = Ruptured;
        if (ruptured is null)
        {
            return null;
        }

        ruptured.Status = BrechaStatus.Closed;
        NextOpenIn = Math.Min(NextOpenIn, _random.Range(AfterInvasion));
        return ruptured;
    }

    /// <summary>Dia novo: a próxima Brecha abre em no máximo 15 s.</summary>
    public void OnNewDay() => NextOpenIn = Math.Min(NextOpenIn, NewDayMaxDelay);

    /// <summary>Para testes e o smoke: encurta a espera até a próxima abertura.</summary>
    public void OpenSoon(double seconds) => NextOpenIn = Math.Min(NextOpenIn, seconds);

    private void Prune()
    {
        for (var i = _brechas.Count - 1; i >= 0; i--)
        {
            var b = _brechas[i];
            if (b.Status == BrechaStatus.Closed && Time - b.OpenedAt >= ClosedKeepSeconds && b != Running)
            {
                _brechas.RemoveAt(i);
            }
        }

        // slice(-5): sai a mais antiga, mas nunca uma aberta, rompida ou a da caçada em curso.
        for (var i = 0; i < _brechas.Count && _brechas.Count > MaxListed;)
        {
            var b = _brechas[i];
            if (b.Status == BrechaStatus.Closed && b != Running)
            {
                _brechas.RemoveAt(i);
            }
            else
            {
                i++;
            }
        }
    }
}
