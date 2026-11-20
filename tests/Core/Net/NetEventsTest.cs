namespace Vespera.Tests.Core.Net;

using GdUnit4;
using Vespera.Core.Net;
using static GdUnit4.Assertions;

/// <summary>Eventos de combate, cenário e campos novos do snapshot (pose, fôlego, flechas, NPCs, pausa do host).</summary>
[TestSuite]
public class NetEventsTest
{
    [TestCase]
    public void EffectBatchRoundTripsEveryKind()
    {
        var effects = new List<NetEffect>
        {
            new(NetEffectKind.Spark, 10, 20, 300, 1.5f, 70, 0, 255, 200, 100, 9),
            new(NetEffectKind.Ring, -5, 6, 120, 0.35f, 6, 0, 255, 255, 0),
            new(NetEffectKind.Number, 33, 44, 1.2f, R: 255, G: 213, Blue: 138, Text: "27"),
            new(NetEffectKind.Shake, A: 0.4f),
            new(NetEffectKind.Slash, 1, 2, 0.5f, 90, 1.8f, 30, N: 2),
        };

        AssertThat(EffectBatch.TryRead(EffectBatch.Write(effects), out var back)).IsTrue();
        AssertThat(back.Count).IsEqual(5);
        AssertThat(back[0].Kind == NetEffectKind.Spark && back[0].N == 9 && back[0].G == 200).IsTrue();
        AssertThat(back[1].Kind == NetEffectKind.Ring && back[1].B == 0.35f).IsTrue();
        AssertThat(back[2].Text).IsEqual("27");
        AssertThat(back[3].A).IsEqual(0.4f);
        AssertThat(back[4].N).IsEqual(2);
        AssertThat(NetProtocol.KindOfBounded(EffectBatch.Write(effects)) == NetMessageKind.Events).IsTrue();
    }

    [TestCase]
    public void EffectBatchRejectsGarbageWithoutThrowing()
    {
        var good = EffectBatch.Write([new NetEffect(NetEffectKind.Number, 1, 2, 1f, Text: "9")]);
        AssertThat(EffectBatch.TryRead(good[..^1], out _)).IsFalse();
        AssertThat(EffectBatch.TryRead([.. good, 0], out _)).IsFalse();
        AssertThat(EffectBatch.TryRead([], out _)).IsFalse();
        AssertThat(EffectBatch.TryRead([(byte)NetMessageKind.Events, 1, 99], out _)).IsFalse();
        // Contagem acima do teto, mesmo sem corpo.
        AssertThat(EffectBatch.TryRead([(byte)NetMessageKind.Events, (byte)(EffectBatch.MaxEffects + 1)], out _)).IsFalse();
        // Tremor com trauma absurdo, anel com duração zero, texto com caractere de controle.
        AssertThat(EffectBatch.TryRead(EffectBatch.Write([new NetEffect(NetEffectKind.Shake, A: 50f)]), out _)).IsFalse();
        AssertThat(EffectBatch.TryRead(EffectBatch.Write([new NetEffect(NetEffectKind.Ring, 0, 0, 10, 0f, 3)]), out _)).IsFalse();
        AssertThat(EffectBatch.TryRead(EffectBatch.Write([new NetEffect(NetEffectKind.Number, 0, 0, 1, Text: "a\nb")]), out _)).IsFalse();
        // NaN no pacote derruba o lote inteiro.
        var nan = EffectBatch.Write([new NetEffect(NetEffectKind.Shake, A: 0.5f)]);
        BitConverter.GetBytes(float.NaN).CopyTo(nan, 3);
        AssertThat(EffectBatch.TryRead(nan, out _)).IsFalse();
    }

    [TestCase]
    public void EffectBatchTrimsLongTextAndCapsTheCount()
    {
        var packet = EffectBatch.Write([new NetEffect(NetEffectKind.Number, 0, 0, 1, Text: new string('9', 100))]);
        AssertThat(EffectBatch.TryRead(packet, out var back)).IsTrue();
        AssertThat(back[0].Text.Length).IsEqual(NetEffect.MaxTextBytes);
        var many = Enumerable.Range(0, EffectBatch.MaxEffects + 1).Select(_ => new NetEffect(NetEffectKind.Shake, A: 0.1f)).ToList();
        AssertThrown(() => EffectBatch.Write(many)).IsInstanceOf<ArgumentException>();
        AssertThat(EffectBatch.Write(many.Take(EffectBatch.MaxEffects).ToList()).Length).IsLess(NetProtocol.MaxEventBytes);
    }

    [TestCase]
    public void SceneRoundTripsAndRejectsBadIdsAndCounts()
    {
        var scene = new SceneSnapshot("praca", 1, [new SceneEntry("lampiao", 10, 20, false), new SceneEntry("barril", -3, 4.5f, true)]);
        AssertThat(SceneSnapshot.TryRead(scene.Write(), out var back)).IsTrue();
        AssertThat(back.LevelId).IsEqual("praca");
        AssertThat((int)back.GroundVariant).IsEqual(1);
        AssertThat(back.Entries.Count).IsEqual(2);
        AssertThat(back.Entries[1].Flip).IsTrue();
        AssertThat(NetProtocol.KindOfBounded(scene.Write()) == NetMessageKind.Scene).IsTrue();

        var bytes = scene.Write();
        AssertThat(SceneSnapshot.TryRead(bytes[..^2], out _)).IsFalse();
        AssertThat(SceneSnapshot.TryRead([.. bytes, 1], out _)).IsFalse();

        // Id da sala vira ".." no pacote: recusado (o id vira caminho de arquivo no convidado).
        var evil = bytes.ToArray();
        evil[2] = (byte)'.';
        evil[3] = (byte)'.';
        AssertThat(SceneSnapshot.TryRead(evil, out _)).IsFalse();

        var tooMany = Enumerable.Range(0, SceneSnapshot.MaxEntries + 1).Select(i => new SceneEntry("barril", i, i, false)).ToList();
        AssertThrown(() => new SceneSnapshot("praca", 0, tooMany).Write()).IsInstanceOf<ArgumentException>();
        var full = new SceneSnapshot("praca", 0, tooMany.Take(SceneSnapshot.MaxEntries).ToList()).Write();
        AssertThat(full.Length).IsLess(NetProtocol.MaxSceneBytes);
    }

    [TestCase]
    public void SnapshotCarriesPoseStaminaArrowsNpcsAndHostPause()
    {
        var snap = new WorldSnapshot(
            9,
            "praca",
            [new NetHunter(1, 5, 6, 3, false, 40, 100, 2, 128, 4, 7)],
            [new NetEnemy(3, 1, 2, 10, 20, "lodo", 2, 5, true)],
            "oi",
            [new NetProjectile(7, 8, 1.25f, true)],
            [new NetNpc("odete", 9, 10, 1, true)],
            WorldSnapshot.FlagHostPaused);
        var bytes = snap.Write();
        AssertThat(bytes.Length).IsLess(NetProtocol.MaxSnapshotBytes);
        AssertThat(WorldSnapshot.TryRead(bytes, out var back)).IsTrue();
        AssertThat((int)back.Hunters[0].Stamina).IsEqual(128);
        AssertThat((int)back.Hunters[0].Anim).IsEqual(4);
        AssertThat((int)back.Hunters[0].Frame).IsEqual(7);
        AssertThat((int)back.Enemies[0].Anim).IsEqual(2);
        AssertThat(back.Enemies[0].FlipH).IsTrue();
        AssertThat(back.Arrows.Count).IsEqual(1);
        AssertThat(back.Arrows[0].Pierce).IsTrue();
        AssertThat(back.NpcList[0].Id).IsEqual("odete");
        AssertThat(back.HostPaused).IsTrue();

        // Sem os campos novos: padrão sem pose, sem flechas, sem NPCs, host rodando.
        var plain = new WorldSnapshot(1, "praca", [new NetHunter(0, 0, 0, 0, false, 1, 1)], []);
        AssertThat(WorldSnapshot.TryRead(plain.Write(), out var back2)).IsTrue();
        AssertThat((int)back2.Hunters[0].Anim).IsEqual(WorldSnapshot.NoAnim);
        AssertThat(back2.HostPaused).IsFalse();
        AssertThat(back2.Arrows.Count + back2.NpcList.Count).IsEqual(0);
    }

    [TestCase]
    public void SnapshotRejectsBadNewFields()
    {
        var snap = new WorldSnapshot(1, "praca", [], [], "", [new NetProjectile(1, 2, 0, false)], [new NetNpc("odete", 1, 2, 1, false)]);
        var bytes = snap.Write();
        // Bandeira desconhecida (o byte de flags fica depois do texto: tick(4)+id(1+5)+caçadores(1)+monstros(2)+texto(2)+kind(1)).
        var flagsAt = 1 + 4 + 1 + 5 + 1 + 2 + 2;
        var bad = bytes.ToArray();
        bad[flagsAt] = 0x80;
        AssertThat(WorldSnapshot.TryRead(bad, out _)).IsFalse();
        // NPC com id perigoso e flechas além do teto.
        var evil = bytes.ToArray();
        var npcId = Array.LastIndexOf(evil, (byte)'o');
        evil[npcId] = (byte)'/';
        AssertThat(WorldSnapshot.TryRead(evil, out _)).IsFalse();
        var arrows = Enumerable.Range(0, WorldSnapshot.MaxProjectiles + 1).Select(_ => new NetProjectile(0, 0, 0, false)).ToList();
        AssertThrown(() => new WorldSnapshot(1, "praca", [], [], "", arrows).Write()).IsInstanceOf<ArgumentException>();
        AssertThat(WorldSnapshot.TryRead(bytes[..^1], out _)).IsFalse();
        AssertThat(WorldSnapshot.TryRead([.. bytes, 0], out _)).IsFalse();
    }

    [TestCase]
    public void WorstCaseSnapshotFitsTheSizeCap()
    {
        var enemies = Enumerable.Range(0, WorldSnapshot.MaxEnemies).Select(i => new NetEnemy(i, i, i, 1, 2, new string('a', 60), 3, 4, true)).ToList();
        var hunters = Enumerable.Range(0, WorldSnapshot.MaxHunters).Select(i => new NetHunter((byte)i, i, i, 1, false, 1, 2, 1, 200, 3, 4)).ToList();
        var arrows = Enumerable.Range(0, WorldSnapshot.MaxProjectiles).Select(_ => new NetProjectile(1, 2, 3, true)).ToList();
        var npcs = Enumerable.Range(0, WorldSnapshot.MaxNpcs).Select(i => new NetNpc(new string('b', 60), i, i, 1, true)).ToList();
        var snap = new WorldSnapshot(1, "praca", hunters, enemies, new string('x', 600), arrows, npcs);
        AssertThat(snap.Write().Length).IsLess(NetProtocol.MaxSnapshotBytes);
        AssertThat(WorldSnapshot.TryRead(snap.Write(), out _)).IsTrue();
    }
}
