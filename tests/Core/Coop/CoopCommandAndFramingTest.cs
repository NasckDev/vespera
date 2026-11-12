namespace Vespera.Tests.Core.Coop;

using System.Numerics;
using GdUnit4;
using Vespera.Core.Advancement;
using Vespera.Core.Coop;
using Vespera.Core.Save;
using Vespera.Tests.Core.Support;
using static GdUnit4.Assertions;

/// <summary>Comando de jogador (serializável para o online), câmera compartilhada, coleira e o convidado no save.</summary>
[TestSuite]
public class CoopCommandAndFramingTest
{
    private static readonly Vector2 Screen = new(1920, 1080);

    [TestCase]
    public void CommandRoundTripsThroughTheWire()
    {
        var cmd = new PlayerCommand(4242, 1, 0.6f, -0.8f, CommandButtons.Attack | CommandButtons.Interact, CommandButtons.Dodge);
        var buffer = new byte[PlayerCommand.WireSize];
        cmd.Write(buffer);
        var back = PlayerCommand.Read(buffer);
        AssertThat(back.Tick).IsEqual(4242u);
        AssertThat((int)back.Slot).IsEqual(1);
        AssertThat(back.MoveX).IsEqualApprox(0.6f, 0.0005f);
        AssertThat(back.MoveY).IsEqualApprox(-0.8f, 0.0005f);
        AssertThat(back.IsHeld(CommandButtons.Interact)).IsTrue();
        AssertThat(back.WasPressed(CommandButtons.Dodge)).IsTrue();
        AssertThat(back.WasPressed(CommandButtons.Attack)).IsFalse();
    }

    [TestCase]
    public void DiagonalIsNormalizedAndMergeKeepsPresses()
    {
        var diagonal = new PlayerCommand(1, 0, 1f, 1f, CommandButtons.None, CommandButtons.Attack).Normalized();
        AssertThat(MathF.Sqrt((diagonal.MoveX * diagonal.MoveX) + (diagonal.MoveY * diagonal.MoveY))).IsEqualApprox(1f, 1e-5f);
        var later = new PlayerCommand(1, 0, 0f, -1f, CommandButtons.Heavy, CommandButtons.Heavy);
        var merged = diagonal.Merge(later);
        AssertThat(merged.WasPressed(CommandButtons.Attack)).IsTrue();
        AssertThat(merged.WasPressed(CommandButtons.Heavy)).IsTrue();
        AssertThat(merged.MoveY).IsEqual(-1f);
    }

    [TestCase]
    public void CloseHuntersKeepTheRuleZoomAndCenterBetweenThem()
    {
        var f = CoopFraming.Frame([new Vector2(100, 200), new Vector2(300, 200)], Screen, 1.4f, 85f / 110f, 70f);
        AssertThat(f.ZoomFactor).IsEqual(1f);
        AssertThat(f.Center.X).IsEqual(200f);
        AssertThat(f.AtFloor).IsFalse();
    }

    [TestCase]
    public void FarHuntersZoomOutOnlyToTheFloor()
    {
        var floor = 85f / 110f;
        var mid = CoopFraming.Frame([new Vector2(0, 0), new Vector2(1500, 0)], Screen, 1.4f, floor, 70f);
        AssertThat(mid.ZoomFactor < 1f && mid.ZoomFactor >= floor).IsTrue();
        AssertThat(mid.AtFloor).IsFalse();
        var far = CoopFraming.Frame([new Vector2(0, 0), new Vector2(5000, 0)], Screen, 1.4f, floor, 70f);
        AssertThat(far.ZoomFactor).IsEqual(floor);
        AssertThat(far.AtFloor).IsTrue();
        // No piso, o caçador com 110 px na regra fica com 85 px.
        AssertThat(110f * far.ZoomFactor).IsEqualApprox(85f, 0.01f);
    }

    [TestCase]
    public void TetherStopsAtTheEdgeAndPullsBack()
    {
        var span = CoopFraming.MaxSpan(Screen, 1.4f, 85f / 110f, 70f);
        AssertThat(span.X).IsEqualApprox((1920f / (1.4f * 85f / 110f)) - 140f, 0.01f);
        Vector2[] other = [new Vector2(0, 0)];
        var dt = 1f / 60f;

        // Dentro do limite: livre.
        var free = CoopFraming.Constrain(new Vector2(100, 0), new Vector2(200, 0), other, span, 2.5f, dt);
        AssertThat(free.X).IsEqual(200f);

        // Andando para fora a partir da borda: para nela.
        var edge = new Vector2(span.X - 1f, 0);
        var stopped = CoopFraming.Constrain(edge, new Vector2(600, 0), other, span, 2.5f, dt);
        AssertThat(edge.X + (stopped.X * dt)).IsEqualApprox(span.X, 0.01f);

        // Já fora: não vai mais longe e é puxado de volta; andar para dentro continua valendo.
        var outside = new Vector2(span.X + 100f, 0);
        AssertThat(CoopFraming.Constrain(outside, new Vector2(300, 0), other, span, 2.5f, dt).X < 0f).IsTrue();
        AssertThat(CoopFraming.Constrain(outside, new Vector2(-300, 0), other, span, 2.5f, dt).X <= -300f).IsTrue();
        AssertThat(CoopFraming.Exceeds(outside, other, span)).IsTrue();

        // O puxão é limitado (suave): bem longe, no máximo maxPull px/s de volta.
        var veryFar = new Vector2(span.X + 5000f, 0);
        AssertThat(CoopFraming.Constrain(veryFar, Vector2.Zero, other, span, 2.5f, dt, maxPull: 120f).X).IsEqual(-120f);
        AssertThat(CoopFraming.Exceeds(new Vector2(10, 10), other, span)).IsFalse();
    }

    [TestCase]
    public void GuestHunterRoundTripsInTheSameSlot()
    {
        var save = SaveData.NewGame(TestDb.Instance, "Rui", "sword");
        var guest = new GuestHunter { ClassId = "hammer" };
        guest.Progress.Xp = 120;
        guest.Progress.Fragmentos = 9;
        guest.Progress.WeaponLevels["hammer"] = 2;
        guest.Progress.Items.Add(new ItemInstance { Id = 1, DefId = "x", Slot = "peito", Name = "Peça" });
        save.Guest = guest;

        var back = SaveSerializer.FromJson(SaveSerializer.ToJson(save), TestDb.Instance);
        AssertThat(back.Guest).IsNotNull();
        AssertThat(back.Guest!.ClassId).IsEqual("hammer");
        AssertThat(back.Guest.Progress.Xp).IsEqual(120);
        AssertThat(back.Guest.Progress.Fragmentos).IsEqual(9);
        AssertThat(back.Guest.Progress.WeaponLevels["hammer"]).IsEqual(2);
        AssertThat(back.Guest.Progress.Items.Count).IsEqual(1);
        AssertThat(back.Hunter.Xp).IsEqual(0);
    }

    [TestCase]
    public void SaveWithoutCoopHasNoGuestAndBadClassIsFixed()
    {
        var save = SaveData.NewGame(TestDb.Instance, "Rui", "sword");
        var back = SaveSerializer.FromJson(SaveSerializer.ToJson(save), TestDb.Instance);
        AssertThat(back.Guest).IsNull();

        save.Guest = new GuestHunter { ClassId = "lança", Name = "  " };
        var fixedUp = SaveSerializer.FromJson(SaveSerializer.ToJson(save), TestDb.Instance);
        AssertThat(TestDb.Instance.Classes.ContainsKey(fixedUp.Guest!.ClassId)).IsTrue();
        AssertThat(fixedUp.Guest.Name).IsEqual(GuestHunter.DefaultName);
    }
}
