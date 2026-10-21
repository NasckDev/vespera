namespace Vespera.Tests.Core;

using GdUnit4;
using Vespera.Core.Combat.Ai;
using Vespera.Core.World;
using static GdUnit4.Assertions;

/// <summary>Campo de perseguição (DECISIONS D43): contorna paredes, ilhas não alcançam, recálculo barato.</summary>
[TestSuite]
public class ChaseFieldTest
{
    /// <summary>Área 200×100 com uma parede vertical em x ∈ [96, 104) de y 0 a 80 (passagem embaixo).</summary>
    private sealed class WallArea : IWalkArea
    {
        public float Width => 200;

        public float Height => 100;

        public bool IsWalkable(float x, float y) =>
            x >= 0 && y >= 0 && x < Width && y < Height && !(x >= 96 && x < 104 && y < 80) && !(x >= 160 && x < 200 && y < 24 && y >= 0 && (x < 164 || y >= 20));

        public IReadOnlyList<MapRect> BlockedRects() => [];
    }

    private sealed class OpenArea(float w, float h) : IWalkArea
    {
        public float Width => w;

        public float Height => h;

        public bool IsWalkable(float x, float y) => x >= 0 && y >= 0 && x < w && y < h;

        public IReadOnlyList<MapRect> BlockedRects() => [];
    }

    [TestCase]
    public void GoesAroundTheWallWhenTheLineIsBlocked()
    {
        var field = new ChaseField(new WallArea());
        field.UpdateNow(150, 20); // jogador à direita da parede
        AssertThat(field.LineClear(50, 20, 150, 20)).IsFalse();
        AssertThat(field.TryDirection(50, 20, out var ux, out var uy)).IsTrue();
        AssertThat(uy > 0.3f).IsTrue(); // desce para a passagem
        AssertThat(field.Distance(50, 20) > field.Distance(140, 20)).IsTrue();
        AssertThat(field.LineClear(110, 20, 150, 20)).IsTrue();
    }

    [TestCase]
    public void EnclosedPocketIsUnreachable()
    {
        var field = new ChaseField(new WallArea());
        field.UpdateNow(20, 20);
        AssertThat(field.Distance(180, 10)).IsEqual(-1); // bolso fechado em (164..200, 0..20)
        AssertThat(field.Distance(150, 90) > 0).IsTrue();
        AssertThat(field.Reachable(180, 10, 20, 20)).IsFalse();
        AssertThat(field.Reachable(150, 90, 20, 20)).IsTrue();
    }

    [TestCase]
    public void RecomputeOnAFullHdMapIsCheap()
    {
        var field = new ChaseField(new OpenArea(1672, 941));
        field.UpdateNow(10, 10);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var worst = 0.0;
        for (var i = 0; i < 60; i++)
        {
            var t0 = sw.Elapsed.TotalMilliseconds;
            field.Update(10 + (i * 9), 10 + (i * 9));
            worst = Math.Max(worst, sw.Elapsed.TotalMilliseconds - t0);
        }

        AssertThat(worst < 4).IsTrue(); // fatiado: nenhum frame paga o BFS inteiro
        for (var i = 0; i < 10; i++)
        {
            field.Update(1000, 900);
        }

        AssertThat(field.Distance(1000, 900)).IsEqual(0); // terminou e trocou o buffer
        AssertThat(field.Distance(10, 900) > 100).IsTrue(); // mapa inteiro, sem limite de alcance
    }
}
