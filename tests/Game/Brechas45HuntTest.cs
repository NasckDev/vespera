using System.Text.RegularExpressions;
using GdUnit4;
using static GdUnit4.Assertions;
using Vespera.Core.Advancement;
using Vespera.Core.Hunt;
using Vespera.Core.Ranks;
using Vespera.Core.Rift;
using Vespera.Core.Story;
using Vespera.Game.World;
using Vespera.Tests.Core.Support;

namespace Vespera.Tests.Game;

/// <summary>
/// Brechas dos Atos IV (Cemitério das Lanternas, Velória) e V (Sino Mudo, Sineiro Mudo): caçadas só com os chãos e os
/// monstros do ato, Sala do Silêncio em toda caçada do Sino, Covil dos Guardiões, pontos em chão andável, selos B → A → S
/// (exame B no Cemitério, sem exame A), XP que basta e o fio da história do Ato III ao VI sem travar (ROTEIRO-V2 §13).
/// </summary>
[TestSuite]
public class Brechas45HuntTest
{
    private sealed record Act(string Brecha, string Hunt, string Prefix, string Arena, string Guardian, string[] Monsters);

    private static readonly Act Cemiterio = new("brecha_cemiterio", "cacada_cemiterio", "cemiterio_", "cemiterio_arena", "velora",
        ["carpideira", "cao_de_vigilia", "velinha"]);

    private static readonly Act Sino = new("brecha_sino", "cacada_sino", "torre_", "torre_arena", "sineiro_mudo",
        ["sineiro", "casca", "traca_de_arquivo"]);

    private static readonly Act[] Acts = [Cemiterio, Sino];

    private static string Dir(string name) => Path.Combine(AppContext.BaseDirectory, name);

    private static HuntConfig Config(Act act) => HuntConfig.Parse(File.ReadAllText(Path.Combine(Dir("Cacada"), $"{act.Hunt}.json")));

    private static BrechaDef Brecha(Act act) => BrechaDef.Parse(File.ReadAllText(Path.Combine(Dir("Data"), $"{act.Brecha}.json")));

    private static LevelData Level(string id) => LevelJson.ParseLevel(File.ReadAllText(Path.Combine(Dir("Levels"), $"{id}.json")));

    private static LevelGrid Grid(LevelData level) => LevelGrid.Parse(level.Cell, level.Cols, level.Rows, level.Grid);

    private static StoryScript Script() => StoryScript.Parse(File.ReadAllText(Path.Combine(Dir("Dialogue"), "story.json")));

    private static StoryEngine Engine() => new(new StoryProgress(), Script());

    private static DialogueRunner Start(string id, StoryEngine engine)
    {
        var runner = new DialogueRunner(DialogueFile.Parse(File.ReadAllText(Path.Combine(Dir("Dialogue"), $"{id}.json"))), engine, "Rui");
        AssertThat(runner.Start()).IsTrue();
        return runner;
    }

    private static void Finish(DialogueRunner runner)
    {
        var steps = 0;
        while (runner.Advance(0) && steps++ < 40)
        {
        }
    }

    [TestCase]
    public void HuntConfigsAreValid()
    {
        foreach (var act in Acts)
        {
            AssertThat(Config(act).Validate()).OverrideFailureMessage($"{act.Hunt} inválido").IsEmpty();
        }
    }

    [TestCase]
    public void RunsUseOnlyTheActGroundsAndMonsters()
    {
        foreach (var act in Acts)
        {
            var planner = new RunPlanner(Config(act));
            var third = 0;
            for (var seed = 1; seed <= 100; seed++)
            {
                var plan = planner.Plan(seed, 1, false);
                AssertThat(plan.Rooms[^1].Ground).IsEqual(act.Arena);
                AssertThat(plan.Rooms[0].Waves.SelectMany(w => w).All(s => s.Monster == act.Monsters[0])).IsTrue();
                foreach (var room in plan.Rooms)
                {
                    AssertThat(room.Ground.StartsWith(act.Prefix, StringComparison.Ordinal)).IsTrue();
                    foreach (var spawn in room.Waves.SelectMany(w => w))
                    {
                        AssertThat(act.Monsters).Contains(spawn.Monster);
                        third += spawn.Monster == act.Monsters[2] ? 1 : 0;
                    }
                }
            }

            AssertThat(third).OverrideFailureMessage($"{act.Hunt}: o terceiro monstro nunca sai").IsGreater(0);
        }
    }

    [TestCase]
    public void SilentRoomIsInEverySinoRunAndRoomsNeverRepeat()
    {
        foreach (var act in Acts)
        {
            var planner = new RunPlanner(Config(act));
            for (var seed = 1; seed <= 200; seed++)
            {
                foreach (var plan in new[] { planner.Plan(seed, 0, false), planner.Plan(seed, 1, false), planner.Plan(seed, 1, true) })
                {
                    var grounds = plan.Rooms.Select(r => r.Ground).ToList();
                    if (act == Sino)
                    {
                        AssertThat(grounds).Contains("torre_silencio");
                    }

                    for (var i = 1; i < grounds.Count; i++)
                    {
                        AssertThat(grounds[i]).OverrideFailureMessage($"{act.Hunt} semente {seed}: sala repetida {grounds[i]}").IsNotEqual(grounds[i - 1]);
                    }
                }
            }
        }
    }

    [TestCase]
    public void GuardianWaitsAfterOneFullRun()
    {
        foreach (var act in Acts)
        {
            var config = Config(act);
            AssertThat(config.GuardianAfterRuns).IsEqual(1);
            AssertThat(config.GuardianFlag).IsEqual(string.Empty);
            var plan = new RunPlanner(config).Plan(7, 1, true);
            AssertThat(plan.HasGuardian).IsTrue();
            AssertThat(plan.Rooms[^1].Ground).IsEqual(act.Arena);
            var arena = Level(act.Arena);
            AssertThat(arena.Rift!.Boss!.Type).IsEqual(act.Guardian);
        }
    }

    [TestCase]
    public void BrechaDefsMatchTheirRoomsMonstersAndGuardian()
    {
        foreach (var act in Acts)
        {
            var brecha = Brecha(act);
            AssertThat(brecha.GuardianId).IsEqual(act.Guardian);
            AssertThat(brecha.Monsters[act.Guardian].Boss).IsTrue();
            AssertThat(brecha.Boss.Phases.Count).IsEqual(2);
            foreach (var monster in act.Monsters)
            {
                AssertThat(brecha.Monsters.ContainsKey(monster)).OverrideFailureMessage($"{act.Brecha}: sem {monster}").IsTrue();
                AssertThat(brecha.Monsters[monster].Telegraph).IsBetween(0.4, 0.8);
            }

            AssertThat(brecha.Rooms.Count).IsBetween(4, 6);
            foreach (var room in brecha.Rooms)
            {
                AssertThat(File.Exists(Path.Combine(Dir("Levels"), $"{room}.json"))).OverrideFailureMessage($"fase {room} ausente").IsTrue();
                AssertThat(Config(act).Grounds.ContainsKey(room)).IsTrue();
            }
        }
    }

    [TestCase]
    public void EveryRoomPointIsOnWalkableFloorAndEveryRoomHasAWayOut()
    {
        foreach (var act in Acts)
        {
            foreach (var (id, ground) in Config(act).Grounds)
            {
                var level = Level(id);
                var grid = Grid(level);
                AssertThat(level.DoorHeight).IsEqual(80f);
                var points = ground.Spawns.Concat(ground.Breakables).ToList();
                points.Add([level.Spawn[0], level.Spawn[1]]);
                if (ground.Rest.Length == 2)
                {
                    points.Add(ground.Rest);
                }

                var rift = level.Rift!;
                points.AddRange(new[] { rift.Chest, rift.ExitPortal, rift.Boss?.At }.OfType<float[]>());
                foreach (var p in points)
                {
                    AssertThat(grid.IsBlockedAt(p[0], p[1])).OverrideFailureMessage($"{id}: ponto {p[0]},{p[1]} em parede").IsFalse();
                }

                AssertThat(rift.Exit is not null || rift.ExitPortal is not null).OverrideFailureMessage($"{id}: sala sem saída").IsTrue();
                AssertThat(rift.Silent).IsEqual(id == "torre_silencio");
                if (ground.Kinds.Contains(RoomKind.Treasure))
                {
                    AssertThat(rift.Chest).OverrideFailureMessage($"{id}: tesouro sem baú").IsNotNull();
                }
            }
        }
    }

    [TestCase]
    public void XpOfOneHuntPlusTheGuardianRunReachesTheNextSeal()
    {
        // B 650 -> A 1100 (Velória + exame B); A 1100 -> S 1800 (Sineiro Mudo, sem exame).
        var catalog = AdvTestData.Catalog;
        foreach (var (act, from, to, exam) in new[] { (Cemiterio, Rank.B, Rank.A, 100), (Sino, Rank.A, Rank.S, 0) })
        {
            var brecha = Brecha(act);
            var planner = new RunPlanner(Config(act));
            var need = catalog.Step(to).Xp - catalog.Step(from).Xp;
            var worst = int.MaxValue;
            for (var seed = 1; seed <= 50; seed++)
            {
                var xp = 0.0;
                foreach (var plan in new[] { planner.Plan(seed, 0, false), planner.Plan(seed + 1000, 1, true) })
                {
                    xp += plan.Rooms.SelectMany(r => r.Waves).SelectMany(w => w).Sum(s => brecha.Monsters[s.Monster].Xp * (s.Elite ? 2 : 1));
                }

                worst = Math.Min(worst, (int)xp + brecha.Monsters[act.Guardian].Xp + exam);
            }

            AssertThat(worst).OverrideFailureMessage($"{act.Brecha}: pior caçada dá {worst} XP, faltam {need}").IsGreaterEqual(need);
        }
    }

    [TestCase]
    public void SealBNeedsTheExamAndSealANeedsNone()
    {
        var engine = AdvTestData.HunterAt(Rank.B);
        engine.RecordGuardianKill("velora");
        engine.Progress.Xp = AdvTestData.Catalog.Step(Rank.A).Xp;
        engine.AddXp(0);
        AssertThat(engine.Rank).IsEqual(Rank.B);
        engine.TryPassExam(Rank.B);
        AssertThat(engine.Rank).IsEqual(Rank.A);

        AssertThat(engine.TryPassExam(Rank.A).Status).IsEqual(ExamStatus.NoExamForRank);
        engine.RecordGuardianKill("sineiro_mudo");
        engine.AddXp(AdvTestData.Catalog.Step(Rank.S).Xp - engine.Progress.Xp);
        AssertThat(engine.Rank).IsEqual(Rank.S);
    }

    [TestCase]
    public void StoryRunsFromTheCemeteryBreachToTheLowerStreets()
    {
        var engine = Engine();
        var events = new List<string>();
        engine.EventRaised += events.Add;
        engine.SetFlag("exame_b_visto");

        // Ato IV: o Vigia dá o objetivo da Brecha (Ato45StoryTest); uma caçada abre a Velória.
        engine.SetObjective("ato4_brecha");
        AssertThat(engine.Script.Objective("ato4_brecha")!.Locked).IsFalse();
        engine.SetFlag("cemiterio_das_lanternas:hunt_1");
        AssertThat(engine.ObjectiveId).IsEqual("ato4_guardiao");
        engine.SetFlag("GuardianDefeated:velora");
        AssertThat(engine.ObjectiveId).IsEqual("ato4_fecho");
        AssertThat(engine.HasFlag("act4_done")).IsTrue();
        AssertThat(engine.AutoplayFor("cemiterio")?.Dialogue).IsEqual("ato4/benicio");
        var fecho4 = Start("ato4/benicio", engine);
        AssertThat(fecho4.Text).IsEqual("A.");
        Finish(fecho4);
        AssertThat(events).Contains("BurnSeal:A");
        AssertThat(engine.ObjectiveId).IsEqual("ato5");

        // Ato V: porta e confissão do Benício, uma caçada, o Sineiro Mudo e o fecho com o selo S.
        Finish(Start("ato5/benicio", engine));
        Finish(Start("ato5/benicio", engine));
        AssertThat(engine.ObjectiveId).IsEqual("ato5_brecha");
        AssertThat(engine.Script.Objective("ato5_brecha")!.Locked).IsFalse();
        engine.SetFlag("sino_mudo:hunt_1");
        AssertThat(engine.ObjectiveId).IsEqual("ato5_guardiao");
        engine.SetFlag("GuardianDefeated:sineiro_mudo");
        AssertThat(engine.ObjectiveId).IsEqual("ato5_fecho");
        AssertThat(engine.HasFlag("act5_done")).IsTrue();
        AssertThat(engine.AutoplayFor("torre")?.Dialogue).IsEqual("ato5/benicio");
        var fecho5 = Start("ato5/benicio", engine);
        Finish(fecho5);
        AssertThat(events).Contains("BurnSeal:S");
        AssertThat(engine.ObjectiveId).IsEqual("ato6");
        AssertThat(engine.Script.Objective("ato6")!.Locked).IsFalse();

        // As passagens abrem pelas mesmas flags: Cemitério -> Torre (Velória), Forjas -> Ruas Baixas (Sineiro Mudo).
        var toTorre = Level("cemiterio").Zones.Single(z => z.Target == "torre");
        AssertThat(StoryConditions.Evaluate(toTorre.Requires, engine)).IsTrue();
        var toRuas = Level("forjas").Zones.Single(z => z.Target == "ruas_baixas");
        AssertThat(StoryConditions.Evaluate(toRuas.Requires, engine)).IsTrue();
    }

    [TestCase]
    public void DeathLinesPlayInOrderOnReturnAndKeepTwoSentencesPerBalloon()
    {
        foreach (var (brecha, hub, dialogue, flag) in new[]
                 {
                     ("cemiterio_das_lanternas", "cemiterio", "ato4/morte", "ato4_morte"),
                     ("sino_mudo", "torre", "ato5/morte", "ato5_morte"),
                 })
        {
            var engine = Engine();
            engine.SetFlag("exame_b_visto");
            engine.SetFlag("ato5_porta");
            engine.SetFlag("ato5_confissao");
            for (var n = 1; n <= 6; n++)
            {
                engine.SetFlag($"{brecha}:fell_{n}");
                AssertThat(engine.HasFlag($"{flag}_{n}")).IsTrue();
                AssertThat(engine.AutoplayFor(hub)?.Dialogue).OverrideFailureMessage($"{hub}: queda {n} sem fala").IsEqual(dialogue);
                var runner = Start(dialogue, engine);
                AssertThat(runner.CurrentId).IsEqual($"m{n}");
                Finish(runner);
                AssertThat(engine.AutoplayFor(hub)?.Dialogue ?? string.Empty).IsNotEqual(dialogue);
            }

            var file = DialogueFile.Parse(File.ReadAllText(Path.Combine(Dir("Dialogue"), $"{dialogue}.json")));
            foreach (var (id, node) in file.Nodes)
            {
                var sentences = Regex.Matches(node.Text, @"[.!?](?=\s|$)").Count;
                AssertThat(sentences).OverrideFailureMessage($"{dialogue}/{id}: {sentences} frases em um balão").IsLessEqual(2);
            }
        }
    }

    [TestCase]
    public void GuardianLinesComeFromTheScript()
    {
        var velora = DialogueFile.Parse(File.ReadAllText(Path.Combine(Dir("Dialogue"), "ato4", "velora.json")));
        AssertThat(velora.Nodes["a"].Text).IsEqual("Qual delas é você?");
        var sineiro = DialogueFile.Parse(File.ReadAllText(Path.Combine(Dir("Dialogue"), "ato5", "sineiro.json")));
        AssertThat(new[] { "a", "b", "c" }.Select(k => sineiro.Nodes[k].Text).ToArray()).ContainsExactly("FESTA", "CORDA", "CULPA");
    }

    [TestCase]
    public void NewMonstersAreInTheScaleContract()
    {
        foreach (var id in Cemiterio.Monsters.Concat(Sino.Monsters).Append("velora").Append("sineiro_mudo"))
        {
            var entry = ScaleRegistry.Get(id);
            AssertThat(entry.Sheet).IsEqual($"res://assets/v2/sprites/monstros/{id}/{id}.json");
            AssertThat(File.Exists(Path.Combine(Dir("Sprites"), "monstros", id, $"{id}.json"))).IsTrue();
        }

        AssertThat(ScaleRegistry.Get("velora").Height).IsEqual(3.00f);
        AssertThat(ScaleRegistry.Get("sineiro_mudo").Height).IsEqual(3.00f);
    }
}
