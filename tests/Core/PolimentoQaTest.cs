namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Common;
using Vespera.Core.Rules;
using Vespera.Core.World;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>QA da frente C1 (rodada 1): as primeiras Brechas do novato e a intenção de sair da Brecha, por muitas sementes.</summary>
[TestSuite]
public class PolimentoQaTest
{
    private static BrechaRules Rules => new(TestDb.Instance);

    [TestCase]
    public void NoviceBrechasNeverExceedTheSealAcrossManySeedsAndSeals()
    {
        // Para cada Selo (E..SSS) e 200 sementes: com a proteção do novato ligada, nenhuma Brecha aberta passa do que o Selo entra.
        for (var seal = 0; seal <= 7; seal++)
        {
            for (var seed = 1; seed <= 200; seed++)
            {
                var events = new List<BrechaEvent>();
                var schedule = new BrechaSchedule(TestDb.Instance, new SeededRandom(seed), firstDone: seed % 2 == 0) { Novice = true };
                for (var i = 0; i < 1500; i++)
                {
                    schedule.Tick(1, seal, 1 + (i / 300), events);
                }

                foreach (var opened in events.Where(e => e.Kind == BrechaEventKind.Opened))
                {
                    AssertThat(Rules.CanEnter(opened.Brecha.Rank, seal))
                        .OverrideFailureMessage($"semente {seed}, Selo {seal}: Brecha {opened.Brecha.ThemeId} rank {opened.Brecha.Rank} fora do que o Selo entra")
                        .IsTrue();
                }
            }
        }
    }

    [TestCase]
    public void FirstBrechaOfANewGameIsEnterableForTheStartingSealWithEverySeed()
    {
        // Jogo novo: Selo E (0). A primeira Brecha (a do Mercado) tem de ser jogável por quem acabou de chegar.
        for (var seed = 1; seed <= 500; seed++)
        {
            var events = new List<BrechaEvent>();
            var schedule = new BrechaSchedule(TestDb.Instance, new SeededRandom(seed), firstDone: false) { Novice = true };
            for (var i = 0; i < 600 && !events.Any(e => e.Kind == BrechaEventKind.Opened); i++)
            {
                schedule.Tick(1, 0, 1, events);
            }

            var first = events.First(e => e.Kind == BrechaEventKind.Opened).Brecha;
            AssertThat(Rules.CanEnter(first.Rank, 0)).OverrideFailureMessage($"semente {seed}: a primeira Brecha ({first.ThemeId}) abriu com rank {first.Rank} para o Selo E").IsTrue();
        }
    }

    [TestCase]
    public void EscapeIntentNeverFiresWithoutHalfASecondOfPushingAndRearmsOnlyOutsideTheZone()
    {
        var intent = new EscapeIntent();
        var dt = 1 / 60.0;

        // Empurra 0,45 s, solta 1 quadro, empurra 0,45 s, … cem vezes: nunca dispara.
        for (var round = 0; round < 100; round++)
        {
            for (var i = 0; i < 27; i++)
            {
                AssertThat(intent.Update(inside: true, pushing: true, dt)).IsFalse();
            }

            intent.Update(inside: true, pushing: false, dt);
        }

        // Dentro da zona sem empurrar por muito tempo: nunca dispara.
        for (var i = 0; i < 6000; i++)
        {
            AssertThat(intent.Update(inside: true, pushing: false, dt)).IsFalse();
        }

        // Empurrar de verdade dispara uma vez; segurar mais não dispara de novo; sair e voltar arma de novo.
        var fired = 0;
        for (var i = 0; i < 600; i++)
        {
            fired += intent.Update(inside: true, pushing: true, dt) ? 1 : 0;
        }

        AssertThat(fired).IsEqual(1);
        intent.Update(inside: false, pushing: true, dt);
        for (var i = 0; i < 40; i++)
        {
            fired += intent.Update(inside: true, pushing: true, dt) ? 1 : 0;
        }

        AssertThat(fired).IsEqual(2);
    }

    [TestCase]
    public void KeepAwayMovesEveryMapArrivalOutsideTheFleeBand()
    {
        // Todas as bordas de fuga do maps.json: uma chegada colada nela (de y-40 a y+10, na faixa dela) sobe até 56 u.
        var maps = MapCatalog.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "maps.json"))).Maps;
        var checkedMaps = 0;
        foreach (var map in maps.Where(m => m.Back is not null))
        {
            var back = map.Back!.Value;
            for (var dy = -40; dy <= 10; dy += 5)
            {
                var at = new MapPoint(back.Center.X, back.Y + dy);
                var away = EscapeIntent.KeepAway(at, back);
                AssertThat(back.Y - away.Y).OverrideFailureMessage($"{map.Id}: chegada em dy={dy} ficou a {back.Y - away.Y} u da borda").IsGreaterEqual(EscapeIntent.SpawnGap - 0.01f);
            }

            // O spawn declarado do mapa (maps.json) depois do KeepAway nunca está na zona.
            var spawn = EscapeIntent.KeepAway(new MapPoint(map.Spawn.X, map.Spawn.Y), back);
            AssertThat(EscapeIntent.InZone(back, spawn.X, spawn.Y)).OverrideFailureMessage($"{map.Id}: o spawn cai dentro da zona de fuga").IsFalse();
            checkedMaps++;
        }

        AssertThat(checkedMaps).IsGreaterEqual(6);
    }
}
