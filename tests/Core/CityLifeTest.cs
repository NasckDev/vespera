namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Common;
using Vespera.Core.Data;
using Vespera.Core.World;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>
/// Cidade viva (spec 01 §4.5, §6, §7, §1.9): fases do dia, escuridão por hora e mapa, lâmpadas, agenda das Brechas,
/// NPCs com rotina e caçadores ambiente sem atravessar parede.
/// </summary>
[TestSuite]
public class CityLifeTest
{
    private static readonly Lazy<MapCatalog> Real = new(() =>
        MapCatalog.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "maps.json"))));

    private static MapCatalog Maps => Real.Value;

    private static ClockDef Clock => TestDb.Instance.SeasonData.Clock;

    // --- Fases do dia ---

    [TestCase(960, DayPhase.Vespera)]   // 16:00, começo do dia
    [TestCase(990, DayPhase.Vespera)]   // 16:30, jogo novo
    [TestCase(1139, DayPhase.Vespera)]  // 18:59
    [TestCase(1140, DayPhase.Night)]    // 19:00
    [TestCase(1500, DayPhase.Night)]    // 01:00
    [TestCase(1739, DayPhase.Night)]    // 04:59
    [TestCase(1740, DayPhase.Dawn)]     // 05:00
    [TestCase(1799, DayPhase.Dawn)]     // 05:59
    public void DayPhaseFollowsTheClock(double clock, DayPhase expected)
        => AssertThat(DayPhases.Of(clock, Clock)).IsEqual(expected);

    // --- Escuridão ---

    [TestCase(16 * 60, 0.16)]
    [TestCase(18 * 60, 0.33)]
    [TestCase(19 * 60, 0.50)]
    [TestCase(22 * 60, 0.62)]
    [TestCase(26 * 60, 0.62)]
    [TestCase(29 * 60, 0.62)]
    [TestCase(29.5 * 60, 0.56)]
    [TestCase(30 * 60, 0.50)]
    public void BaseDarknessTable(double clock, double expected)
        => AssertThat(CityLighting.BaseDarkness(clock)).IsEqualApprox((float)expected, 0.0001f);

    [TestCase]
    public void DarknessHasNoJumpAtTenPm()
    {
        // B2-D39: a v2 caía de 0,66 para 0,62 às 22:00; aqui a curva é contínua.
        var before = CityLighting.BaseDarkness((22 * 60) - 0.01);
        var after = CityLighting.BaseDarkness(22 * 60);
        AssertThat(Math.Abs(before - after)).IsLess(0.001f);
        for (var m = 16 * 60.0; m < 30 * 60; m += 1)
        {
            AssertThat(Math.Abs(CityLighting.BaseDarkness(m + 1) - CityLighting.BaseDarkness(m))).IsLess(0.01f);
        }
    }

    [TestCase]
    public void DarknessByMapKind()
    {
        // Tabela da spec 01 §4.5 às 16:00 (base 0,16).
        AssertThat(CityLighting.Darkness(Maps["forge"], 960)).IsEqualApprox(0.096f, 0.0001f);
        AssertThat(CityLighting.Darkness(Maps["guild"], 960)).IsEqualApprox(0.228f, 0.0001f);
        AssertThat(CityLighting.Darkness(Maps["inn"], 960)).IsEqualApprox(0.228f, 0.0001f);
        AssertThat(CityLighting.Darkness(Maps["shop_arm"], 960)).IsEqualApprox(0.192f, 0.0001f);

        // Noite fechada (0,62). Rua da Forja: 0,62 × 1,15 (B2-D51; a v2 parava em 0,372).
        AssertThat(CityLighting.Darkness(Maps["forge"], 24 * 60)).IsEqualApprox(0.713f, 0.0001f);
        AssertThat(CityLighting.Darkness(Maps["guild"], 24 * 60)).IsEqualApprox(0.481f, 0.0001f);
        AssertThat(CityLighting.Darkness(Maps["shop_rel"], 24 * 60)).IsEqualApprox(0.399f, 0.0001f);

        // Brechas não seguem o relógio.
        AssertThat(CityLighting.Darkness(Maps["fish2"], 960)).IsEqual(0.26f);
        AssertThat(CityLighting.Darkness(Maps["cath"], 24 * 60)).IsEqual(0.26f);
        AssertThat(CityLighting.Darkness(Maps["core"], 24 * 60)).IsEqual(0.40f);
    }

    [TestCase]
    public void StreetNightIsAnEventButStaysContinuous()
    {
        // B2-D51: dia igual à v2, véspera escurecendo de verdade, noite fechada 0,713, sem saltos minuto a minuto.
        var forge = Maps["forge"];
        AssertThat(CityLighting.Darkness(forge, 16 * 60)).IsEqualApprox(0.096f, 0.0001f);
        AssertThat(CityLighting.Darkness(forge, 19 * 60)).IsEqualApprox(0.5f * 1.0065f, 0.002f);
        AssertThat(CityLighting.Darkness(forge, 22 * 60)).IsEqualApprox(0.713f, 0.0001f);
        for (var m = 16 * 60.0; m < 30 * 60; m += 1)
        {
            AssertThat(Math.Abs(CityLighting.Darkness(forge, m + 1) - CityLighting.Darkness(forge, m))).IsLess(0.012f);
            var a = CityLighting.StreetGrade(m);
            var b = CityLighting.StreetGrade(m + 1);
            AssertThat(Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B)).IsLess(0.02f);
        }

        // Tarde neutra, véspera quente (mais vermelho que azul), noite fria (mais azul que vermelho).
        AssertThat(CityLighting.StreetGrade(16 * 60)).IsEqual(new LightColor(1f, 1f, 1f));
        var vespera = CityLighting.StreetGrade(18 * 60);
        AssertThat(vespera.R).IsGreater(vespera.B);
        var night = CityLighting.StreetGrade(23 * 60);
        AssertThat(night.B).IsGreater(night.R);
        AssertThat(night.R).IsGreaterEqual(0.75f); // legível: nunca escurece mais que 25% num canal
    }

    [TestCase]
    public void LightShapeAndColors()
    {
        AssertThat(CityLighting.Profile(0)).IsEqual(1f);
        AssertThat(CityLighting.Profile(0.4f)).IsEqualApprox(0.55f, 0.0001f);
        AssertThat(CityLighting.Profile(1f)).IsEqual(0f);
        AssertThat(CityLighting.PlayerLightRadius(false, 1)).IsEqual(72f);
        AssertThat(CityLighting.PlayerLightRadius(true, 1)).IsEqual(40f);
        AssertThat(CityLighting.PlayerLightRadius(false, 0.45)).IsEqualApprox(32.4f, 0.001f);
        AssertThat(CityLighting.GlowAlphaFor("m")).IsEqual(0.22f);
        AssertThat(CityLighting.GlowAlphaFor("a")).IsEqual(0.14f);
        AssertThat(CityLighting.ColorOf("m")).IsEqual(new LightColor(230 / 255f, 74 / 255f, 142 / 255f));
        AssertThat(CityLighting.ColorOf("?")).IsEqual(CityLighting.ColorOf("a"));
        AssertThat(CityLighting.Flicker(0, 0)).IsEqual(1f);
        AssertThat(CityLighting.Flicker(3, 10)).IsBetween(0.95f, 1.05f);
    }

    [TestCase]
    public void LightsJsonUsesRadius46Amber()
    {
        var lights = CityLighting.ParseLightsJson("""{"city":[[1401,117,48,"a"],[765,3,48,"f"]],"mine":[]}""");
        AssertThat(lights["city"].Count).IsEqual(2);
        AssertThat(lights["city"][1]).IsEqual(new MapLight(765, 3, 46, "a"));
        AssertThat(lights["mine"].Count).IsEqual(0);
    }

    [TestCase]
    public void RealLightsJsonHasTheSixV2Keys()
    {
        var lights = CityLighting.ParseLightsJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "lights.json")));
        AssertThat(lights["city"].Count).IsEqual(58);
        AssertThat(lights["fish2"].Count).IsEqual(23);
        AssertThat(lights["crypt2"].Count).IsEqual(46);
        foreach (var map in Maps.Maps)
        {
            if (map.LightsKey is { } key)
            {
                AssertThat(lights.ContainsKey(key)).IsTrue();
            }
        }
    }

    [TestCase]
    public void LampsLightUpBetweenFiveAndSix()
    {
        for (var i = 0; i < 58; i++)
        {
            AssertThat(CityLighting.LampLevel(i, 16.9 * 60)).IsEqual(0f);
            AssertThat(CityLighting.LampLevel(i, 18 * 60)).IsEqual(1f);
            AssertThat(CityLighting.LampLevel(i, 27 * 60)).IsEqual(1f);
        }

        // Acendem uma a uma: às 17:30 parte está acesa, parte apagada.
        var lit = Enumerable.Range(0, 58).Count(i => CityLighting.LampLevel(i, 17.5 * 60) >= 1f);
        AssertThat(lit).IsBetween(10, 48);
    }

    // --- Brechas ---

    private static BrechaSchedule NewSchedule(IRandom random, bool firstDone = false) => new(TestDb.Instance, random, firstDone);

    [TestCase]
    public void FirstBrechaIsTheMarketAfterSixteenSeconds()
    {
        var events = new List<BrechaEvent>();
        var s = NewSchedule(new ConstantRandom(0.5));
        AssertThat(s.NextOpenIn).IsEqual(16.0);
        s.Tick(15.9, 0, 1, events);
        AssertThat(events.Count).IsEqual(0);
        s.Tick(0.2, 0, 1, events);
        AssertThat(events.Count).IsEqual(1);
        var b = events[0].Brecha;
        AssertThat(events[0].Kind).IsEqual(BrechaEventKind.Opened);
        AssertThat(b.ThemeId).IsEqual("fish");
        AssertThat(b.Bairro).IsEqual("Mercado de Peixes");
        AssertThat(b.Rank).IsEqual(0);
        AssertThat(b.MaxTime).IsEqual(300.0);
        AssertThat(b.NpcCloseAt is null).IsTrue();
        AssertThat(b.Spot).IsEqual(0);
        AssertThat(b.Season.Id).IsEqual("outono");
        AssertThat(b.Title).IsEqual("Cais Enevoado");
        AssertThat(s.FirstDone).IsTrue();
        AssertThat(s.NextOpenIn).IsBetween(80.0, 120.0); // R(80,120) com 0,5 = 100
    }

    [TestCase]
    public void ContinueOpensAfterSixSecondsAndUsesSecondSpot()
    {
        var events = new List<BrechaEvent>();
        var s = NewSchedule(new ConstantRandom(0.9), firstDone: true);
        AssertThat(s.NextOpenIn).IsEqual(6.0);
        s.Tick(6, 0, 4, events);
        AssertThat(events[0].Brecha.Spot).IsEqual(0);
        AssertThat(events[0].Brecha.MaxTime).IsEqualApprox(264.0, 0.0001); // R(210,270) com 0,9
        AssertThat(events[0].Brecha.Season.Id).IsEqual("inverno");
        s.OpenSoon(0);
        s.Tick(0.01, 0, 4, events);
        AssertThat(events.Count).IsEqual(2);
        AssertThat(events[1].Brecha.Spot).IsEqual(1);
        AssertThat(s.OpenCount).IsEqual(2);

        // Máximo de 2 abertas: a terceira não abre (e não sorteia nada além do próximo atraso).
        s.OpenSoon(0);
        s.Tick(0.01, 0, 4, events);
        AssertThat(events.Count).IsEqual(2);
    }

    [TestCase]
    public void IgnoredBrechaRupturesWhenTheTimerEnds()
    {
        var events = new List<BrechaEvent>();
        var s = NewSchedule(new ConstantRandom(0.5));
        s.Tick(16, 0, 1, events);
        var b = events[0].Brecha;
        s.Tick(299, 0, 1, events);
        AssertThat(b.Status).IsEqual(BrechaStatus.Open);
        AssertThat(b.TimeLeft).IsEqualApprox(1.0, 0.0001);
        s.Tick(1.5, 0, 1, events);
        AssertThat(b.Status).IsEqual(BrechaStatus.Ruptured);
        AssertThat(events[^1].Kind).IsEqual(BrechaEventKind.Ruptured);
        AssertThat(ReferenceEquals(s.Ruptured, b)).IsTrue();
        AssertThat(s.OpenAt(0) is null).IsTrue();

        AssertThat(ReferenceEquals(s.ResolveInvasion(), b)).IsTrue();
        AssertThat(b.Status).IsEqual(BrechaStatus.Closed);
        AssertThat(s.NextOpenIn).IsLessEqual(55.0);
    }

    [TestCase]
    public void TimerStopsWhileThePlayerIsInside()
    {
        var events = new List<BrechaEvent>();
        var s = NewSchedule(new ConstantRandom(0.5));
        s.Tick(16, 0, 1, events);
        var b = events[0].Brecha;
        AssertThat(ReferenceEquals(s.EnterRun(0), b)).IsTrue();
        s.Tick(400, 0, 1, events);
        AssertThat(b.Status).IsEqual(BrechaStatus.Open);
        AssertThat(b.TimeLeft).IsEqual(300.0);

        // Fuga: continua aberta e o timer volta a correr.
        s.EndRun(cleared: false);
        s.Tick(10, 0, 1, events);
        AssertThat(b.TimeLeft).IsEqualApprox(290.0, 0.0001);

        // Chefe derrotado: fecha e a próxima abre logo.
        s.EnterRun(0);
        s.EndRun(cleared: true);
        AssertThat(b.Status).IsEqual(BrechaStatus.Closed);
        AssertThat(s.NextOpenIn).IsLessEqual(50.0);
        AssertThat(s.Running is null).IsTrue();
    }

    [TestCase]
    public void CasaClosesBrechaAtNpcTime()
    {
        var events = new List<BrechaEvent>();
        // continuar: tema (0,0), rank sem bônus (0,9), timer 210 (0,0), Casa fecha (0,1 < 0,55), npcAt = R(25,120) com 0,5 = 72,5
        // depois: a segunda Brecha (Cemitério, sem Casa) abre aos 106 s e tudo o mais sorteia 0,99.
        var s = NewSchedule(new ThenConstant(0.99, 0.0, 0.9, 0.0, 0.1, 0.5, 0.5), firstDone: true);
        s.Tick(6, 0, 1, events);
        var b = events[0].Brecha;
        AssertThat(b.NpcCloseAt!.Value).IsEqualApprox(72.5, 0.0001);
        s.Tick(210 - 72.5 - 0.01, 0, 1, events);
        AssertThat(b.Status).IsEqual(BrechaStatus.Open);
        s.Tick(0.02, 0, 1, events);
        AssertThat(b.Status).IsEqual(BrechaStatus.Closed);
        AssertThat(events[^1].Kind).IsEqual(BrechaEventKind.ClosedByCasa);
        AssertThat(b.ClosedBy).IsEqual("Casa Ferrugem");
        AssertThat(s.NextOpenIn).IsLessEqual(70.0);
    }

    [TestCase]
    public void NewDayBringsTheNextBrechaSooner()
    {
        var s = NewSchedule(new ConstantRandom(0.5), firstDone: true);
        s.Tick(6, 0, 1, []);
        AssertThat(s.NextOpenIn).IsEqual(100.0);
        s.OnNewDay();
        AssertThat(s.NextOpenIn).IsEqual(15.0);
    }

    [TestCase]
    public void ClosedBrechasLeaveTheListAfterTenMinutes()
    {
        var events = new List<BrechaEvent>();
        var s = NewSchedule(new ConstantRandom(0.5));
        s.Tick(16, 0, 1, events);
        s.EnterRun(0);
        s.EndRun(cleared: true);
        AssertThat(s.Brechas.Count).IsGreaterEqual(1);
        s.Tick(600, 0, 1, events);
        AssertThat(s.Find(events[0].Brecha.Id) is null).IsTrue();
    }

    // --- NPCs e caçadores ---

    [TestCase]
    public void WalkMoverSlidesAndNeverEntersWalls()
    {
        var area = new ShapeWalkArea(100, 100, [WalkShape.FromRect(new MapRect(0, 0, 100, 100))], [new MapRect(40, 0, 20, 70)]);
        var (x, y) = WalkMover.Move(area, 20, 50, 60, 0);
        AssertThat(area.CanStand(x, y, WalkMover.NpcRadius)).IsTrue();
        AssertThat(x).IsLess(40f);

        // Diagonal contra a parede: desliza em y.
        (x, y) = WalkMover.Move(area, 30, 50, 20, 20);
        AssertThat(x).IsLess(40f);
        AssertThat(y).IsEqualApprox(70f, 0.01f);
    }

    [TestCase]
    public void VesnaRouteInTheGuildIsWalkable()
    {
        // Integração v0.1: a Guilda ganhou máscara própria (B4d-D39); a rota tem de valer sobre ela.
        var guild = Maps["guild"];
        var area = new MaskWalkArea(InterioresMapaQaTest.LoadGrid(guild), guild.MaskCell);
        var vesna = TestDb.Instance.Npcs.Walkers.Single(w => w.Id == "vesna");
        var route = WalkerRoute.Build(area, vesna.Path);
        AssertThat(route.Waypoints.Count).IsEqual(3);
        foreach (var leg in route.Legs)
        {
            AssertThat(leg).IsNotNull();
            for (var i = 0; i + 1 < leg!.Count; i++)
            {
                AssertThat(area.CanStand(leg[i].X, leg[i].Y, WalkMover.NpcRadius)).IsTrue();
            }
        }

        var walker = new TownWalker(vesna, route, new ConstantRandom(0.3));
        var visited = new HashSet<int>();
        for (var frame = 0; frame < 60 * 120; frame++)
        {
            walker.Update(area, 1 / 60.0);
            AssertThat(area.CanStand(walker.X, walker.Y, WalkMover.NpcRadius)).IsTrue();
            visited.Add(walker.Leg);
        }

        AssertThat(visited.Count).IsEqual(3); // deu a volta completa
    }

    [TestCase]
    public void WalkerRoutesAroundWallsAndFixesBlockedWaypoints()
    {
        // Duas salas ligadas por um corredor; o segundo waypoint cai dentro da parede.
        var area = new ShapeWalkArea(120, 120,
        [
            WalkShape.FromRect(new MapRect(0, 0, 120, 40)),
            WalkShape.FromRect(new MapRect(100, 40, 20, 40)),
            WalkShape.FromRect(new MapRect(0, 80, 120, 40)),
        ], []);
        var def = new WalkerDef
        {
            Id = "teste",
            Name = "Teste",
            Map = "x",
            HourFrom = 16,
            HourTo = 24,
            Speed = 30,
            Path = [new Point2(20, 20), new Point2(20, 100), new Point2(60, 60)],
            Lines = ["a", "b"],
        };
        var route = WalkerRoute.Build(area, def.Path);
        foreach (var w in route.Waypoints)
        {
            AssertThat(area.CanStand(w.X, w.Y, WalkMover.NpcRadius)).IsTrue(); // (60, 60) estava na parede
        }

        AssertThat(route.Legs[0]!.Count).IsGreater(2); // contorna pelo corredor
        AssertThat(route.Legs[0]!.Any(p => p.X > 100)).IsTrue();

        var walker = new TownWalker(def, route, new ConstantRandom(0.0));
        for (var frame = 0; frame < 60 * 30; frame++)
        {
            walker.Update(area, 1 / 60.0);
            AssertThat(area.CanStand(walker.X, walker.Y, WalkMover.NpcRadius)).IsTrue();
        }

        AssertThat(walker.Talk(walker.X, walker.Y - 10)).IsEqual("a");
        AssertThat(walker.Facing).IsEqual(TownFacing.Up);
        AssertThat(walker.Wait).IsGreaterEqual(4.0);
        AssertThat(walker.Talk(walker.X + 10, walker.Y)).IsEqual("b");
        AssertThat(walker.Facing).IsEqual(TownFacing.Right);
        AssertThat(walker.Talk(walker.X, walker.Y)).IsEqual("a");
    }

    [TestCase]
    public void HunterCountsAndNamesFollowTheV2()
    {
        var random = new ConstantRandom(0.99);
        AssertThat(AmbientHunter.CountFor(Maps["shop_arm"], 3, random)).IsEqual(0);
        AssertThat(AmbientHunter.CountFor(Maps["guild"], 0, random)).IsEqual(0);
        AssertThat(AmbientHunter.CountFor(Maps["guild"], 1, random)).IsEqual(1);
        AssertThat(AmbientHunter.CountFor(Maps["fish2"], 1, random)).IsEqual(0);
        AssertThat(AmbientHunter.CountFor(Maps["forge"], 0, random)).IsEqual(2);
        AssertThat(AmbientHunter.CountFor(Maps["inn"], 0, new ConstantRandom(0.1))).IsEqual(1);

        var picked = AmbientHunter.Pick(TestDb.Instance.Social, "bow", 8, new ConstantRandom(0.4));
        AssertThat(picked.Count).IsEqual(6); // Nina e Caio usam arco
        AssertThat(picked.Any(h => h.Weapon == "bow")).IsFalse();
        AssertThat(picked.Select(h => h.Name).Distinct().Count()).IsEqual(6);
    }

    [TestCase]
    public void HunterWandersWithoutEnteringWalls()
    {
        var area = new MaskWalkArea(InterioresMapaQaTest.LoadGrid(Maps["inn"]), Maps["inn"].MaskCell); // máscara da B4d
        var social = TestDb.Instance.Social;
        var rng = new SeededRandom(7);
        var start = WalkMover.RandomPoint(area, 498, 896, rng, 250, 520, 70);
        var hunter = new AmbientHunter(social.Hunters[0], start.X, start.Y, social, rng);
        var spoke = false;
        for (var frame = 0; frame < 60 * 90; frame++)
        {
            hunter.Update(area, 498, 896, 1 / 60.0);
            AssertThat(area.CanStand(hunter.X, hunter.Y, WalkMover.NpcRadius)).IsTrue();
            spoke |= hunter.Say is not null;
        }

        AssertThat(spoke).IsTrue();
    }

    [TestCase]
    public void ChatFillsPlaceholders()
    {
        var social = TestDb.Instance.Social;
        for (var i = 0; i < 50; i++)
        {
            var line = TownChat.RandomLine(social, new SeededRandom(i));
            AssertThat(line.Contains('{')).IsFalse();
        }

        AssertThat(TownChat.RandomLine(social, new ConstantRandom(0.0))).IsEqual("alguém pra Brecha do cemitério? levo poção");
        AssertThat(TownChat.OpenedLine("Mercado de Peixes", new ConstantRandom(0.0))).IsEqual("abriu uma no Mercado de Peixes!");
        AssertThat(TownChat.ClosedByCasaLine("Casa da Maré", "Cemitério das Velas")).IsEqual("a Casa da Maré fechou a Brecha do Cemitério das Velas");
    }
}

/// <summary>RNG de teste: os valores dados, em ordem, e depois sempre <c>rest</c>.</summary>
internal sealed class ThenConstant(double rest, params double[] values) : IRandom
{
    private int _next;

    public double NextDouble() => _next < values.Length ? values[_next++] : rest;
}
